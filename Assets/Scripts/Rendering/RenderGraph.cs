using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Unmath;

public partial class RenderGraph : IDisposable
{
	private readonly List<IRenderPass> renderPasses = new();
	private readonly List<ViewInfo> viewInfos = new();
	private readonly List<RenderTargetIdentifier> resources = new();
	private readonly ResizableArray<RenderTargetInfo> targets = new();
	private readonly ResizableArray<TextureHandle> handles = new();
	public NativeRenderPassSystem nativeRenderPassSystem = new();

	private readonly PassBuilder builder;

	public RenderGraph()
	{
		builder = new(this);
	}

	public TextureHandle GetTexture(RenderTargetDescriptor descriptor, int propertyId)
	{
		targets.Add(new(descriptor, propertyId));
		return new(targets.Count - 1);
	}

	public void Dispose()
	{
		nativeRenderPassSystem.Dispose();
	}

	public PassBuilder AddRenderPass(string name)
	{
		builder.Name = name;
		builder.Index = renderPasses.Count;
		return builder;
	}

	private void SetRenderPass(string name, ViewHandle viewHandle, int index, IRenderPass renderPass, List<TextureHandle> resources, List<TextureHandle> outputs, List<TextureHandle> inputs)
	{
		// Set resources
		var inputStart = handles.Count;
		foreach (var resource in resources)
		{
			targets[resource.index].lastReadIndex = index;
			handles.Add(resource);
		}

		// Set outputs
		foreach (var output in outputs)
			SetResourceWriteIndex(output, index);

		// Set inputs
		foreach (var input in inputs)
			SetResourceWriteIndex(input, index);

		var (nativePassIndex, isNewSubPass) = nativeRenderPassSystem.AddRenderPass(name, index, targets, resources, outputs, inputs);

		renderPass.ResourceRange = inputStart..handles.Count;
		renderPass.IsNewSubPass = isNewSubPass;
		renderPass.ViewHandle = viewHandle;
		renderPass.Name = name;
		renderPass.NativePassIndex = nativePassIndex;

		renderPasses.Add(renderPass);
	}

	public bool IsResourceWritten(TextureHandle resource)
	{
		return targets[resource.index].lastWriteIndex != -1;
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
		// TODO: This might not be neccessary and might make culling passes not possible?
		target.lastReadIndex = index;
		targets[handle.index] = target;
	}

	public void ExportResource(TextureHandle handle, RenderTargetIdentifier id)
	{
		var resourceIndex = resources.Count;
		resources.Add(id);

		ref var target = ref targets[handle.index];
		target.resourceIndex = resourceIndex;
		target.isExported = true;
	}

	public ViewHandle AddViewInfo(Int2 size, int samples = 1)
	{
		var index = viewInfos.Count;
		viewInfos.Add(new(size, samples));
		return new(index);
	}

