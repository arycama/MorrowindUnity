using System;

namespace Esm
{
	[Serializable]
	class SkillRecord : EsmRecordCollection<CharacterSkill, SkillRecord>
	{
		private CharacterSkill index;
		private string description;
		private SkillData skillData;

		public string Id { get; private set; }

		public override void Initialize(System.IO.BinaryReader reader, RecordHeader header)
		{
			while (reader.BaseStream.Position < header.DataEndPos)
			{
				var type = (SubRecordType)reader.ReadInt32();
				var size = reader.ReadInt32();

				switch (type)
				{
					case SubRecordType.Index:
						index = (CharacterSkill)reader.ReadInt32();
						break;
					case SubRecordType.SkillData:
						skillData = new SkillData(reader);
						break;
					case SubRecordType.Description:
						description = reader.ReadString(size);
						break;
				}
			}
		}

		[Serializable]
		private class SkillData
		{
			private readonly CharacterAttribute attribute;
			private readonly Specialization specialization;
			private readonly float[] useValue = new float[4];

			public SkillData(System.IO.BinaryReader reader)
			{
				attribute = (CharacterAttribute)reader.ReadInt32();
				specialization = (Specialization)reader.ReadInt32();
				for (var i = 0; i < useValue.Length; i++)
				{
					useValue[i] = reader.ReadSingle();
				}
			}
		}
	}
}