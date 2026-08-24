using System;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Unmath;
using Math = Unmath.Math;

public class RenderGraph
{
	private readonly List<RenderTargetInfo> targets = new();
	private readonly List<RenderTargetIdentifier> exportedResources = new();
	private readonly List<IRenderPass> renderPasses = new();
	private readonly List<RenderTargetIdentifier> resources = new();
	private readonly List<string> passNames = new();
	private readonly List<Range> inputRanges = new();
	private readonly List<Range> inputAttachmentRanges = new();
	private readonly List<Range> outputRanges = new();
	private readonly List<ViewHandle> passViewHandles = new();
	private readonly List<ViewInfo> viewInfos = new();

	private TextureHandle[] passInputs = new TextureHandle[8], passInputAttachments = new TextureHandle[8], passOutputs = new TextureHandle[8];
	private int inputCount, inputAttachmentCount, outputCount;

	public TextureHandle GetTexture(RenderTargetDescriptor descriptor, int propertyId)
	{
		targets.Add(new(descriptor, propertyId));
		return new(targets.Count - 1);
	}

	public RenderPass<T> AddRenderPass<T>(string name, ViewHandle viewHandle, T data = default, ReadOnlySpan<TextureHandle> outputs = default, ReadOnlySpan<TextureHandle> inputs = default, ReadOnlySpan<TextureHandle> inputAttachments = default, Action<CommandBuffer, T> render = default)
	{
		var index = renderPasses.Count;

		passNames.Add(name);
		inputRanges.Add(SetInputs(inputs, index));
		inputAttachmentRanges.Add(SetInputAttachments(inputAttachments, index));
		outputRanges.Add(SetOutputs(outputs, index));
		passViewHandles.Add(viewHandle);

		var renderPass = new RenderPass<T>(data, render);
		renderPasses.Add(renderPass);
		return renderPass;
	}

	public void SetResourceReadIndex(TextureHandle handle, int index)
	{
		var target = targets[handle.index];
		target.lastReadIndex = index;
		targets[handle.index] = target;
	}

	public void SetResourceWriteIndex(TextureHandle handle, int index)
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

	public void ExportResource(TextureHandle handle, RenderTargetIdentifier id)
	{
		var resourceIndex = exportedResources.Count;
		exportedResources.Add(id);

		var target = targets[handle.index];
		target.resourceIndex = resourceIndex;
		target.isExported = true;
		targets[handle.index] = target;
	}

	private static Range SetItems(ReadOnlySpan<TextureHandle> items, ref int count, ref TextureHandle[] array)
	{
		var start = count;
		var newCount = count + items.Length;

		if (array.Length < newCount)
			Array.Resize(ref array, Math.Max(newCount, array.Length * 2));

		items.CopyTo(array.AsSpan(start, items.Length));
		count = newCount;

		return start..count;
	}

	public Range SetInputs(ReadOnlySpan<TextureHandle> items, int passIndex)
	{
		foreach (var item in items)
			SetResourceReadIndex(item, passIndex);
		return SetItems(items, ref inputCount, ref passInputs);
	}

	public Range SetInputAttachments(ReadOnlySpan<TextureHandle> items, int passIndex)
	{
		// We tread input attachments as 'writes' to prevent them from resolving too early since a resolve is done after all writes
		foreach (var item in items)
			SetResourceWriteIndex(item, passIndex);
		return SetItems(items, ref inputAttachmentCount, ref passInputAttachments);
	}

	public Range SetOutputs(ReadOnlySpan<TextureHandle> items, int passIndex)
	{
		foreach (var item in items)
			SetResourceWriteIndex(item, passIndex);
		return SetItems(items, ref outputCount, ref passOutputs);
	}

	public ViewHandle AddViewInfo(Int2 size, int samples = 1)
	{
		var index = viewInfos.Count;
		viewInfos.Add(new(size, samples));
		return new(index);
	}

	public void Clear()
	{
		targets.Clear();
		exportedResources.Clear();
		renderPasses.Clear();
		resources.Clear();
		passNames.Clear();
		inputRanges.Clear();
		inputAttachmentRanges.Clear();
		outputRanges.Clear();
		passViewHandles.Clear();
		viewInfos.Clear();
		inputCount = 0;
		outputCount = 0;
		inputAttachmentCount = 0;
	}

