using System;
using UnityEngine;

namespace Esm
{
	public class LockpickRecord : ItemRecord<LockpickRecordData>
	{
		public override SoundRecord PickupSound { get { return Record.GetRecord<SoundRecord>("Item Lockpick Up"); } }

		public override SoundRecord DropSound { get { return Record.GetRecord<SoundRecord>("Item Lockpick Down"); } }

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
					case SubRecordType.LockpickData:
						data = new LockpickRecordData(reader);
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

			Lockpick.Create(gameObject, this, referenceData);

			return gameObject;
		}
	}
}