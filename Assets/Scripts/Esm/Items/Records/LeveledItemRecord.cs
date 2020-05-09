using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Esm
{
	public class LeveledItemRecord : ItemRecord<LeveledItemRecordData>
	{
		[SerializeField]
		private int count;

		[SerializeField]
		private byte chanceNone;

		[SerializeField]
		private Tuple<ItemRecord, int>[] items;

		public override SoundRecord PickupSound => Record.GetRecord<SoundRecord>("Item Misc Up");
		public override SoundRecord DropSound => Record.GetRecord<SoundRecord>("Item Misc Down");

		public override void Initialize(System.IO.BinaryReader reader, RecordHeader header)
		{
			var index = 0;
			ItemRecord item = null;
			var itemChance = 0;

			while (reader.BaseStream.Position < header.DataEndPos)
			{
				var type = (SubRecordType)reader.ReadInt32();
				var size = reader.ReadInt32();

				switch (type)
				{
					case SubRecordType.Id:
						name = reader.ReadString(size);
						break;
					case SubRecordType.Data:
						data = new LeveledItemRecordData(reader);
						break;
					case SubRecordType.NextName:
						chanceNone = reader.ReadByte();
						break;
					case SubRecordType.Index:
						count = reader.ReadInt32();
						items = new Tuple<ItemRecord, int>[count];
						break;
					case SubRecordType.ItemName:
						item = Record.GetRecord<ItemRecord>(reader.ReadString(size));
						break;
					case SubRecordType.IntValue:
						itemChance = reader.ReadInt16();
						items[index] = new Tuple<ItemRecord, int>(item, itemChance);
						index++;
						break;
				}
			}
		}

		public override void AddToInventory(IInventory inventory, int quantity = 1)
		{
			if (data.Flags.HasFlag(LeveledItemFlags.CalculateForEachItem))
			{
				for (var i = 0; i < quantity; i++)
				{
					var random = Random.Range(0, 100);
					if (random < chanceNone)
					{
						continue;
					}

					var index = Random.Range(0, items.Length);
					var itemData = items[index].Item1;
					inventory.AddItem(itemData, 1);
				}
			}
			else
			{
				var random = Random.Range(0, 100);
				if (random < chanceNone)
				{
					return;
				}

				var index = Random.Range(0, items.Length);
				var itemData = items[index].Item1;
				inventory.AddItem(itemData, quantity);
			}
		}
	}
}