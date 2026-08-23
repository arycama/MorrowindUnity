using System;

public ref struct FixedBuffer<T>
{
	private readonly Span<T> buffer;

	public FixedBuffer(Span<T> buffer)
	{
		this.buffer = buffer;
		Count = 0;
	}

	public int Count { get; private set; }
	public readonly Span<T> Span => buffer[..Count];

	public bool Add(T item)
	{
		if (Count >= buffer.Length) 
			return false;

		buffer[Count++] = item;
		return true;
	}

	public void Clear()
	{
		Count = 0;
	}
}
