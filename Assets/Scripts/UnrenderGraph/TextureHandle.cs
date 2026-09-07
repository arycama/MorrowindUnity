using System;
using System.Diagnostics;

[DebuggerDisplay("{index}")]
public readonly struct TextureHandle : IEquatable<TextureHandle>
{
	public readonly int index;

	public TextureHandle(int index)
	{
		this.index = index;
	}

	public override bool Equals(object obj)
	{
		return obj is TextureHandle handle && Equals(handle);
	}

	public bool Equals(TextureHandle other)
	{
		return index == other.index;
	}

	public override int GetHashCode()
	{
		return HashCode.Combine(index);
	}

	public static bool operator ==(TextureHandle left, TextureHandle right)
	{
		return left.Equals(right);
	}

	public static bool operator !=(TextureHandle left, TextureHandle right)
	{
		return !(left == right);
	}

	public static implicit operator int(TextureHandle handle) => handle.index;

	public static implicit operator ResourceHandle(TextureHandle handle) => new(handle, ResourceHandleType.Texture);
}

