using System;
using UnityEngine;

namespace Esm
{
	[Serializable]
	public class ClassData
	{
		[SerializeField]
		private Specialization specialization;

		[SerializeField]
		private CharacterAttribute[] favouriteAttributes = new CharacterAttribute[2];

		[SerializeField]
		private CharacterSkill[] majorSkills = new CharacterSkill[5];

		[SerializeField]
		private CharacterSkill[] minorSkills = new CharacterSkill[5];

		[SerializeField]
		private bool isPlayable;

		[SerializeField, EnumFlags]
		private ClassAutoCalcFlags autoCalcFlags;

		public ClassData(System.IO.BinaryReader reader)
		{
			for (var i = 0; i < favouriteAttributes.Length; i++)
			{
				favouriteAttributes[i] = (CharacterAttribute)reader.ReadInt32();
			}

			specialization = (Specialization)reader.ReadInt32();

			for (var i = 0; i < minorSkills.Length; i++)
			{
				minorSkills[i] = (CharacterSkill)reader.ReadInt32();
				majorSkills[i] = (CharacterSkill)reader.ReadInt32();
			}

			isPlayable = reader.ReadInt32() == 1;
			autoCalcFlags = (ClassAutoCalcFlags)reader.ReadInt32();
		}
	}
}