using System;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Unmath;
using Math = Unmath.Math;

public class RenderGraph : IDisposable
{
	private readonly List<RenderTargetInfo> targets = new();
	private readonly List<IRenderPass> renderPasses = new();
	private readonly List<RenderTargetIdentifier> resources = new();
	private readonly List<ViewHandle> passViewHandles = new();
	private readonly List<ViewInfo> viewInfos = new();

	// Resources
	private readonly List<Range> resourceRanges = new();
	private TextureHandle[] resourceHandles = new TextureHandle[8];
	private int resourceHandleCount;

	// Native Renderpass
	private readonly NativeList<TextureHandle> attachmentHandles = new(8, Allocator.Persistent);
	private readonly NativeList<int> passOutputIndices = new(8, Allocator.Persistent);
	private readonly NativeList<int> passInputIndices = new(8, Allocator.Persistent);
	private readonly NativeList<SubPassDescriptor> subPasses = new(8, Allocator.Persistent);
	private int depthIndex = -1;
	private SubPassFlags flags;
	private readonly StringBuilder passNameBuilder = new();

	// Subpasses
	private readonly List<NativeRenderPassDescriptor> nativeRenderPassDescriptors = new();
	private readonly List<int> nativeRenderPassIndices = new();
	private readonly List<bool> newSubPassFlags = new();

	public TextureHandle GetTexture(RenderTargetDescriptor descriptor, int propertyId)
	{
		targets.Add(new(descriptor, propertyId));
		return new(targets.Count - 1);
	}

	public void AddRenderPass<T>(string name, ViewHandle viewHandle, T data = default, ReadOnlySpan<TextureHandle> outputs = default, ReadOnlySpan<TextureHandle> resources = default, ReadOnlySpan<TextureHandle> inputs = default, Action<CommandBuffer, T> render = default)
	{
		passViewHandles.Add(viewHandle);

		var index = renderPasses.Count;
		var renderPass = new RenderPass<T>(data, render);
		renderPasses.Add(renderPass);

		// Ensure capacity and add the input indices to the array
		var newCount = resourceHandleCount + resources.Length;
		if (resourceHandles.Length < newCount)
			Array.Resize(ref resourceHandles, Math.Max(newCount, resourceHandles.Length * 2));
		resources.CopyTo(resourceHandles.AsSpan(resourceHandleCount, resources.Length));

		var inputStart = resourceHandleCount;
		resourceHandleCount += resources.Length;
		resourceRanges.Add(inputStart..resourceHandleCount);

		// Native render pass logic
		var nativeRenderPassDescriptorIndex = -1;
		var isInNativeRenderPass = attachmentHandles.Length > 0;

		// Process resources
		var canMergeWithExistingPass = true;
		foreach (var resource in resources)
		{
			// Mark each resource as read by this pass
			var target = targets[resource.index];
			target.lastReadIndex = index;
			targets[resource.index] = target;

			// Check to see if any of the resources read are part of the current render pass
			for (var i = 0; i < attachmentHandles.Length; i++)
			{
				if (attachmentHandles[i].index != resource.index)
					continue;

				canMergeWithExistingPass = false;
				break;
			}
		}

		// If a pass has started and can't be merged, end it and reset the data
		var isNativeRenderPass = outputs.Length > 0;
		if (isInNativeRenderPass && (!isNativeRenderPass || !canMergeWithExistingPass))
		{
			var passEndIndex = index - 1; // Since htis is called from the first pass that is not the render pass index, the previous pass is the end index
			nativeRenderPassDescriptors.Add(new(new(attachmentHandles.AsArray(), Allocator.Temp), new(subPasses.AsArray(), Allocator.Temp), depthIndex, passEndIndex, passNameBuilder.ToString()));
			attachmentHandles.Clear();
			subPasses.Clear();
			_ = passNameBuilder.Clear();
			depthIndex = -1;
		}

		int GetAttachmentIndex(TextureHandle attachment)
		{
			// Check if handle already exists, otherwise add
			for (var i = 0; i < attachmentHandles.Length; i++)
				if (attachmentHandles[i].index == attachment.index)
					return i;

			attachmentHandles.Add(attachment);
			return attachmentHandles.Length - 1;
		}

		// Outputs
		foreach (var output in outputs)
		{
			SetResourceWriteIndex(output, index);
			var attachmentIndex = GetAttachmentIndex(output);
			var target = targets[output.index];
			var isColor = target.descriptor.format switch
			{
				GraphicsFormat.D16_UNorm or GraphicsFormat.D24_UNorm or GraphicsFormat.D32_SFloat or GraphicsFormat.D16_UNorm_S8_UInt or GraphicsFormat.D24_UNorm_S8_UInt or GraphicsFormat.D32_SFloat_S8_UInt or GraphicsFormat.S8_UInt => false,
				_ => true,
			};

			if (isColor)
				passOutputIndices.Add(attachmentIndex);
			else
				depthIndex = attachmentIndex;
		}

		// Input attachments
		foreach (var inputAttachment in inputs)
		{
			SetResourceWriteIndex(inputAttachment, index);
			var attachmentIndex = GetAttachmentIndex(inputAttachment);
			passInputIndices.Add(attachmentIndex);
			if (attachmentIndex == depthIndex)
				flags |= SubPassFlags.ReadOnlyDepth;
		}

		var isNewSubPass = false;
		if (isNativeRenderPass)
		{
			nativeRenderPassDescriptorIndex = nativeRenderPassDescriptors.Count;
			isNewSubPass = true;
			subPasses.Add(new() { inputs = new(passInputIndices.AsArray()), colorOutputs = new(passOutputIndices.AsArray()), flags = flags });
			passOutputIndices.Clear();
			passInputIndices.Clear();
			flags = SubPassFlags.None;

			if (passNameBuilder.Length > 0)
				_ = passNameBuilder.Append(", ");
			_ = passNameBuilder.Append(name);
		}

		nativeRenderPassIndices.Add(nativeRenderPassDescriptorIndex);
		newSubPassFlags.Add(isNewSubPass);
	}

