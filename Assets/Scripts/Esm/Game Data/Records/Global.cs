using System;
using UnityEngine;

namespace Esm
{
	[Serializable]
	public class Global : EsmRecord
	{
		[SerializeField]
		private float value;

		[SerializeField]
		private GlobalType globalType;

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
					case SubRecordType.Name:
						globalType = (GlobalType)reader.ReadByte();
						break;
					case SubRecordType.FloatValue:
						value = reader.ReadSingle();
						break;
				}
			}
		}

		public GlobalType GlobalType => globalType;

		public float Value { get { return value; } set { this.value = value; } }
	}
}