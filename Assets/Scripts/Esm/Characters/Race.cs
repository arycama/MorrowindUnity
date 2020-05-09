using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Esm
{
	[Serializable]
	public class Race : EsmRecord
	{
		[SerializeField]
		private string fullName;

		[SerializeField, TextArea(5, 20)]
		private string description;

		[SerializeField]
		private List<SpellRecord> powers = new List<SpellRecord>();

		[SerializeField]
		private RaceData raceData;

		public override void Initialize(BinaryReader reader, RecordHeader header)
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
					case SubRecordType.Name:
						fullName = reader.ReadString(size);
						break;
					case SubRecordType.RaceData:
						raceData = new RaceData(reader);
						break;
					case SubRecordType.NpcSpell:
						powers.Add(Record.GetRecord<SpellRecord>(reader.ReadString(size)));
						break;
					case SubRecordType.Description:
						description = reader.ReadString(size);
						break;
				}
			}
		}

		public void SetNpcData(Transform transform, bool isFemale)
		{
			raceData.SetNpcSize(transform, isFemale);
		}

		public bool IsBeastRace => raceData.IsBeastRace;
		public string Id => name;
		public string Name => fullName;
	}
}