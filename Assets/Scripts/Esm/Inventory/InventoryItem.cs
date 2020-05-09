using System;
using UnityEngine;

namespace Esm
{
	// Simple grouping for an Item/Quantity pair, used in Containers, Npc Records and Creatures. (Actual inventories at runtime do not use this and are more complicated)
	[Serializable]
	public class InventoryItem
	{
		[SerializeField]
		private ItemRecord record;

		[SerializeField]
		private int count;

		public InventoryItem(System.IO.BinaryReader reader)
		{
			count = reader.ReadInt32();
			record = Record.GetRecord<ItemRecord>(reader.ReadString(32));
		}

		public InventoryItem(ItemRecord record, int count)
		{
			this.record = record;
			this.count = count;
		}

		public int Quantity => count;	
		public ItemRecord ItemData => record;
	}
}