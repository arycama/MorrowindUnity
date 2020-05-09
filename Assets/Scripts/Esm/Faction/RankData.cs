using System;
using UnityEngine;

namespace Esm
{
	[Serializable]
	public class RankData
	{
		[SerializeField]
		private int[] attributeRequirements = new int[2];

		[SerializeField]
		private int primarySkillRequirement;

		[SerializeField]
		private int favouredSkillsRequirements;

		[SerializeField]
		private int reputationRequirement;

		public RankData(System.IO.BinaryReader reader)
		{
			for (var i = 0; i < attributeRequirements.Length; i++)
			{
				attributeRequirements[i] = reader.ReadInt32();
			}

			primarySkillRequirement = reader.ReadInt32();
			favouredSkillsRequirements = reader.ReadInt32();
			reputationRequirement = reader.ReadInt32();
		}

		public int[] Attributes => attributeRequirements;
		public int PrimarySkill => primarySkillRequirement;
		public int FavouredSkills => favouredSkillsRequirements;
		public int ReputationRequirement => reputationRequirement;
	}
}