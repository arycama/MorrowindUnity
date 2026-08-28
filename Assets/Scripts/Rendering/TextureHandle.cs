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

	public static implicit operator int(TextureHandle handle) => handle.index;

	public static bool operator ==(TextureHandle left, TextureHandle right)
	{
		return left.index == right.index;
	}

	public static bool operator !=(TextureHandle left, TextureHandle right)
	{
		return left.index != right.index;
	}

	public override bool Equals(object obj)
	{
		return obj is TextureHandle other && Equals(other);
	}

	public override int GetHashCode()
	{
		return index;
	}

	bool IEquatable<TextureHandle>.Equals(TextureHandle other)
	{
		return index == other.index;
	}
}