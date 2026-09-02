using System.Diagnostics;
using UnityEngine;

[DebuggerDisplay("count({count}) stride({stride}) target({target}) usageFlags({usageFlags})")]
public readonly struct BufferDescriptor
{
	public readonly int count;
	public readonly int stride;
	public readonly GraphicsBuffer.Target target;
	public readonly GraphicsBuffer.UsageFlags usageFlags;

	public BufferDescriptor(int count = 1, int stride = 4, GraphicsBuffer.Target target = GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags usageFlags = GraphicsBuffer.UsageFlags.None)
	{
		this.count = count;
		this.stride = stride;
		this.target = target;
		this.usageFlags = usageFlags;
	}
}