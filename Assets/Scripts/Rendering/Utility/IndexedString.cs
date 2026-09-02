using System.Collections.Generic;
using UnityEngine.Assertions;

public class IndexedString
{
	private readonly List<string> strings;
	private readonly string id;

	public string this[int index]
	{
		get
		{
			Assert.IsTrue(index >= 0);
			EnsureCapacity(index);
			return strings[index];
		}
	}

	public IndexedString(string id, int initialCapacity = 0)
	{
		this.id = id;
		strings = initialCapacity > 0 ? new(initialCapacity) : new();
		EnsureCapacity(initialCapacity);
	}

	private void EnsureCapacity(int index)
	{
		while (strings.Count <= index)
			strings.Add($"{id}{strings.Count}");
	}
}
