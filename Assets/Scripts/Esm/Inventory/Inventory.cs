using System.Collections.Generic;
using UnityEngine;

namespace Esm
{
	public abstract class Inventory<T, K> : RecordBehaviour<T, K>, IInventory where T : Inventory<T, K> where K : EsmRecord, IInventoryRecord
	{
		private Dictionary<ItemRecord, int> items = new Dictionary<ItemRecord, int>();

		public IReadOnlyDictionary<ItemRecord, int> Items => items;

		protected override void Initialize(K record, ReferenceData referenceData)
		{
			base.Initialize(record, referenceData);
		
			foreach (var item in record.Items)
			{
				item.ItemData.AddToInventory(this, item.Quantity);
			}
		}

		public void AddItem(ItemRecord item, int quantity)
		{
			int currentQuantity = 0;
			items.TryGetValue(item, out currentQuantity);
			items[item] = currentQuantity + quantity;
		}

		public int GetItemQuantity(ItemRecord item)
		{
			int currentQuantity = 0;
			items.TryGetValue(item, out currentQuantity);
			return currentQuantity;
		}

		public void RemoveItem(ItemRecord item, int quantity)
		{
			if((items[item] -= quantity) < 1)
				items.Remove(item);
		}

		// Transfer item from this inventory to another
		public void TransferItem(ItemRecord item, int quantity, IInventory targetInventory)
		{
			RemoveItem(item, quantity);

			// Play the item sound
			item.PickupSound.PlaySound2D();

			// Transfer the item with the container's owner data
			targetInventory.AddItem(item, quantity);
		}
	}
}