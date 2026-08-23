using System.Diagnostics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Unmath;

[DebuggerDisplay("{size} {samples}xAA {format}, clear: ({clear}, color: {clearColor}, depth: {clearDepth}, stencil {clearStencil})")]
public struct RenderTargetDescriptor
{
	public Int2 size;
	public GraphicsFormat format;
	public bool clear;
	public Color clearColor;
	public float clearDepth;
	public uint clearStencil;
	public int samples;

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

	public RenderTextureDescriptor GetDescriptor(bool dontResolve)
	{
		// Otherwise we need to create a new resource
		var descriptor = new RenderTextureDescriptor
		{
			width = size.x,
			height = size.y,
			volumeDepth = 1,
			msaaSamples = dontResolve ? samples : 1,
			mipCount = 1,
			dimension = TextureDimension.Tex2D,
			shadowSamplingMode = ShadowSamplingMode.None,
		};

		bool isDepth = false, isStencil = false;
		switch (format)
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
				descriptor.graphicsFormat = format;
				break;
		}

		if (isDepth)
			descriptor.depthStencilFormat = format;

		if (isStencil)
			descriptor.stencilFormat = GraphicsFormat.R8_UInt;

		if (dontResolve && samples > 1)
			descriptor.bindMS = true;

		return descriptor;
	}
}
