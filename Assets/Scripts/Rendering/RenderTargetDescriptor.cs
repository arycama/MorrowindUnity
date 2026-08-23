using System.Diagnostics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
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
}
