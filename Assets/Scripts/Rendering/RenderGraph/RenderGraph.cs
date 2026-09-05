using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Unmath;

public class RenderGraph : IDisposable
{
	private readonly List<IRenderPass> renderPasses = new();
	private readonly List<ViewInfo> viewInfos = new();
	private readonly ResizableArray<ResourceInfo> resourceInfo = new();
	private readonly ResizableArray<ResourceHandle> handles = new();
	private readonly RenderTargetSystem renderTargetSystem = new();
	private readonly BufferSystem bufferSystem = new();
	private readonly List<RayTracingAccelerationStructure> rayTracingAccelerationStructures = new();
	private readonly List<Texture> textures = new();
	private readonly NativeRenderPassSystem nativeRenderPassSystem = new();
	private readonly ResourceMap resourceMap = new();
	private readonly PassBuilder passBuilder;
	private readonly ConstantBufferBuilder constantBufferBuilder;
	private readonly ResizableArray<byte> constantBufferData = new();
	public int FrameIndex { get; private set; }

	public RenderGraph()
	{
		passBuilder = new(this);
		constantBufferBuilder = new(this);
	}

	public void Dispose()
	{
		nativeRenderPassSystem.Dispose();
		bufferSystem.Dispose();
	}

	public void BeginCamera()
	{
		resourceMap.Clear();
	}

	public RenderTargetHandle GetTexture(RenderTargetDescriptor descriptor, int propertyId)
	{
		var descriptorIndex = renderTargetSystem.AddDescriptor(descriptor);
		resourceInfo.Add(new(descriptorIndex, propertyId, ResourceHandleType.RenderTarget));
		return new(resourceInfo.Count - 1);
	}

	public BufferHandle GetBuffer(BufferDescriptor descriptor, int propertyId)
	{
		var index = bufferSystem.AddDescriptor(descriptor);
		resourceInfo.Add(new(index, propertyId, ResourceHandleType.Buffer));
		return new(resourceInfo.Count - 1);
	}

	public RenderTargetIdentifier GetTextureResource(RenderTargetHandle handle)
	{
		var target = resourceInfo[handle];
		return renderTargetSystem.GetTexture(target.resourceIndex);
	}

	public GraphicsBuffer GetBufferResource(BufferHandle handle)
	{
		var target = resourceInfo[handle];
		return bufferSystem.GetBuffer(target.resourceIndex);
	}

	public void SetResource<T>(T resource) where T : IRenderResource
	{
		resourceMap.SetResource(resource);
	}

	public T GetResource<T>()
	{
		return resourceMap.GetResource<T>();
	}

	public bool TryGetResource(Type type, out IRenderResource resource) => resourceMap.TryGetResource(type, out resource);

	public bool TryGetResource<T>(out T resource)
	{
		var hasResource = TryGetResource(typeof(T), out var temp);
		resource = hasResource ? (T)temp : default;
		return hasResource;
	}

	public PassBuilder AddRenderPass(string name)
	{
		passBuilder.Name = name;
		passBuilder.Index = renderPasses.Count;
		return passBuilder;
	}

	public ViewInfo GetViewInfo(ViewHandle handle) => viewInfos[handle.index];

