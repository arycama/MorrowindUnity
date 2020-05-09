using System;
using UnityEngine;

namespace Esm
{
	public class RepairItemRecord : ItemRecord<RepairItemRecordData>
	{
		public override SoundRecord PickupSound => Record.GetRecord<SoundRecord>("Item Repair Up");
		public override SoundRecord DropSound => Record.GetRecord<SoundRecord>("Item Repair Down");

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
					case SubRecordType.RepairItemData:
						data = new RepairItemRecordData(reader);
						break;
				}
			}
		}

		public InfoPanel DisplayInfo(Vector3 position, int quantity, bool displayIcon, int uses)
		{
			var infoPanel = base.CreateInfo(position, quantity, displayIcon);
			data.DisplayInfo(infoPanel, uses);
			return infoPanel;
		}

		public override GameObject CreateGameObject(ReferenceData referenceData, Transform parent = null)
		{
			var gameObject = base.CreateGameObject(referenceData, parent);
			var ownerData = new OwnerData(referenceData.Owner, referenceData.Global, referenceData.Faction, referenceData.Rank);
			var uses = referenceData.Health == -1 ? data.MaxUses : referenceData.Health;

			RepairItem.Create(gameObject, this, referenceData);

			return gameObject;
		}
	}
}