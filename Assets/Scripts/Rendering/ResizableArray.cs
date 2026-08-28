using System;

public class ResizableArray<T>
{
	private T[] items = new T[4];
	public int Count { get; private set; }

	public ref T this[int index] => ref items[index];
	public T[] this[Range range] => items[range];

	public void Add(T item)
	{
		if (Count == items.Length - 1)
			Array.Resize(ref items, items.Length * 2);
		items[Count++] = item;
	}

	public void Clear() => Count = 0;
}
