using System;
using UnityEngine;

namespace Esm
{
	public class BookRecord : ItemRecord<BookRecordData>
	{
		[SerializeField, TextArea(5, 20)]
		public string text;

		[SerializeField]
		private EnchantmentData enchantment;

		public override SoundRecord PickupSound => Record.GetRecord<SoundRecord>("Item Book Up");
		public override SoundRecord DropSound => Record.GetRecord<SoundRecord>("Item Book Down");

		public override void Initialize(System.IO.BinaryReader reader, RecordHeader header)
		{
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
					case SubRecordType.ItemTexture:
						CreateSprite(reader.ReadString(size));
						break;
					case SubRecordType.Enchantment:
						enchantment = Record.GetRecord<EnchantmentData>(reader.ReadString(size));
						break;
					case SubRecordType.BookData:
						data = new BookRecordData(reader);
						break;
					case SubRecordType.Text:
						text = reader.ReadString(size);
						break;
				}
			}
		}

		public override GameObject CreateGameObject(ReferenceData referenceData, Transform parent = null)
		{
			var gameObject = base.CreateGameObject(referenceData, parent);
			var ownerData = new OwnerData(referenceData.Owner, referenceData.Global, referenceData.Faction, referenceData.Rank);

			Book.Create(gameObject, this, referenceData);

			return gameObject;
		}

		public InfoPanel DisplayInfo(Vector3 position, int quantity, bool displayIcon, float charge)
		{
			var infoPanel = base.CreateInfo(position, quantity, displayIcon);
			enchantment?.DisplayInfo(infoPanel, charge);
			return infoPanel;
		}
	}
}