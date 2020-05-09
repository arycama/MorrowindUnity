using System;
using UnityEngine;

namespace Esm
{
	[Serializable]
	public class RaceData
	{
		[SerializeField]
		private float femaleHeight;

		[SerializeField]
		private float femaleWeight;

		[SerializeField]
		private float maleHeight;

		[SerializeField]
		private float maleWeight;

		[SerializeField, EnumFlags]
		private RaceFlags flags;

		[SerializeField]
		private Tuple<CharacterSkill, int>[] skillBonuses = new Tuple<CharacterSkill, int>[7];

		[SerializeField]
		private Tuple<int, int>[] attributes = new Tuple<int, int>[8];

		public RaceData(System.IO.BinaryReader reader)
		{
			for (var i = 0; i < 7; i++)
			{
				var skill = (CharacterSkill)reader.ReadInt32();
				var bonus = reader.ReadInt32();
				skillBonuses[i] = new Tuple<CharacterSkill, int>(skill, bonus);
			}

			for (var i = 0; i < attributes.Length; i++)
			{
				var maleAttribute = reader.ReadInt32();
				var femaleAttribute = reader.ReadInt32();
				attributes[i] = new Tuple<int, int>(maleAttribute, femaleAttribute);
			}

			maleHeight = reader.ReadSingle();
			femaleHeight = reader.ReadSingle();
			maleWeight = reader.ReadSingle();
			femaleWeight = reader.ReadSingle();

			flags = (RaceFlags)reader.ReadInt32();
		}

		public bool IsBeastRace => flags.HasFlag(RaceFlags.BeastRace);

		public void SetNpcSize(Transform transform, bool isFemale)
		{
			if (isFemale)
			{
				transform.localScale = new Vector3(femaleWeight, femaleHeight, femaleWeight);
			}
			else
			{
				transform.localScale = new Vector3(maleWeight, maleHeight, maleWeight);
			}
		}
	}
}