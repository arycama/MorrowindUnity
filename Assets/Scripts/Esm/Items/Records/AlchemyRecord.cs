using System;
using System.Collections.Generic;
using UnityEngine;

namespace Esm
{
	public class AlchemyRecord : ItemRecord<AlchemyRecordData>
	{
		[SerializeField]
		private EnchantmentEffect[] enchantments;

		public override SoundRecord PickupSound => Record.GetRecord<SoundRecord>("Item Potion Up");
		public override SoundRecord DropSound => Record.GetRecord<SoundRecord>("Item Potion Down");

		public override void Initialize(System.IO.BinaryReader reader, RecordHeader header)
		{
			var enchantments = new List<EnchantmentEffect>();

			while (reader.BaseStream.Position < header.DataEndPos)
			{
				var type = (SubRecordType)reader.ReadInt32();
				var size = reader.ReadInt32();

				switch (type)
				{
					case SubRecordType.Id:
						name = reader.ReadString(size);
						break;
					case SubRecordType.Model:
						model = reader.ReadString(size);
						break;
					case SubRecordType.Name:
						fullName = reader.ReadString(size);
						break;
					case SubRecordType.Script:
						script = Script.Get(reader.ReadString(size));
						break;
					case SubRecordType.Text:
						CreateSprite(reader.ReadString(size));
						break;
					case SubRecordType.AlchemyData:
						data = new AlchemyRecordData(reader);
						break;
					case SubRecordType.Enchantment:
						enchantments.Add(new EnchantmentEffect(reader));
						break;
				}
			}

			this.enchantments = enchantments.ToArray();
		}

		public override GameObject CreateGameObject(ReferenceData referenceData, Transform parent = null)
		{
			var gameObject = base.CreateGameObject(referenceData, parent);

			Alchemy.Create(gameObject, this, referenceData);

			return gameObject;
		}

		public override void UseItem(GameObject target)
		{
			Record.GetRecord<SoundRecord>("Drink").PlaySound2D();

			// Needs to somehow also remove itself from the inventory.
		}

		public override InfoPanel DisplayInfo(Vector3 position, int quantity, bool displayIcon = true)
		{
			var infoPanel = base.CreateInfo(position, quantity, displayIcon);
			data.DisplayInfo(infoPanel);

			foreach(var enchantment in enchantments)
			{
				enchantment.DisplayInfo(infoPanel);
			}

			return infoPanel;
		}
	}
}