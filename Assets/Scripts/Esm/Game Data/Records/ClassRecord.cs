using System;
using UnityEngine;

namespace Esm
{
	[Serializable]
	public class ClassRecord : EsmRecord
	{
		[SerializeField]
		private string fullName;

		[SerializeField, TextArea(5, 20)]
		private string description;

		[SerializeField]
		private ClassData classData;

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
						fullName = reader.ReadString(size);
						break;
					case SubRecordType.ClassData:
						classData = new ClassData(reader);
						break;
					case SubRecordType.Description:
						description = reader.ReadString(size);
						break;
				}
			}
		}

		public string Name => fullName;
	}
}