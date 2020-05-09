using System.Collections.Generic;
using Esm;

public interface IInventoryRecord
{
	IEnumerable<InventoryItem> Items { get; }
}