	public void SetRenderPass(PassBuilder builder)
	{
		var inputStart = handles.Count;
		foreach (var resource in builder.Resources)
		{
			resourceInfo[resource].lastReadIndex = builder.Index;
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
		foreach (var handle in builder.UavOutputs)
		{
			handles.Add(handle);
			SetResourceWriteIndex(handle, builder.Index);
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

	public bool IsResourceWritten(ResourceHandle resource)
	{
		return resourceInfo[resource].lastWriteIndex != -1;
	}

	private void SetResourceWriteIndex(ResourceHandle handle, int index)
	{
		ref var target = ref resourceInfo[handle];

		// Track the first pass this target is written to so we know when to clear. This also allows allocation to be skipped for textures that are never written to
		if (target.firstWriteIndex == -1)
			target.firstWriteIndex = index;

		// We also track the last write index so that we know when to resolve if msaa is enabled
		target.lastWriteIndex = index;

		// Writes are also treataed as reads for the purposes of resource tracking, this stops a texture from being discarded as a future write (Eg a 2nd pass to the same RT) would not be treated as a read otherwise, and would cause the texture to be discarded after the first pass
		// TODO: This might not be neccessary and might make culling passes not possible?
		target.lastReadIndex = index;
	}

	public void ExportTexture(RenderTargetHandle handle, RenderTargetIdentifier id)
	{
		var resourceIndex = renderTargetSystem.ExportTarget(id);
		ref var target = ref resourceInfo[handle];
		target.resourceIndex = resourceIndex;
		target.isExternal = true;
	}

	public TextureHandle GetTextureHandle(Texture texture, int propertyId)
	{
		resourceInfo.Add(new(-1, propertyId, ResourceHandleType.Texture));
		var handle = new TextureHandle(resourceInfo.Count - 1);
		var resourceIndex = textures.Count;
		ref var target = ref resourceInfo[handle];
		target.resourceIndex = resourceIndex;
		target.isExternal = true;
		textures.Add(texture);
		return handle;
	}

	public RayTracingAccelerationStructureHandle GetRtasHandle(RayTracingAccelerationStructure structure, int propertyId)
	{
		resourceInfo.Add(new(-1, propertyId, ResourceHandleType.RayTracingAccelerationStructure));
		var handle = new RayTracingAccelerationStructureHandle(resourceInfo.Count - 1);
		var resourceIndex = rayTracingAccelerationStructures.Count;
		ref var target = ref resourceInfo[handle];
		target.resourceIndex = resourceIndex;
		target.isExternal = true;
		rayTracingAccelerationStructures.Add(structure);
		return handle;
	}

	public ViewHandle AddViewInfo(Int2 size, int samples = 1, int volumeDepth = 1)
	{
		var index = viewInfos.Count;
		viewInfos.Add(new(size, samples, volumeDepth));
		return new(index);
	}

	private void AllocateTexture(RenderTargetHandle handle, ViewHandle viewHandle, bool isUav = false, int samples = 1)
	{
		ref var target = ref resourceInfo[handle];
		target.resourceIndex = renderTargetSystem.AllocateTarget(handle, target.descriptorIndex, viewInfos[viewHandle.index], samples, isUav);
	}

	private void AllocateBuffer(BufferHandle handle)
	{
		ref var target = ref resourceInfo[handle];
		target.resourceIndex = bufferSystem.AllocateBuffer(handle, target.descriptorIndex);
	}

	private void BeginNativeRenderPass(CommandBuffer command, int renderPassIndex, IRenderPass renderPass)
	{
		var nativePassDesc = nativeRenderPassSystem.GetDescriptor(renderPass.NativePassIndex);
		var viewHandle = renderPass.ViewHandle;
		var viewInfo = viewInfos[renderPass.ViewHandle.index];

		// Resolve the attachments to their final values
		var attachments = new FixedBuffer<AttachmentDescriptor>(stackalloc AttachmentDescriptor[8]);
		foreach (var texture in nativePassDesc.attachments)
		{
			ref var target = ref resourceInfo[texture];
			var descriptor = renderTargetSystem.GetDescriptor(target.descriptorIndex);
			var attachmentDesc = new AttachmentDescriptor
			{
				graphicsFormat = descriptor.format,
			};

			// Load the target if it has been written to before this renderpass, otherwise clear it if required
			var isFirstWrite = target.firstWriteIndex >= renderPassIndex;
			if (isFirstWrite)
			{
				if (descriptor.clear)
				{
					attachmentDesc.loadAction = RenderBufferLoadAction.Clear;
					attachmentDesc.clearColor = descriptor.clearColor;
					attachmentDesc.clearDepth = descriptor.clearDepth;
					attachmentDesc.clearStencil = descriptor.clearStencil;
				}
				else
					attachmentDesc.loadAction = RenderBufferLoadAction.DontCare;
			}
			else
			{
				// If this target has been written previously, it must be loaded
				attachmentDesc.loadStoreTarget = new(renderTargetSystem.GetTexture(target.resourceIndex), 0, CubemapFace.Unknown, nativePassDesc.depthSlice);
			}

			var isColor = descriptor.format switch
			{
				GraphicsFormat.D16_UNorm or GraphicsFormat.D24_UNorm or GraphicsFormat.D32_SFloat or GraphicsFormat.D16_UNorm_S8_UInt or GraphicsFormat.D24_UNorm_S8_UInt or GraphicsFormat.D32_SFloat_S8_UInt or GraphicsFormat.S8_UInt => false,
				_ => true,
			};

			// If this is the last pass, it needs to be resolved
			var requiresResolve = viewInfo.samples > 1 && nativePassDesc.passEndIndex == target.lastWriteIndex && isColor;
			var requiresMsaaStore = viewInfo.samples > 1 && (nativePassDesc.passEndIndex < target.lastWriteIndex || nativePassDesc.passEndIndex == target.lastWriteIndex) && !isColor;
			var requiresStore = target.lastReadIndex > nativePassDesc.passEndIndex || target.isExternal;

			if (requiresResolve)
			{
				AllocateTexture(texture, viewHandle);
				attachmentDesc.resolveTarget = new(renderTargetSystem.GetTexture(target.resourceIndex), 0, CubemapFace.Unknown, nativePassDesc.depthSlice);
				attachmentDesc.storeAction = RenderBufferStoreAction.Resolve;
			}
			else if (requiresMsaaStore)
			{
				// Depth targets can't be msaa resolved so we need to store the msaa version.
				if (isFirstWrite)
					AllocateTexture(texture, viewHandle, false, viewInfo.samples);

				attachmentDesc.loadStoreTarget = new(renderTargetSystem.GetTexture(target.resourceIndex), 0, CubemapFace.Unknown, nativePassDesc.depthSlice);
			}
			else if (requiresStore)
			{
				// A store is required if the target is read outside of this nativePass, or it is exported
				if (!target.isExternal && isFirstWrite)
					AllocateTexture(texture, viewHandle, false, 1);

				attachmentDesc.loadStoreTarget = new(renderTargetSystem.GetTexture(target.resourceIndex), 0, CubemapFace.Unknown, nativePassDesc.depthSlice);
			}
			else
			{
				attachmentDesc.storeAction = RenderBufferStoreAction.DontCare;
			}

			_ = attachments.Add(attachmentDesc);
		}

		Span<byte> debugNameUtf8 = stackalloc byte[Encoding.UTF8.GetByteCount(nativePassDesc.debugName)];
		_ = Encoding.UTF8.GetBytes(nativePassDesc.debugName, debugNameUtf8);

		var subPasses = nativeRenderPassSystem.GetSubPassDescriptors(nativePassDesc.subpasses);
		command.BeginRenderPass(viewInfo.size.x, viewInfo.size.y, nativePassDesc.volumeDepth, viewInfo.samples, attachments.Span.AsArray(), nativePassDesc.depthIndex, -1, subPasses.AsArray(), debugNameUtf8);
	}

	private void EndNativeRenderPass(CommandBuffer command, int lastNativePass, int passIndex)
	{
		command.EndRenderPass();

		// Free any resources from the previous pass if possible
		var nativePassDesc = nativeRenderPassSystem.GetDescriptor(lastNativePass);
		foreach (var attachment in nativePassDesc.attachments)
		{
			ref var target = ref resourceInfo[attachment];

			// Exported targets should never be released
			if (target.isExternal)
				continue;

			// If the target needs to be read later, it can't be released yet
			if (target.lastReadIndex > passIndex)
				continue;

			// Don't release targets that were never assigned
			if (target.resourceIndex == -1)
				continue;

			renderTargetSystem.ReleaseResource(attachment);
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
			foreach (var handle in handles[renderPass.UavResourceRange])
			{
				ref var target = ref resourceInfo[handle];

				// If this is the first time it is written, we need to allocate a texture
				if (i == target.firstWriteIndex && !target.isExternal)
				{
					if (handle.type == ResourceHandleType.RenderTarget)
					{
						var descriptor = renderTargetSystem.GetDescriptor(target.descriptorIndex);
						AllocateTexture(new(handle.index), descriptor.viewHandle, true, 1);
					}

					if (handle.type == ResourceHandleType.Buffer)
						AllocateBuffer(new(handle.index));
				}

				if (handle.type == ResourceHandleType.RenderTarget)
				{
					var resource = renderTargetSystem.GetTexture(target.resourceIndex);
					command.SetGlobalTexture(target.propertyId, resource);
				}

				if (handle.type == ResourceHandleType.Buffer)
				{
					var resource = bufferSystem.GetBuffer(target.resourceIndex);

					// Constant buffers are only ever set as uav write for the purposes of having their data set, so don't need to actually be set.
					if (resource.target != GraphicsBuffer.Target.Constant)
						command.SetGlobalBuffer(target.propertyId, resource);
				}
			}

			// Set resources. Note this needs to happen after allocation, since we free any resources after this, and we don't want to accidentally free a resource that is being read
			foreach (var handle in handles[renderPass.ResourceRange])
			{
				ref var target = ref resourceInfo[handle];

				if (handle.type == ResourceHandleType.RenderTarget)
				{
					var resource = renderTargetSystem.GetTexture(target.resourceIndex);
					command.SetGlobalTexture(target.propertyId, resource);
				}

				if (handle.type == ResourceHandleType.Buffer)
				{
					var resource = bufferSystem.GetBuffer(target.resourceIndex);

					if (resource.target == GraphicsBuffer.Target.Constant)
						command.SetGlobalConstantBuffer(resource, target.propertyId, 0, resource.stride);
					else
						command.SetGlobalBuffer(target.propertyId, resource);
				}

				if (handle.type == ResourceHandleType.Texture)
				{
					var resource = textures[target.resourceIndex];
					command.SetGlobalTexture(target.propertyId, resource);
				}

				if (handle.type == ResourceHandleType.RayTracingAccelerationStructure)
				{
					var resource = rayTracingAccelerationStructures[target.resourceIndex];
					command.SetGlobalRayTracingAccelerationStructure(target.propertyId, resource);
				}

				// If this is the last time a resource is read, it can be freed for the next pass
				if (i == target.lastReadIndex && !target.isExternal)
				{
					if (handle.type == ResourceHandleType.RenderTarget)
						renderTargetSystem.ReleaseResource(new(handle.index));

					if (handle.type == ResourceHandleType.Buffer)
						bufferSystem.ReleaseResource(new(handle.index));

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

			// TODO: Can this be done in the above loop
			// Free any UAVs. This needs to be done after the pass, otherwise we might allocate and free a texture before the pass starts, allowing another UAV to be assigned to the same texture
			foreach (var handle in handles[renderPass.UavResourceRange])
			{
				ref var target = ref resourceInfo[handle];

				// If this is the last time a resource is read, it can be freed for the next pass
				if (i == target.lastReadIndex && !target.isExternal)
				{
					if (handle.type == ResourceHandleType.RenderTarget)
						renderTargetSystem.ReleaseResource(new(handle.index));

					if (handle.type == ResourceHandleType.Buffer)
						bufferSystem.ReleaseResource(new(handle.index));

					target.resourceIndex = -1;
				}
			}
		}

		if (lastNativePass != -1)
			EndNativeRenderPass(command, lastNativePass, renderPasses.Count - 1);

		FrameIndex++;
	}

	public void Clear()
	{
		resourceInfo.Clear();
		renderPasses.Clear();
		viewInfos.Clear();
		nativeRenderPassSystem.Clear();
		handles.Clear();
		resourceMap.Clear();
		rayTracingAccelerationStructures.Clear();
		textures.Clear();
		constantBufferData.Clear();
		renderTargetSystem.FreeUnreleasedResources();
		bufferSystem.FreeUnreleasedResources();
	}

	public Range AddConstantBufferData(ReadOnlySpan<byte> data)
	{
		return constantBufferData.AddRange(data);
	}

	public Span<byte> GetConstantBufferData(Range range)
	{
		return constantBufferData.AsSpan(range);
	}

	public ConstantBufferBuilder AddConstantBuffer(string name, out BufferHandle handle)
	{
		// Constant buffer gets built inside a using statement and then the actual descriptor is created after. So
		// a handle that indicates the next available index is returned so that it will point to the correct data once the builder has completed
		handle = new(resourceInfo.Count);
		constantBufferBuilder.PropertyName = name;
		return constantBufferBuilder;
	}
}