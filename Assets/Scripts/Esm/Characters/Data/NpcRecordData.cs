using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Esm
{
	[Serializable]
	public class NpcRecordData
	{
		[SerializeField]
		private short level;

		[SerializeField]
		private byte[] attributes;

		[SerializeField]
		private byte[] skills;

		[SerializeField]
		private byte reputation;

		[SerializeField]
		private DerivedAttributeData derivedAttributeData;

		[SerializeField]
		private byte disposition;

		[SerializeField]
		private byte factionID;

		[SerializeField]
		private byte rank;

		[SerializeField]
		private int gold;

		[SerializeField]
		private byte unknown1;

		[SerializeField]
		private byte unknown2;
			
		[SerializeField]
		private byte unknown3;

		public NpcRecordData(BinaryReader reader, int size)
		{
			if (size == 52)
			{
				level = reader.ReadInt16();
				attributes = reader.ReadBytes(8);
				skills = reader.ReadBytes(27);
				reputation = reader.ReadByte();
				derivedAttributeData = new DerivedAttributeData(reader);
				disposition = reader.ReadByte();
				factionID = reader.ReadByte();
				rank = reader.ReadByte();
				unknown1 = reader.ReadByte();
				gold = reader.ReadInt32();

				unknown2 = 0;
				unknown3 = 0;
			}
			else
			{
				level = reader.ReadInt16();
				disposition = reader.ReadByte();
				factionID = reader.ReadByte();
				rank = reader.ReadByte();
				unknown1 = reader.ReadByte();
				unknown2 = reader.ReadByte();
				unknown3 = reader.ReadByte();
				gold = reader.ReadInt32();

				derivedAttributeData = new DerivedAttributeData();

				skills = new byte[27];
				attributes = new byte[8];
				reputation = 0;

				// Should auto-calc attributes here
				//attributes = new Attributes();
				//skills = classId.ClassData.CalculateSkills();
			}
		}

		public byte Reputation => reputation;
		public byte Rank => rank;
		public byte Disposition => disposition;
		public short Level => level;
		public DerivedAttributeData DerivedAttributeData => derivedAttributeData;

		public IReadOnlyList<byte> Attributes => attributes;
		public IReadOnlyList<byte> Skills => skills;

		public byte GetAttribute(CharacterAttribute attribute) => attributes[(int)attribute];

		public byte GetSkill(CharacterSkill skill) => skills[(int)skill];
	}
}