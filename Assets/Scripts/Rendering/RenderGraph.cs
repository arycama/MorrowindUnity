using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Unmath;

public class RenderGraph : IDisposable
{
	private readonly List<IRenderPass> renderPasses = new();
	private readonly List<ViewInfo> viewInfos = new();
	private readonly List<RenderTargetIdentifier> resources = new();
	private readonly ResizableArray<RenderTargetInfo> targets = new();
	private readonly ResizableArray<TextureHandle> handles = new();
	private readonly TextureHandleSystem textureHandleSystem = new();
	private readonly NativeRenderPassSystem nativeRenderPassSystem = new();
	private readonly PassBuilder builder;
	public int FrameIndex { get; private set; }

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

	public void SetRenderPass(PassBuilder builder)
	{
		var inputStart = handles.Count;
		foreach (var resource in builder.Resources)
		{
			targets[resource].lastReadIndex = builder.Index;
			handles.Add(resource);
		}

		var resourceRange = inputStart..handles.Count;

		if (builder.DepthStencil.index != -1)
			SetResourceWriteIndex(builder.DepthStencil, builder.Index);

		foreach (var output in builder.Outputs)
			SetResourceWriteIndex(output, builder.Index);

		foreach (var input in builder.Inputs)
			SetResourceWriteIndex(input, builder.Index);

		// UAV resources are handled specially
		var uavStart = handles.Count;
		foreach (var resource in builder.UavOutputs)
		{
			handles.Add(resource);
			SetResourceWriteIndex(resource, builder.Index);
		}

		var uavResourceRange = uavStart..handles.Count;

		var (nativePassIndex, isNewSubPass) = nativeRenderPassSystem.AddRenderPass(builder);

		var renderPass = builder.RenderPass;
		renderPass.ResourceRange = resourceRange;
		renderPass.UavResourceRange = uavResourceRange;
		renderPass.IsNewSubPass = isNewSubPass;
		renderPass.ViewHandle = builder.ViewHandle;
		renderPass.Name = builder.Name;
		renderPass.NativePassIndex = nativePassIndex;
		renderPass.Keywords = new(builder.Keywords);

		renderPasses.Add(renderPass);
	}

	public bool IsResourceWritten(TextureHandle resource)
	{
		return targets[resource].lastWriteIndex != -1;
	}

	private void SetResourceWriteIndex(TextureHandle handle, int index)
	{
		ref var target = ref targets[handle];

		// Track the first pass this target is written to so we know when to clear. This also allows allocation to be skipped for textures that are never written to
		if (target.firstWriteIndex == -1)
			target.firstWriteIndex = index;

		// We also track the last write index so that we know when to resolve if msaa is enabled
		target.lastWriteIndex = index;

		// Writes are also treataed as reads for the purposes of resource tracking, this stops a texture from being discarded as a future write (Eg a 2nd pass to the same RT) would not be treated as a read otherwise, and would cause the texture to be discarded after the first pass
		// TODO: This might not be neccessary and might make culling passes not possible?
		target.lastReadIndex = index;
	}

	public void ExportResource(TextureHandle handle, RenderTargetIdentifier id)
	{
		var resourceIndex = resources.Count;
		resources.Add(id);

		ref var target = ref targets[handle];
		target.resourceIndex = resourceIndex;
		target.isExported = true;
	}

	public ViewHandle AddViewInfo(Int2 size, int samples = 1, int volumeDepth = 1)
	{
		var index = viewInfos.Count;
		viewInfos.Add(new(size, samples, volumeDepth));
		return new(index);
	}

	private void AllocateResource(CommandBuffer command, ref RenderTargetInfo target, ViewInfo viewInfo, bool isUav = false, int samples = 1)
	{
		target.resourceIndex = resources.Count;
		textureHandleSystem.GetTemporaryRT(command, target.propertyId, target.descriptor, viewInfo, samples, isUav);
		resources.Add(target.propertyId);
	}

