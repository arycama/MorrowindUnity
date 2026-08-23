using System;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

public class RenderGraph
{
	private readonly List<RenderTargetInfo> targets = new();
	private readonly List<RenderTargetIdentifier> resources = new();
	private readonly List<RenderTargetIdentifier> importedResources = new();
	private readonly List<IRenderPass> renderPasses = new();

	public TextureHandle GetTexture(RenderTargetDescriptor descriptor, bool dontResolve)
	{
		targets.Add(new(descriptor, -1, -1, -1, dontResolve, false));
		return new(targets.Count - 1);
	}

	public RenderPass<T> AddRenderPass<T>(string name, bool invertCulling, T data, Action<CommandBuffer, T> render)
	{
		var index = renderPasses.Count;
		var renderPass = new RenderPass<T>(name, index, invertCulling, this, data, render);
		renderPasses.Add(renderPass);
		return renderPass;
	}

	public void SetTargetReadIndex(TextureHandle handle, int index)
	{
		var target = targets[handle.index];
		target.lastReadIndex = index;
		targets[handle.index] = target;
	}

	public void SetTargetWriteIndex(TextureHandle handle, int index)
	{
		var target = targets[handle.index];

		// Track the first pass this target is written to so we know when to clear. This also allows allocation to be skipped for textures that are never written to
		if (target.firstWriteIndex == -1)
			target.firstWriteIndex = index;

		// Writes are also treataed as reads for the purposes of resource tracking, this stops a texture from being discarded as a future write (Eg a 2nd pass to the same RT) would not be treated as a read otherwise, and would cause the texture to be discarded after the first pass
		target.lastReadIndex = index;
		targets[handle.index] = target;
	}

	public void ExportTexture(TextureHandle handle, RenderTargetIdentifier id)
	{
		var resourceIndex = importedResources.Count;
		importedResources.Add(id);

		var target = targets[handle.index];
		target.resourceIndex = resourceIndex;
		target.isImported = true;
		targets[handle.index] = target;
	}

	public void Execute(CommandBuffer command)
	{
		// TODO: Can we use spans
		var attachments = new NativeList<AttachmentDescriptor>(8, Allocator.Temp);
		var subpasses = new NativeList<SubPassDescriptor>(8, Allocator.Temp);
		var colorOutputs = new NativeList<int>(8, Allocator.Temp);
		var depthIndex = -1;

		for (var i = 0; i < renderPasses.Count; i++)
		{
			var renderPass = renderPasses[i];
			foreach (var output in renderPass.Outputs)
			{
				var target = targets[output.handle.index];
				var attachmentDescriptor = new AttachmentDescriptor
				{
					graphicsFormat = target.descriptor.format
				};

				// Clear the target on the first write if needed, or just leave contents uninitialized. If this is not the first write, then it will default to a load action.
				if (i == target.firstWriteIndex)
				{
					if (target.descriptor.clear)
					{
						attachmentDescriptor.loadAction = RenderBufferLoadAction.Clear;
						attachmentDescriptor.clearColor = target.descriptor.clearColor;
						attachmentDescriptor.clearDepth = target.descriptor.clearDepth;
						attachmentDescriptor.clearStencil = target.descriptor.clearStencil;
					}
					else
						attachmentDescriptor.loadAction = RenderBufferLoadAction.DontCare;
				}

				// If this is the last time this target is read, it does not need to be stored. Otherwise it needs to be stored or resolved depending on sample count
				var requiresResolve = !output.dontResolve && target.descriptor.samples > 1;

				// TODO: Can this be combined with the 2nd branch at all
				if (target.isImported)
				{
					// Imported targets are always resolved or stored
					var resource = importedResources[target.resourceIndex];
					if (requiresResolve)
					{
						attachmentDescriptor.resolveTarget = resource;
						attachmentDescriptor.storeAction = RenderBufferStoreAction.Resolve;
					}
					else
					{
						attachmentDescriptor.loadStoreTarget = resource;
					}
				}
				else
				{
					RenderTargetIdentifier resource;
					var requiresLoad = i > target.firstWriteIndex;
					var requiresStore = i < target.lastReadIndex;

					if (requiresLoad)
					{
						// If this target has already been written to, use it's current resource
						resource = resources[target.resourceIndex];

						if (requiresStore)
						{
							if (requiresResolve)
							{
								attachmentDescriptor.resolveTarget = resource;
								attachmentDescriptor.storeAction = RenderBufferStoreAction.Resolve;
							}
							else
							{
								attachmentDescriptor.loadStoreTarget = resource;
							}
						}
						else
						{
							attachmentDescriptor.loadStoreTarget = resource;
							attachmentDescriptor.storeAction = RenderBufferStoreAction.DontCare;
						}
					}
					else if (requiresStore)
					{
						// Dynamic targets only need to be stored if they are read in a later renderpass
						target.resourceIndex = resources.Count;
						command.GetTemporaryRT(target.resourceIndex, target.descriptor.GetDescriptor(target.dontResolve));
						resource = target.resourceIndex;
						resources.Add(resource);
						targets[output.handle.index] = target;

						if (requiresResolve)
						{
							attachmentDescriptor.resolveTarget = resource;
							attachmentDescriptor.storeAction = RenderBufferStoreAction.Resolve;
						}
						else
						{
							attachmentDescriptor.loadStoreTarget = resource;
						}
					}
					else
					{
						attachmentDescriptor.storeAction = RenderBufferStoreAction.DontCare;
					}
				}

				var index = attachments.Length;
				attachments.Add(attachmentDescriptor);

				switch (target.descriptor.format)
				{
					case GraphicsFormat.D16_UNorm:
					case GraphicsFormat.D24_UNorm:
					case GraphicsFormat.D32_SFloat:
					case GraphicsFormat.D16_UNorm_S8_UInt:
					case GraphicsFormat.D24_UNorm_S8_UInt:
					case GraphicsFormat.D32_SFloat_S8_UInt:
						depthIndex = index;
						break;
					default:
						colorOutputs.Add(index);
						break;
				}
			}

			foreach (var input in renderPass.Inputs)
			{
				var target = targets[input.handle.index];
				var resource = resources[target.resourceIndex];
				command.SetGlobalTexture(input.propertyId, resource);
			}

			if (renderPass.IsNativeRenderPass)
			{
				if (renderPass.InvertCulling)
					command.SetInvertCulling(true);

				subpasses.Add(new() { colorOutputs = new(colorOutputs.AsArray()) });
				colorOutputs.Clear();

				Span<byte> debugNameUtf8 = stackalloc byte[Encoding.UTF8.GetByteCount(renderPass.Name)];
				_ = Encoding.UTF8.GetBytes(renderPass.Name, debugNameUtf8);

				command.BeginRenderPass(renderPass.Size.x, renderPass.Size.y, renderPass.Samples, attachments.AsArray(), depthIndex, subpasses.AsArray(), debugNameUtf8);
				subpasses.Clear();
				attachments.Clear();
				depthIndex = -1;
			}

			renderPass.Execute(command);

			if (renderPass.IsNativeRenderPass)
			{
				command.EndRenderPass();
				if (renderPass.InvertCulling)
					command.SetInvertCulling(false);
			}
		}

		targets.Clear();
		resources.Clear();
		importedResources.Clear();
		renderPasses.Clear();
	}
}
