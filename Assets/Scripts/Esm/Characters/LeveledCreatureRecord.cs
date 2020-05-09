using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Esm
{
	[Serializable]
	public class LeveledCreatureRecord : CreatableRecord
	{
		public int calculateFromAllLevelsLessThanPCsLevel;
		public byte? chanceNone;
		public int index;

		public List<string> creatureNames = new List<string>();
		public List<short> pcLevel = new List<short>();

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
					case SubRecordType.Data:
						calculateFromAllLevelsLessThanPCsLevel = reader.ReadInt32();
						break;
					case SubRecordType.NextName:
						chanceNone = reader.ReadByte();
						break;
					case SubRecordType.Index:
						index = reader.ReadInt32();
						break;
					case SubRecordType.CreatureName:
						creatureNames.Add(reader.ReadString(size));
						break;
					case SubRecordType.IntValue:
						pcLevel.Add(reader.ReadInt16());
						break;
				}
			}
		}

		public override GameObject CreateGameObject(ReferenceData referenceData, Transform parent = null)
		{
			var random = Random.Range(0, 100);

			if (chanceNone != null && random < chanceNone)
			{
				return null;
			}

			var index = Random.Range(0, creatureNames.Count);
			var id = creatureNames[index];
			var creatureRecord = Record.GetRecord<CreatureRecord>(id);
			return creatureRecord.CreateGameObject(referenceData);
		}
	}
}