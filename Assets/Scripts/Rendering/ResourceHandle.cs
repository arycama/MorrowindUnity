using System;
using System.Diagnostics;

[DebuggerDisplay("{index}")]
public readonly struct ResourceHandle : IEquatable<ResourceHandle>
{
	public readonly int index;
	public readonly ResourceHandleType type;

	public ResourceHandle(int index, ResourceHandleType type)
	{
		this.index = index;
		this.type = type;
	}

	public override bool Equals(object obj)
	{
		return obj is ResourceHandle handle && Equals(handle);
	}

	public bool Equals(ResourceHandle other)
	{
		return index == other.index &&
			   type == other.type;
	}

	public override int GetHashCode()
	{
		return HashCode.Combine(index, type);
	}

	public static bool operator ==(ResourceHandle left, ResourceHandle right)
	{
		return left.Equals(right);
	}

	public static bool operator !=(ResourceHandle left, ResourceHandle right)
	{
		return !(left == right);
	}

	public static implicit operator int(ResourceHandle handle) => handle.index;
}
