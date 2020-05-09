using System.Collections.Generic;
using System.IO;
using UnityEngine;

class BirthSignRecord : EsmRecordCollection<BirthSignRecord>
{
	[SerializeField]
	private string description;

	[SerializeField]
	private string fullName;

	[SerializeField]
	private string texture;

	[SerializeField]
	private List<string> spells = new List<string>();

	public override void Initialize(BinaryReader reader, RecordHeader header)
	{
		while (reader.BaseStream.Position < header.DataEndPos)
		{
			var type = (SubRecordType)reader.ReadInt32();
			var size = reader.ReadInt32();

			switch (type)
			{
				case SubRecordType.Description:
					description = reader.ReadString(size);
					break;
				case SubRecordType.Id:
					name = reader.ReadString(size);
					break;
				case SubRecordType.Name:
					fullName = reader.ReadString(size);
					break;
				case SubRecordType.NpcSpell:
					spells.Add(reader.ReadString(size));
					break;
				case SubRecordType.Texture:
					texture = reader.ReadString(size);
					break;
			}
		}
	}
}