using System;
using Math = Unmath.Math;

public class ResizableArray<T>
{
	private T[] items = new T[4];
	public int Count { get; private set; }

	public ref T this[int index] => ref items[index];
	public T[] this[Range range] => items[range];

	public void Clear() => Count = 0;

	public Span<T> AsSpan(Range range) => items.AsSpan(range);
	public Span<T> AsSpan() => AsSpan(..Count);

	private void EnsureCapacity(int newCapacity)
	{
		if (newCapacity < this.items.Length)
			return;

		var newSize = Math.NextPowerOfTwo(newCapacity);
		var items = this.items;
		Array.Resize(ref items, newSize);
		this.items = items;
	}

	public void Add(T item)
	{
		EnsureCapacity(Count + 1);
		items[Count++] = item;
	}

	public Range AddRange(ReadOnlySpan<T> span)
	{
		var newCount = Count + span.Length;
		EnsureCapacity(newCount);
		var range = Count..newCount;
		span.CopyTo(items.AsSpan(range));
		Count = newCount;
		return range;
	}
}
