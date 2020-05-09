using System;
using UnityEngine;

namespace Esm
{
	[Serializable]
	public class SpellData
	{
		[SerializeField]
		private SpellType spellType;

		[SerializeField]
		private int spellCost;

		[SerializeField, EnumFlags]
		private SpellFlags spellFlags;

		public SpellData(System.IO.BinaryReader reader)
		{
			spellType = (SpellType)reader.ReadInt32();
			spellCost = reader.ReadInt32();
			spellFlags = (SpellFlags)reader.ReadInt32();
		}

		public int SpellCost => spellCost;
		public SpellType SpellType => spellType;
		public SpellFlags SpellFlags => spellFlags;
	}
}