using System;
using System.IO;
using UnityEngine;

namespace Esm
{
	[Serializable]
	public class InfoRecordData
	{
		[SerializeField]
		private DialogType dialogType;

		[SerializeField]
		private int minDispositionOrIndex;

		[SerializeField]
		private byte rank;

		[SerializeField]
		private Gender gender;

		[SerializeField]
		private byte playerRank;

		[SerializeField]
		private bool isDeleted;

		public InfoRecordData(BinaryReader reader)
		{
			dialogType = (DialogType)reader.ReadInt32();
			minDispositionOrIndex = reader.ReadInt32();
			rank = reader.ReadByte();
			gender = (Gender)reader.ReadByte();
			playerRank = reader.ReadByte();
			isDeleted = reader.ReadByte() != 0; // always 0, possibly deleted?
		}

		public bool IsDeleted => isDeleted;

		public bool CheckFilters(Character npc, Character player, Faction faction, bool invertDisposition)
		{
			// Checks the speaker disposition
			if (player != null)
			{
				var disposition = npc.GetDisposition(player);
				if(invertDisposition)
				{
					if(minDispositionOrIndex != 0 && disposition >= minDispositionOrIndex)
					{
						return false;
					}
				}
				else if(disposition < minDispositionOrIndex)
				{
					return false;
				}
			}

			// Returns false if the speaker is not the required gender
			if (gender != Gender.None && npc.IsFemale == (gender == Gender.Male))
				return false;

			// 255 means no rank, so skip otherwise
			if (rank != byte.MaxValue)
			{
				// If faction is null, rank can be applied to any faction
				if (faction == null)
				{
					// Check if Npc is the specified rank in any faction
					if (!npc.IsRankOfAnyFaction(rank))
						return false;
				}
				// Check if Npc is the specified rank in a specific faction
				else if (npc.IsFactionRank(faction, rank))
					return false;
			}

			// Check player rank if needed 
			if (playerRank != byte.MaxValue)
			{
				// If faction is null, rank can be applied to any faction
				if (faction == null)
				{
					if (!player.IsRankOfAnyFaction(rank))
						return false;
				}
				else if (player.IsFactionRank(faction, rank))
					return false;
			}

			// Return true if no conditions were failed
			return true;
		}
	}
}