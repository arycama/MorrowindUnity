using System;
using System.Collections.Generic;
using UnityEngine;

namespace Esm
{
	public class MiscItemRecord : ItemRecord<MiscItemRecordData>
	{
		public override SoundRecord PickupSound => Record.GetRecord<SoundRecord>(fullName.StartsWith("Gold") ? "Item Gold Up" : "Item Misc Up");
		public override SoundRecord DropSound => Record.GetRecord<SoundRecord>(fullName.StartsWith("Gold") ? "Item Gold Down" : "Item Misc Down");

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
					case SubRecordType.MiscItemData:
						data = new MiscItemRecordData(reader, fullName);
						break;
				}
			}
		}

		public override GameObject CreateGameObject(ReferenceData referenceData, Transform parent = null)
		{
			var gameObject = base.CreateGameObject(referenceData, parent);
			var ownerData = new OwnerData(referenceData.Owner, referenceData.Global, referenceData.Faction, referenceData.Rank);

			MiscItem.Create(gameObject, this, referenceData);

			return gameObject;
		}

		public InfoPanel DisplayInfo(Vector3 position, int quantity, bool displayIcon, CreatureRecord soul)
		{
			var infoPanel = base.CreateInfo(position, quantity, displayIcon);

			// Soul Gems
			if (soul != null)
			{
				infoPanel.AddTitle($"{fullName} ({soul})");
			}

			// Gold
			else if (fullName.StartsWith("Gold"))
			{
				var goldCountIndex = fullName.IndexOf("_");
				var goldCount = fullName.Substring(goldCountIndex);

				infoPanel.AddTitle($"Gold ({goldCount})");
			}
			else
			{
				infoPanel.AddTitle(fullName);
			}

			infoPanel.DisplayIcon(Icon);
			data.DisplayInfo(infoPanel);

			return infoPanel;
		}
	}
}