	public void Execute(CommandBuffer command)
	{
		for (var i = 0; i < renderPasses.Count; i++)
		{
			var attachmentHandles = new FixedBuffer<TextureHandle>(stackalloc TextureHandle[8]);
			var subpasses = new FixedBuffer<SubPassDescriptor>(stackalloc SubPassDescriptor[8]);
			var passOutputIndices = new FixedBuffer<int>(stackalloc int[8]);
			var passInputIndices = new FixedBuffer<int>(stackalloc int[8]);
			var depthIndex = -1;

			var renderPass = renderPasses[i];

			// Inputs
			foreach (var input in passInputs[inputRanges[i]])
			{
				var target = targets[input.index];
				var resource = resources[target.resourceIndex];
				command.SetGlobalTexture(target.propertyId, resource);
			}

			var viewHandle = passViewHandles[i];
			var viewData = viewInfos[viewHandle.index];

			// Outputs
			var outputRange = outputRanges[i];
			foreach (var output in passOutputs[outputRange])
			{
				// Check if handle already exists, otherwise add
				var index = -1;
				for (var j = 0; j < attachmentHandles.Count; j++)
				{
					if (attachmentHandles[j].index == output.index)
					{
						index = j;
						break;
					}
				}

				if (index == -1)
				{
					index = attachmentHandles.Count;
					_ = attachmentHandles.Add(output);
				}

				var target = targets[output.index];
				var isColor = target.descriptor.format switch
				{
					GraphicsFormat.D16_UNorm or GraphicsFormat.D24_UNorm or GraphicsFormat.D32_SFloat or GraphicsFormat.D16_UNorm_S8_UInt or GraphicsFormat.D24_UNorm_S8_UInt or GraphicsFormat.D32_SFloat_S8_UInt or GraphicsFormat.S8_UInt => false,
					_ => true,
				};

				if (isColor)
					_ = passOutputIndices.Add(index);
				else
					depthIndex = index;
			}

			// Input attachments
			var flags = SubPassFlags.None;
			var inputAttachmentRange = inputAttachmentRanges[i];
			foreach (var inputAttachment in passInputAttachments[inputAttachmentRange])
			{
				// Check if handle already exists, otherwise add
				var index = -1;
				for (var j = 0; j < attachmentHandles.Count; j++)
				{
					if (attachmentHandles[j].index == inputAttachment.index)
					{
						index = j;
						break;
					}
				}

				if (index == -1)
				{
					index = attachmentHandles.Count;
					_ = attachmentHandles.Add(inputAttachment);
				}
				if(index == depthIndex)
				{
					flags |= SubPassFlags.ReadOnlyDepth;
				}

				_ = passInputIndices.Add(index);
			}

			var attachments = new FixedBuffer<AttachmentDescriptor>(stackalloc AttachmentDescriptor[8]);
			for (var j = 0; j < attachmentHandles.Count; j++)
			{
				var attachment = attachmentHandles[j];

				var target = targets[attachment.index];
				var attachmentDescriptor = new AttachmentDescriptor
				{
					graphicsFormat = target.descriptor.format
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

				var isColor = target.descriptor.format switch
				{
					GraphicsFormat.D16_UNorm or GraphicsFormat.D24_UNorm or GraphicsFormat.D32_SFloat or GraphicsFormat.D16_UNorm_S8_UInt or GraphicsFormat.D24_UNorm_S8_UInt or GraphicsFormat.D32_SFloat_S8_UInt or GraphicsFormat.S8_UInt => false,
					_ => true,
				};

				// If this is the last pass, it needs to be resolved
				var requiresResolve = viewData.samples > 1 && i == target.lastWriteIndex && isColor;
				if (requiresResolve)
				{
					target.resourceIndex = resources.Count;
					var resourceId = Shader.PropertyToID(target.resourceIndex.ToString());
					command.GetTemporaryRT(resourceId, target.descriptor.GetRenderTextureDescriptor(1, viewInfos[target.descriptor.viewHandle.index]));
					resources.Add(resourceId);
					targets[attachment.index] = target;
					attachmentDescriptor.resolveTarget = resources[target.resourceIndex];
					attachmentDescriptor.storeAction = RenderBufferStoreAction.Resolve;
				}
				else
				{
					// Depth targets can't be msaa resolved so we need to store the msaa version.
					var requiresMsaaStore = viewData.samples > 1 && (i < target.lastWriteIndex || i == target.lastWriteIndex && !isColor);
					if (requiresMsaaStore)
					{
						if (isFirstWrite)
						{
							target.resourceIndex = resources.Count;
							var resourceId = Shader.PropertyToID(target.resourceIndex.ToString());
							command.GetTemporaryRT(resourceId, target.descriptor.GetRenderTextureDescriptor(viewData.samples, viewInfos[target.descriptor.viewHandle.index]));
							resources.Add(resourceId);
							targets[attachment.index] = target;
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
								attachmentDescriptor.loadStoreTarget = exportedResources[target.resourceIndex];
							}
							else
							{
								if (isFirstWrite)
								{
									target.resourceIndex = resources.Count;
									var resourceId = Shader.PropertyToID(target.resourceIndex.ToString());
									command.GetTemporaryRT(resourceId, target.descriptor.GetRenderTextureDescriptor(1, viewInfos[target.descriptor.viewHandle.index]));
									resources.Add(resourceId);
									targets[attachment.index] = target;
								}

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

				_ = attachments.Add(attachmentDescriptor);
			}

			var isNativeRenderPass = !outputRange.Start.Equals(outputRange.End);
			if (isNativeRenderPass)
			{
				_ = subpasses.Add(new() { inputs = new(passInputIndices.Span.AsArray()), colorOutputs = new(passOutputIndices.Span.AsArray()), flags = flags });

				var passName = passNames[i];
				Span<byte> debugNameUtf8 = stackalloc byte[Encoding.UTF8.GetByteCount(passName)];
				_ = Encoding.UTF8.GetBytes(passName, debugNameUtf8);

				command.BeginRenderPass(viewData.size.x, viewData.size.y, 1, viewData.samples, attachments.Span.AsArray(), depthIndex, -1, subpasses.Span.AsArray(), debugNameUtf8);
			}

			renderPass.Execute(command);

			if (isNativeRenderPass)
				command.EndRenderPass();
		}
	}
}
