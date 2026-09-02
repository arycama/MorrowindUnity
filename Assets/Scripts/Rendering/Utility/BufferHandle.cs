using System;
using System.Diagnostics;

[DebuggerDisplay("{index}")]
public readonly struct BufferHandle : IEquatable<BufferHandle>
{
	public readonly int index;

	public BufferHandle(int index)
	{
		this.index = index;
	}

	public override bool Equals(object obj)
	{
		return obj is BufferHandle handle && Equals(handle);
	}

	public bool Equals(BufferHandle other)
	{
		return index == other.index;
	}

	public override int GetHashCode()
	{
		return HashCode.Combine(index);
	}

	public static bool operator ==(BufferHandle left, BufferHandle right)
	{
		return left.Equals(right);
	}

	public static bool operator !=(BufferHandle left, BufferHandle right)
	{
		return !(left == right);
	}

	public static implicit operator int(BufferHandle handle) => handle.index;

	public static implicit operator ResourceHandle(BufferHandle handle) => new(handle, ResourceHandleType.Buffer);
}