	private void BeginNativeRenderPass(CommandBuffer command, int renderPassIndex, IRenderPass renderPass)
	{
		var nativePassDesc = nativeRenderPassSystem.GetDescriptor(renderPass.NativePassIndex);
		var viewInfo = viewInfos[renderPass.ViewHandle.index];

		// Resolve the attachments to their final values
		var attachments = new FixedBuffer<AttachmentDescriptor>(stackalloc AttachmentDescriptor[8]);
		foreach (var attachment in nativePassDesc.attachments)
		{
			ref var target = ref targets[attachment];
			var attachmentDesc = new AttachmentDescriptor
			{
				graphicsFormat = target.descriptor.format
			};

			// Load the target if it has been written to before this renderpass, otherwise clear it if required
			var isFirstWrite = target.firstWriteIndex >= renderPassIndex;
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
			var requiresMsaaStore = viewInfo.samples > 1 && (nativePassDesc.passEndIndex < target.lastWriteIndex || nativePassDesc.passEndIndex == target.lastWriteIndex) && !isColor;
			var requiresStore = target.lastReadIndex > nativePassDesc.passEndIndex || target.isExported;

			if (requiresResolve)
			{
				AllocateResource(command, ref target, viewInfo);
				attachmentDesc.resolveTarget = resources[target.resourceIndex];
				attachmentDesc.storeAction = RenderBufferStoreAction.Resolve;
			}
			else if (requiresMsaaStore)
			{
				// Depth targets can't be msaa resolved so we need to store the msaa version.
				if (isFirstWrite)
					AllocateResource(command, ref target, viewInfo, false, viewInfo.samples);

				attachmentDesc.loadStoreTarget = resources[target.resourceIndex];
			}
			else if (requiresStore)
			{
				// A store is required if the target is read outside of this nativePass, or it is exported
				if (!target.isExported && isFirstWrite)
					AllocateResource(command, ref target, viewInfo, false, 1);

				attachmentDesc.loadStoreTarget = resources[target.resourceIndex];
			}
			else
			{
				attachmentDesc.storeAction = RenderBufferStoreAction.DontCare;
			}

			_ = attachments.Add(attachmentDesc);
		}

		Span<byte> debugNameUtf8 = stackalloc byte[Encoding.UTF8.GetByteCount(nativePassDesc.debugName)];
		_ = Encoding.UTF8.GetBytes(nativePassDesc.debugName, debugNameUtf8);

		command.BeginRenderPass(viewInfo.size.x, viewInfo.size.y, 1, viewInfo.samples, attachments.Span.AsArray(), nativePassDesc.depthIndex, -1, nativePassDesc.subpasses, debugNameUtf8);
	}

	private void EndNativeRenderPass(CommandBuffer command, int lastNativePass, int passIndex)
	{
		command.EndRenderPass();

		// Free any resources from the previous pass if possible
		var nativePassDesc = nativeRenderPassSystem.GetDescriptor(lastNativePass);
		foreach (var attachment in nativePassDesc.attachments)
		{
			ref var target = ref targets[attachment];

			// Exported targets should never be released
			if (target.isExported)
				continue;

			// If the target needs to be read later, it can't be released yet
			if (target.lastReadIndex > passIndex)
				continue;

			// Don't release targets that were never assigned
			if (target.resourceIndex == -1)
				continue;

			textureHandleSystem.ReleaseTemporaryRT(command, target.propertyId);
			target.resourceIndex = -1;
		}
	}

	public void Execute(CommandBuffer command)
	{
		nativeRenderPassSystem.CloseIfNeeded(renderPasses.Count);

		var lastNativePass = -1;
		for (var i = 0; i < renderPasses.Count; i++)
		{
			var renderPass = renderPasses[i];
			if (renderPass.NativePassIndex != lastNativePass)
			{
				// End current pass if needed
				if (lastNativePass != -1)
				{
					EndNativeRenderPass(command, lastNativePass, i - 1);
					lastNativePass = -1;
				}

				if (renderPass.NativePassIndex > -1)
				{
					BeginNativeRenderPass(command, i, renderPass);
					lastNativePass = renderPass.NativePassIndex;
				}
			}
			else if (renderPass.IsNewSubPass)
				command.NextSubPass();

			// UAV resources are handled seperately so we need to write them here
			foreach (var input in handles[renderPass.UavResourceRange])
			{
				ref var target = ref targets[input];

				// If this is the first time it is written, we need to allocate a texture
				if (i == target.firstWriteIndex)
				{
					var viewInfo = viewInfos[renderPass.ViewHandle.index];
					AllocateResource(command, ref target, viewInfo, true, 1);
				}

				var resource = resources[target.resourceIndex];
				command.SetGlobalTexture(target.propertyId, resource);
			}

			// Set resources. Note this needs to happen after allocation, since we free any resources after this, and we don't want to accidentally free a resource that is being read
			foreach (var input in handles[renderPass.ResourceRange])
			{
				ref var target = ref targets[input];
				var resource = resources[target.resourceIndex];
				command.SetGlobalTexture(target.propertyId, resource);

				// If this is the last time a resource is read, it can be freed for the next pass
				if (i == target.lastReadIndex && !target.isExported)
				{
					textureHandleSystem.ReleaseTemporaryRT(command, target.propertyId);
					target.resourceIndex = -1;
				}
			}

			foreach (var keyword in renderPass.Keywords)
				command.EnableKeyword(keyword);

			command.BeginSample(renderPass.Name);
			renderPass.Execute(command);
			command.EndSample(renderPass.Name);

			foreach (var keyword in renderPass.Keywords)
				command.DisableKeyword(keyword);

			// Free any UAVs. This needs to be done after the pass, otherwise we might allocate and free a texture before the pass starts, allowing another UAV to be assigned to the same texture
			foreach (var input in handles[renderPass.UavResourceRange])
			{
				ref var target = ref targets[input];

				// If this is the last time a resource is read, it can be freed for the next pass
				if (i == target.lastReadIndex && !target.isExported)
				{
					textureHandleSystem.ReleaseTemporaryRT(command, target.propertyId);
					target.resourceIndex = -1;
				}
			}
		}

		if (lastNativePass != -1)
			EndNativeRenderPass(command, lastNativePass, renderPasses.Count - 1);

		textureHandleSystem.ReleaseRemainingTargets(command);

		FrameIndex++;
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
