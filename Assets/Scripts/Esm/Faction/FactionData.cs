using System;

namespace Esm
{
	[Serializable]
	public class FactionData
	{
		public CharacterAttribute[] attributes = new CharacterAttribute[2];
		public RankData[] rankData = new RankData[10];
		public CharacterSkill[] favouriteSkills = new CharacterSkill[6];
		public int unknown1;
		public Flags flags;

		[Flags]
		public enum Flags
		{
			None,
			HiddenFromPlayer
		}

		public FactionData(System.IO.BinaryReader reader)
		{
			for (var i = 0; i < attributes.Length; i++)
			{
				attributes[i] = (CharacterAttribute)reader.ReadInt32();
			}

			for (var i = 0; i < rankData.Length; i++)
			{
				rankData[i] = new RankData(reader);
			}

			for (var i = 0; i < favouriteSkills.Length; i++)
			{
				favouriteSkills[i] = (CharacterSkill)reader.ReadInt32();
			}

			unknown1 = reader.ReadInt32(); // -1 for all except Imperial Cult. Maybe it has something to do with shrines?
			flags = (Flags)reader.ReadInt32();
		}
	}
	
}