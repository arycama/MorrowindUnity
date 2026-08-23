using System.Diagnostics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Unmath;

[DebuggerDisplay("{size} {samples}xAA {format}, clear: ({clear}, color: {clearColor}, depth: {clearDepth}, stencil {clearStencil})")]
public readonly struct RenderTargetDescriptor
{
	public readonly Int2 size;
	public readonly GraphicsFormat format;
	public readonly bool clear;
	public readonly Color clearColor;
	public readonly float clearDepth;
	public readonly uint clearStencil;
	public readonly int samples;

	public RenderTargetDescriptor(Int2 size, GraphicsFormat format, int samples = 1, bool clear = false, Color clearColor = default, float clearDepth = 1f, uint clearStencil = default)
	{
		this.size = size;
		this.format = format;
		this.clear = clear;
		this.clearColor = clearColor;
		this.clearDepth = clearDepth;
		this.clearStencil = clearStencil;
		this.samples = samples;
	}

	public static implicit operator RenderTextureDescriptor(RenderTargetDescriptor desc)
	{
		// Otherwise we need to create a new resource
		var descriptor = new RenderTextureDescriptor
		{
			width = desc.size.x,
			height = desc.size.y,
			volumeDepth = 1,
			mipCount = 1,
			dimension = TextureDimension.Tex2D,
			shadowSamplingMode = ShadowSamplingMode.None,
		};

		bool isDepth = false, isStencil = false;
		switch (desc.format)
		{
			case GraphicsFormat.D16_UNorm:
			case GraphicsFormat.D24_UNorm:
			case GraphicsFormat.D32_SFloat:
				isDepth = true;
				break;
			case GraphicsFormat.D16_UNorm_S8_UInt:
			case GraphicsFormat.D24_UNorm_S8_UInt:
			case GraphicsFormat.D32_SFloat_S8_UInt:
				isDepth = true;
				isStencil = true;
				break;
			case GraphicsFormat.S8_UInt:
				isStencil = true;
				break;
			default:
				descriptor.graphicsFormat = desc.format;
				break;
		}

		if (isDepth)
			descriptor.depthStencilFormat = desc.format;

		if (isStencil)
			descriptor.stencilFormat = GraphicsFormat.R8_UInt;

		if (desc.samples > 1 && (isDepth || isStencil))
		{
			// Resolve not supported 
			descriptor.msaaSamples = desc.samples;
			descriptor.bindMS = true;
		}
		else
		{
			descriptor.msaaSamples = 1;
		}

		return descriptor;
	}
}
