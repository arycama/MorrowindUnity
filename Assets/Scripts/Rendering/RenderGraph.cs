using System;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

public class RenderGraph
{
	private readonly List<RenderTargetInfo> targets = new();
	private readonly List<RenderTargetIdentifier> resources = new();
	private readonly List<RenderTargetIdentifier> importedResources = new();
	private readonly List<IRenderPass> renderPasses = new();

	public TextureHandle GetTexture(RenderTargetDescriptor descriptor)
	{
		targets.Add(new(descriptor));
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

		// We also track the last write index so that we know when to resolve if msaa is enabled
		target.lastWriteIndex = index;

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
		target.isExported = true;
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
				var target = targets[output.index];
				var attachmentDescriptor = new AttachmentDescriptor
				{
					graphicsFormat = target.descriptor.format
				};

				var isColor = target.descriptor.format switch
				{
					GraphicsFormat.D16_UNorm or GraphicsFormat.D24_UNorm or GraphicsFormat.D32_SFloat or GraphicsFormat.D16_UNorm_S8_UInt or GraphicsFormat.D24_UNorm_S8_UInt or GraphicsFormat.D32_SFloat_S8_UInt or GraphicsFormat.S8_UInt => false,
					_ => true,
				};

				// Clear the target on the first write if needed, or just leave contents uninitialized. If this is not the first write, then it will default to a load action.
				var isFirstWrite = i == target.firstWriteIndex;
				if (isFirstWrite)
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
				else
				{
					// If this target has been written previously, it must be loaded
					attachmentDescriptor.loadStoreTarget = resources[target.resourceIndex];
					attachmentDescriptor.loadAction = RenderBufferLoadAction.Load;
				}

				// If this is the last pass, it needs to be resolved
				var requiresResolve = renderPass.Samples > 1 && i == target.lastWriteIndex && isColor;
				if (requiresResolve)
				{
					target.resourceIndex = resources.Count;
					var resourceId = Shader.PropertyToID(target.resourceIndex.ToString());
					command.GetTemporaryRT(resourceId, target.descriptor.GetRenderTextureDescriptor(1));
					resources.Add(resourceId);
					targets[output.index] = target;
					attachmentDescriptor.resolveTarget = resources[target.resourceIndex];
					attachmentDescriptor.storeAction = RenderBufferStoreAction.Resolve;
				}
				else
				{
					// Depth targets can't be msaa resolved so we need to store the msaa version.
					var requiresMsaaStore = renderPass.Samples > 1 && (i < target.lastWriteIndex || (i == target.lastWriteIndex && !isColor));
					if (requiresMsaaStore)
					{
						if (isFirstWrite)
						{
							target.resourceIndex = resources.Count;
							var resourceId = Shader.PropertyToID(target.resourceIndex.ToString());
							command.GetTemporaryRT(resourceId, target.descriptor.GetRenderTextureDescriptor(renderPass.Samples));
							resources.Add(resourceId);
							targets[output.index] = target;
						}

						attachmentDescriptor.loadStoreTarget = resources[target.resourceIndex];
						attachmentDescriptor.storeAction = RenderBufferStoreAction.Store;
					}
					else
					{
						var requiresStore = i < target.lastReadIndex || target.isExported;
						if (requiresStore)
						{
							if (target.isExported)
							{
								attachmentDescriptor.loadStoreTarget = importedResources[target.resourceIndex];
							}
							else
							{
								target.resourceIndex = resources.Count;
								var resourceId = Shader.PropertyToID(target.resourceIndex.ToString());
								command.GetTemporaryRT(resourceId, target.descriptor.GetRenderTextureDescriptor(1));
								resources.Add(resourceId);
								targets[output.index] = target;
								attachmentDescriptor.loadStoreTarget = resources[target.resourceIndex];
							}

							attachmentDescriptor.storeAction = RenderBufferStoreAction.Store;
						}
						else
						{
							attachmentDescriptor.storeAction = RenderBufferStoreAction.DontCare;
						}
					}
				}

				var index = attachments.Length;
				attachments.Add(attachmentDescriptor);

				if (isColor)
					colorOutputs.Add(index);
				else
					depthIndex = index;
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
