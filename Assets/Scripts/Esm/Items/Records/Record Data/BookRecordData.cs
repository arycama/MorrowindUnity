using System;
using UnityEngine;

namespace Esm
{
	[Serializable]
	public class BookRecordData : ItemRecordData
	{
		[SerializeField]
		private int enchantPoints;

		[SerializeField]
		private BookFlags flags;

		[SerializeField]
		private CharacterSkill skillID;

		public BookRecordData(System.IO.BinaryReader reader)
		{
			weight = reader.ReadSingle();
			value = reader.ReadInt32();
			flags = (BookFlags)reader.ReadInt32();
			skillID = (CharacterSkill)reader.ReadInt32();
			enchantPoints = reader.ReadInt32();
		}
	}
}