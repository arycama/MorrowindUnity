using System;
using System.Collections.Generic;
using System.Text;
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
	private readonly List<Range> outputRanges = new();
	private readonly List<ViewHandle> passViewHandles = new();
	private readonly List<ViewInfo> viewInfos = new();

	private TextureHandle[] passInputs = new TextureHandle[8], passOutputs = new TextureHandle[8];
	private int inputCount, outputCount;

	public TextureHandle GetTexture(RenderTargetDescriptor descriptor, int propertyId)
	{
		targets.Add(new(descriptor, propertyId));
		return new(targets.Count - 1);
	}

	public RenderPass<T> AddRenderPass<T>(string name, bool isNativeRenderPass, ViewHandle viewHandle, T data, ReadOnlySpan<TextureHandle> outputs, ReadOnlySpan<TextureHandle> inputs, Action<CommandBuffer, T> render)
	{
		var index = renderPasses.Count;

		passNames.Add(name);
		inputRanges.Add(SetInputs(inputs, index));
		outputRanges.Add(SetOutputs(outputs, index));
		passViewHandles.Add(viewHandle);

		var renderPass = new RenderPass<T>(isNativeRenderPass, data, render);
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

	public Range SetInputs(ReadOnlySpan<TextureHandle> inputs, int passIndex)
	{
		var start = inputCount;
		var newCount = inputCount + inputs.Length;

		if (passInputs.Length < newCount)
			Array.Resize(ref passInputs, Math.Max(newCount, passInputs.Length * 2));

		inputs.CopyTo(passInputs.AsSpan(start, inputs.Length));
		inputCount = newCount;

		foreach(var input in inputs)
			SetResourceReadIndex(input, passIndex);

		return start..inputCount;
	}

	public Range SetOutputs(ReadOnlySpan<TextureHandle> outputs, int passIndex)
	{
		var start = outputCount;
		var newCount = outputCount + outputs.Length;

		if (passOutputs.Length < newCount)
			Array.Resize(ref passOutputs, Math.Max(newCount, passOutputs.Length * 2));

		outputs.CopyTo(passOutputs.AsSpan(start, outputs.Length));
		outputCount = newCount;

		foreach (var output in outputs)
			SetResourceWriteIndex(output, passIndex);

		return start..outputCount;
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
		outputRanges.Clear();
		passViewHandles.Clear();
		viewInfos.Clear();
		inputCount = 0;
		outputCount = 0;
	}

	public void Execute(CommandBuffer command)
	{
		var attachments = new FixedBuffer<AttachmentDescriptor>(stackalloc AttachmentDescriptor[8]);
		var subpasses = new FixedBuffer<SubPassDescriptor>(stackalloc SubPassDescriptor[8]);
		var colorOutputs = new FixedBuffer<int>(stackalloc int[8]);
		var depthIndex = -1;

		for (var i = 0; i < renderPasses.Count; i++)
		{
			var renderPass = renderPasses[i];
			var viewHandle = passViewHandles[i];
			var viewData = viewInfos[viewHandle.index];

			foreach (var output in passOutputs[outputRanges[i]])
			{
				var target = targets[output.index];
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
					command.GetTemporaryRT(resourceId, target.descriptor.GetRenderTextureDescriptor(1));
					resources.Add(resourceId);
					targets[output.index] = target;
					attachmentDescriptor.resolveTarget = resources[target.resourceIndex];
					attachmentDescriptor.storeAction = RenderBufferStoreAction.Resolve;
				}
				else
				{
					// Depth targets can't be msaa resolved so we need to store the msaa version.
					var requiresMsaaStore = viewData.samples > 1 && (i < target.lastWriteIndex || (i == target.lastWriteIndex && !isColor));
					if (requiresMsaaStore)
					{
						if (isFirstWrite)
						{
							target.resourceIndex = resources.Count;
							var resourceId = Shader.PropertyToID(target.resourceIndex.ToString());
							command.GetTemporaryRT(resourceId, target.descriptor.GetRenderTextureDescriptor(viewData.samples));
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
								attachmentDescriptor.loadStoreTarget = exportedResources[target.resourceIndex];
							}
							else
							{
								if (isFirstWrite)
								{
									target.resourceIndex = resources.Count;
									var resourceId = Shader.PropertyToID(target.resourceIndex.ToString());
									command.GetTemporaryRT(resourceId, target.descriptor.GetRenderTextureDescriptor(1));
									resources.Add(resourceId);
									targets[output.index] = target;
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

				var index = attachments.Count;
				attachments.Add(attachmentDescriptor);

				if (isColor)
					colorOutputs.Add(index);
				else
					depthIndex = index;
			}

			foreach (var input in passInputs[inputRanges[i]])
			{
				var target = targets[input.index];
				var resource = resources[target.resourceIndex];
				command.SetGlobalTexture(target.propertyId, resource);
			}

			if (renderPass.IsNativeRenderPass)
			{
				subpasses.Add(new() { colorOutputs = new(colorOutputs.Span.AsArray()) });
				colorOutputs.Clear();

				var passName = passNames[i];
				Span<byte> debugNameUtf8 = stackalloc byte[Encoding.UTF8.GetByteCount(passName)];
				_ = Encoding.UTF8.GetBytes(passName, debugNameUtf8);

				command.BeginRenderPass(viewData.size.x, viewData.size.y, viewData.samples, attachments.Span.AsArray(), depthIndex, subpasses.Span.AsArray(), debugNameUtf8);
				subpasses.Clear();
				attachments.Clear();
				depthIndex = -1;
			}

			renderPass.Execute(command);

			if (renderPass.IsNativeRenderPass)
				command.EndRenderPass();
		}
	}
}
