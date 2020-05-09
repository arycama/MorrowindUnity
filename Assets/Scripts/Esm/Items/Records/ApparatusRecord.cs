using System;
using UnityEngine;

namespace Esm
{
	public class ApparatusRecord : ItemRecord<ApparatusRecordData>
	{
		public override SoundRecord PickupSound => Record.GetRecord<SoundRecord>("Item Apparatus Up");
		public override SoundRecord DropSound => Record.GetRecord<SoundRecord>("Item Apparatus Down");

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
					case SubRecordType.ApparatusData:
						data = new ApparatusRecordData(reader);
						break;
				}
			}
		}

		public override GameObject CreateGameObject(ReferenceData referenceData, Transform parent = null)
		{
			var gameObject = base.CreateGameObject(referenceData, parent);

			Apparatus.Create(gameObject, this, referenceData);

			return gameObject;
		}
	}
}