using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Esm
{
	[Serializable]
	public class Faction : EsmRecord
	{
		// Used to store factions during initialization so stack overflows don't occur
		private static readonly Dictionary<string, Faction> factionDictionary = new Dictionary<string, Faction>();

		[SerializeField]
		private string fullName;

		[SerializeField]
		public FactionData factionData;

		[SerializeField]
		public List<string> ranks = new List<string>();

		[SerializeField]
		public List<FactionReaction> reactions = new List<FactionReaction>();

		public string Name => fullName;

		public int GetReaction(Faction faction)
		{
			var result = reactions.FirstOrDefault((f) => f.Faction == faction);
			if(result != null)
				return result.Reaction;
			else
				return 0;
		}

		// Checks if a character meets the requirements to advance to the next rank
		public int CheckRankRequirements(Character character)
		{
			// Should apparently return 0 if nothing met, 1 if requirements are met, but not reuptation?, 2 if reuptation, but not requirements, 3 if qualify
			int reputation;
			var rank = character.GetFactionRank(this, out reputation);
			var rankData = factionData.rankData[rank];

			// Do faction reputation check first, as it's the simplest
			bool meetsReputationRequirement = false;
			if (reputation >= rankData.ReputationRequirement)
				meetsReputationRequirement = true;

			// Check attributes
			var meetsSkillAndAttributeRequirements = true;
			for (var i = 0; i < factionData.attributes.Length; i++)
			{
				if (character.GetAttribute(factionData.attributes[i]) < rankData.Attributes[i])
				{
					meetsSkillAndAttributeRequirements = false;
					break;
				}
			}

			// Should skip below if above is not met (We don't need to check skills if attributes fail

			// Check all skills, saving the highest as the primary
			var highestSkill = 0;
			foreach (var skill in factionData.favouriteSkills)
			{
				// Get the skill and check if it is lower than the favored skill requirements
				var characterSkill = character.GetSkill(skill);
				if (characterSkill < rankData.FavouredSkills)
				{
					meetsSkillAndAttributeRequirements = false;
					break;
				}

				// If this skill is higher than the last, set it as the highest skill
				if (characterSkill > highestSkill)
					highestSkill = characterSkill;
			}

			// Finally check the highest sklil
			if (highestSkill < rankData.PrimarySkill)
				meetsSkillAndAttributeRequirements = false;

			// Return 0 if nothing met, 1 if skills but not reputation, 2 if reputation but not skills, 3 if requirements met
			if (meetsReputationRequirement)
			{
				if (meetsSkillAndAttributeRequirements)
					return 3;
				else
					return 2;
			}
			else if (meetsSkillAndAttributeRequirements)
				return 1;

			return 0;
		}

		public string GetRankName(int index)
		{
			return ranks[index];
		}

		public override void Initialize(System.IO.BinaryReader reader, RecordHeader header)
		{
			Faction reactionFaction = null;

			while (reader.BaseStream.Position < header.DataEndPos)
			{
				var type = (SubRecordType)reader.ReadInt32();
				var size = reader.ReadInt32();

				switch (type)
				{
					case SubRecordType.Id:
						name = reader.ReadString(size);
						factionDictionary[name] = this;
						break;
					case SubRecordType.Name:
						fullName = reader.ReadString(size);
						break;
					case SubRecordType.RankName:
						ranks.Add(reader.ReadString(size));
						break;
					case SubRecordType.FactionData:
						factionData = new FactionData(reader);
						break;
					case SubRecordType.Anam:
						reactionFaction = GetReactionFaction(reader, size);
						break;
					case SubRecordType.IntValue:
						var reactionValue = reader.ReadInt32();
						var factionReaction = new FactionReaction(reactionFaction, reactionValue);
						reactions.Add(factionReaction);
						break;
				}
			}
		}

		private Faction GetReactionFaction(System.IO.BinaryReader reader, int size)
		{
			var reactionFactionName = reader.ReadString(size);
			Faction reactionFaction;
			if (factionDictionary.TryGetValue(reactionFactionName, out reactionFaction))
			{
				return reactionFaction;
			}

			return Record.GetRecord<Faction>(reactionFactionName);
		}
	}
}