	private void SetResourceWriteIndex(TextureHandle handle, int index)
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
		var resourceIndex = resources.Count;
		resources.Add(id);

		var target = targets[handle.index];
		target.resourceIndex = resourceIndex;
		target.isExported = true;
		targets[handle.index] = target;
	}

	public ViewHandle AddViewInfo(Int2 size, int samples = 1)
	{
		var index = viewInfos.Count;
		viewInfos.Add(new(size, samples));
		return new(index);
	}

	public void Execute(CommandBuffer command)
	{
		var currentNativeRenderPass = -1;

		for (var i = 0; i < renderPasses.Count; i++)
		{
			// Set resources. TODO: Any other pass setup/initialization like cbuffers or render state (wireframe?) here, also mip generation etc.
			foreach (var input in resourceHandles[resourceRanges[i]])
			{
				var target = targets[input.index];
				var resource = resources[target.resourceIndex];
				command.SetGlobalTexture(target.propertyId, resource);
			}

			var nativeRenderPassIndex = nativeRenderPassIndices[i];
			if (nativeRenderPassIndex != currentNativeRenderPass)
			{
				// End current pass if needed
				if (currentNativeRenderPass != -1)
				{
					command.EndRenderPass();
					currentNativeRenderPass = -1;
				}

				if (nativeRenderPassIndex > -1)
				{
					var nativeRenderPassDescriptor = nativeRenderPassDescriptors[nativeRenderPassIndex];
					var attachmentHandles = nativeRenderPassDescriptor.attachments;
					var viewHandle = passViewHandles[i];
					var viewInfo = viewInfos[viewHandle.index];

					// Resolve the attachments to their final values
					var attachments = new FixedBuffer<AttachmentDescriptor>(stackalloc AttachmentDescriptor[8]);
					for (var j = 0; j < attachmentHandles.Length; j++)
					{
						var attachment = attachmentHandles[j];

						var target = targets[attachment.index];
						var attachmentDescriptor = new AttachmentDescriptor
						{
							graphicsFormat = target.descriptor.format
						};

						// Load the target if it has been written to before this renderpass, otherwise clear it if required
						var isFirstWrite = target.firstWriteIndex >= i && target.firstWriteIndex <= nativeRenderPassDescriptor.passEndIndex;
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
						var requiresResolve = viewInfo.samples > 1 && nativeRenderPassDescriptor.passEndIndex == target.lastWriteIndex && isColor;
						if (requiresResolve)
						{
							target.resourceIndex = resources.Count;
							var resourceId = Shader.PropertyToID(target.resourceIndex.ToString());
							command.GetTemporaryRT(resourceId, target.descriptor.GetRenderTextureDescriptor(1, viewInfo));
							resources.Add(resourceId);
							targets[attachment.index] = target;
							attachmentDescriptor.resolveTarget = resources[target.resourceIndex];
							attachmentDescriptor.storeAction = RenderBufferStoreAction.Resolve;
						}
						else
						{
							// Depth targets can't be msaa resolved so we need to store the msaa version.
							var requiresMsaaStore = viewInfo.samples > 1 && (nativeRenderPassDescriptor.passEndIndex < target.lastWriteIndex || nativeRenderPassDescriptor.passEndIndex == target.lastWriteIndex && !isColor);
							if (requiresMsaaStore)
							{
								if (isFirstWrite)
								{
									target.resourceIndex = resources.Count;
									var resourceId = Shader.PropertyToID(target.resourceIndex.ToString());
									command.GetTemporaryRT(resourceId, target.descriptor.GetRenderTextureDescriptor(viewInfo.samples, viewInfo));
									resources.Add(resourceId);
									targets[attachment.index] = target;
								}

								attachmentDescriptor.loadStoreTarget = resources[target.resourceIndex];
								attachmentDescriptor.storeAction = RenderBufferStoreAction.Store;
							}
							else
							{
								// A store is required if the target is read outside of this nativeRenderPass, or it is exported
								var requiresStore = target.lastReadIndex > nativeRenderPassDescriptor.passEndIndex || target.isExported;
								if (requiresStore)
								{
									if (target.isExported)
									{
										attachmentDescriptor.loadStoreTarget = resources[target.resourceIndex];
									}
									else
									{
										if (isFirstWrite)
										{
											target.resourceIndex = resources.Count;
											var resourceId = Shader.PropertyToID(target.resourceIndex.ToString());
											command.GetTemporaryRT(resourceId, target.descriptor.GetRenderTextureDescriptor(1, viewInfo));
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

					Span<byte> debugNameUtf8 = stackalloc byte[Encoding.UTF8.GetByteCount(nativeRenderPassDescriptor.debugName)];
					_ = Encoding.UTF8.GetBytes(nativeRenderPassDescriptor.debugName, debugNameUtf8);

					command.BeginRenderPass(viewInfo.size.x, viewInfo.size.y, 1, viewInfo.samples, attachments.Span.AsArray(), nativeRenderPassDescriptor.depthIndex, -1, nativeRenderPassDescriptor.subpasses, debugNameUtf8);
					currentNativeRenderPass = nativeRenderPassIndex;
				}
			}
			else if (newSubPassFlags[i])
				command.NextSubPass();

			renderPasses[i].Execute(command);
		}
	}

	public void Clear()
	{
		targets.Clear();
		renderPasses.Clear();
		resources.Clear();
		resourceRanges.Clear();
		passViewHandles.Clear();
		viewInfos.Clear();
		nativeRenderPassDescriptors.Clear();
		nativeRenderPassIndices.Clear();
		newSubPassFlags.Clear();
		resourceHandleCount = 0;
	}

	public void Dispose()
	{
		attachmentHandles.Dispose();
		passOutputIndices.Dispose();
		passInputIndices.Dispose();
		subPasses.Dispose();
	}
}
