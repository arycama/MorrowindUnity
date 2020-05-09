using System.Collections.Generic;

public interface IInventory
{
	IReadOnlyDictionary<ItemRecord, int> Items { get; }

	void AddItem(ItemRecord item, int quantity);
	void RemoveItem(ItemRecord item, int quantity);
	void TransferItem(ItemRecord item, int quantity, IInventory targetInventory);

	int GetItemQuantity(ItemRecord item);
}