	public void Execute(CommandBuffer command)
	{
		var lastNativePass = -1;

		for (var i = 0; i < renderPasses.Count; i++)
		{
			var renderPass = renderPasses[i];

			// Set resources. TODO: Any other pass setup/initialization like cbuffers or render state (wireframe?) here, also mip generation etc.
			foreach (var input in handles[renderPass.ResourceRange])
			{
				var target = targets[input.index];

				if (target.resourceIndex == -1)
				{
					Debug.LogError($"Pass {renderPass.Name} couldn't find resource for descriptor {target.descriptor}");
					return;
				}

				var resource = resources[target.resourceIndex];
				command.SetGlobalTexture(target.propertyId, resource);
			}

			if (renderPass.NativePassIndex != lastNativePass)
			{
				// End current pass if needed
				if (lastNativePass != -1)
				{
					command.EndRenderPass();
					lastNativePass = -1;
				}

				if (renderPass.NativePassIndex > -1)
				{
					var nativePassDesc = nativeRenderPassSystem.GetDescriptor(renderPass.NativePassIndex);
					var attachmentHandles = nativePassDesc.attachments;
					var viewInfo = viewInfos[renderPass.ViewHandle.index];

					// Resolve the attachments to their final values
					var attachments = new FixedBuffer<AttachmentDescriptor>(stackalloc AttachmentDescriptor[8]);
					foreach (var attachment in attachmentHandles)
					{
						var target = targets[attachment.index];
						var attachmentDesc = new AttachmentDescriptor
						{
							graphicsFormat = target.descriptor.format
						};

						// Load the target if it has been written to before this renderpass, otherwise clear it if required
						var isFirstWrite = target.firstWriteIndex >= i && target.firstWriteIndex <= nativePassDesc.passEndIndex;
						if (isFirstWrite)
						{
							if (target.descriptor.clear)
							{
								attachmentDesc.loadAction = RenderBufferLoadAction.Clear;
								attachmentDesc.clearColor = target.descriptor.clearColor;
								attachmentDesc.clearDepth = target.descriptor.clearDepth;
								attachmentDesc.clearStencil = target.descriptor.clearStencil;
							}
							else
								attachmentDesc.loadAction = RenderBufferLoadAction.DontCare;
						}
						else
						{
							// If this target has been written previously, it must be loaded
							attachmentDesc.loadStoreTarget = resources[target.resourceIndex];
						}

						var isColor = target.descriptor.format switch
						{
							GraphicsFormat.D16_UNorm or GraphicsFormat.D24_UNorm or GraphicsFormat.D32_SFloat or GraphicsFormat.D16_UNorm_S8_UInt or GraphicsFormat.D24_UNorm_S8_UInt or GraphicsFormat.D32_SFloat_S8_UInt or GraphicsFormat.S8_UInt => false,
							_ => true,
						};

						// If this is the last pass, it needs to be resolved
						var requiresResolve = viewInfo.samples > 1 && nativePassDesc.passEndIndex == target.lastWriteIndex && isColor;
						if (requiresResolve)
						{
							target.resourceIndex = resources.Count;
							var resourceId = Shader.PropertyToID(target.resourceIndex.ToString());
							command.GetTemporaryRT(resourceId, target.descriptor.GetRenderTextureDescriptor(1, viewInfo));
							resources.Add(resourceId);
							targets[attachment.index] = target;
							attachmentDesc.resolveTarget = resources[target.resourceIndex];
							attachmentDesc.storeAction = RenderBufferStoreAction.Resolve;
						}
						else
						{
							// Depth targets can't be msaa resolved so we need to store the msaa version.
							var requiresMsaaStore = viewInfo.samples > 1 && (nativePassDesc.passEndIndex < target.lastWriteIndex || nativePassDesc.passEndIndex == target.lastWriteIndex && !isColor);
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

								attachmentDesc.loadStoreTarget = resources[target.resourceIndex];
							}
							else
							{
								// A store is required if the target is read outside of this nativePass, or it is exported
								var requiresStore = target.lastReadIndex > nativePassDesc.passEndIndex || target.isExported;
								if (requiresStore)
								{
									if (target.isExported)
									{
										attachmentDesc.loadStoreTarget = resources[target.resourceIndex];
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

										attachmentDesc.loadStoreTarget = resources[target.resourceIndex];
									}
								}
								else
								{
									attachmentDesc.storeAction = RenderBufferStoreAction.DontCare;
								}
							}
						}

						_ = attachments.Add(attachmentDesc);
					}

					Span<byte> debugNameUtf8 = stackalloc byte[Encoding.UTF8.GetByteCount(nativePassDesc.debugName)];
					_ = Encoding.UTF8.GetBytes(nativePassDesc.debugName, debugNameUtf8);

					//command.BeginSample(renderPass.NativePassIndex.ToString());
					command.BeginRenderPass(viewInfo.size.x, viewInfo.size.y, 1, viewInfo.samples, attachments.Span.AsArray(), nativePassDesc.depthIndex, -1, nativePassDesc.subpasses, debugNameUtf8);
					lastNativePass = renderPass.NativePassIndex;
				}
			}
			else if (renderPass.IsNewSubPass)
				command.NextSubPass();

			command.BeginSample(renderPass.Name);
			renderPasses[i].Execute(command);
			command.EndSample(renderPass.Name);
		}
	}

	public void Clear()
	{
		targets.Clear();
		renderPasses.Clear();
		resources.Clear();
		viewInfos.Clear();
		nativeRenderPassSystem.Clear();
		handles.Clear();
	}
}