using System;
using System.Text;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Unmath;

public struct NativeRenderPass<T> : IDisposable
{
	private readonly Int2 size;
	private readonly int samples;
	private readonly CommandBuffer command;
	private readonly string name;

	private readonly NativeList<AttachmentDescriptor> attachments;
	private readonly NativeList<SubPassDescriptor> subpasses;
	private readonly NativeList<int> colorOutputs;
	private int depthIndex;
	private Action<CommandBuffer, T> render;
	private T data;

	public NativeRenderPass(Int2 size, int samples, CommandBuffer command, string name, T data, Action<CommandBuffer, T> render)
	{
		this.size = size;
		this.samples = samples;
		this.command = command;
		this.name = name;
		this.data = data;
		this.render = render;

		depthIndex = -1;

		attachments = new(8, Allocator.Temp);
		subpasses = new(8, Allocator.Temp);
		colorOutputs = new(8, Allocator.Temp);
	}

	public void WriteAttachment(RenderTargetDescriptor descriptor, RenderTargetIdentifier? target = null, bool resolve = true)
	{
		var requiresResolve = target.HasValue && resolve && descriptor.samples > 1;

		attachments.Add(new AttachmentDescriptor
		{
			loadAction = descriptor.clear ? RenderBufferLoadAction.Clear : RenderBufferLoadAction.DontCare, // TODO: Support load, if contents have already been written to previously
			storeAction = target == null ? RenderBufferStoreAction.DontCare : (requiresResolve ? RenderBufferStoreAction.Resolve : RenderBufferStoreAction.Store), // TODO: Only store if result is read later
			graphicsFormat = descriptor.format,
			loadStoreTarget = target == null || requiresResolve ? BuiltinRenderTextureType.None : target.Value, // TODO: Only set if target is read later
			resolveTarget = requiresResolve ? target.Value : BuiltinRenderTextureType.None,
			clearColor = descriptor.clearColor,
			clearDepth = descriptor.clearDepth,
			clearStencil = descriptor.clearStencil
		});

		var index = attachments.Length - 1;

		bool isDepth = false;
		switch (descriptor.format)
		{
			case GraphicsFormat.D16_UNorm:
			case GraphicsFormat.D16_UNorm_S8_UInt:
			case GraphicsFormat.D24_UNorm:
			case GraphicsFormat.D24_UNorm_S8_UInt:
			case GraphicsFormat.D32_SFloat:
			case GraphicsFormat.D32_SFloat_S8_UInt:
				isDepth = true;
				break;
		}

		if (isDepth)
			depthIndex = index;
		else
			colorOutputs.Add(index);
	}

	void IDisposable.Dispose()
	{
		// End subpass
		subpasses.Add(new() { colorOutputs = new(colorOutputs.AsArray()) });
		colorOutputs.Clear();

		Span<byte> debugNameUtf8 = stackalloc byte[Encoding.UTF8.GetByteCount(name)];
		_ = Encoding.UTF8.GetBytes(name, debugNameUtf8);

		command.BeginRenderPass(size.x, size.y, samples, attachments.AsArray(), depthIndex, subpasses.AsArray(), debugNameUtf8);
		attachments.Clear();
		depthIndex = -1;
		subpasses.Clear();

		render(command, data);

		command.EndRenderPass();
	}
}
