using System;
using System.Collections.Generic;
using System.IO;

namespace Esm
{
	[Serializable]
	class BaseAttributeData
	{
		protected int strength, intelligence, willpower, agility, speed, endurance, personality, luck;

		private Dictionary<CharacterAttribute, byte> attributes;

		public BaseAttributeData(System.IO.BinaryReader reader)
		{
			attributes = new Dictionary<CharacterAttribute, byte>(8)
		{
			{ CharacterAttribute.sAttributeStrength, reader.ReadByte() },
			{ CharacterAttribute.sAttributeIntelligence, reader.ReadByte() },
			{ CharacterAttribute.sAttributeWillpower, reader.ReadByte() },
			{ CharacterAttribute.sAttributeAgility, reader.ReadByte() },
			{ CharacterAttribute.sAttributeSpeed, reader.ReadByte() },
			{ CharacterAttribute.sAttributeEndurance, reader.ReadByte() },
			{ CharacterAttribute.sAttributePersonality, reader.ReadByte() },
			{ CharacterAttribute.sAttributeLuck, reader.ReadByte() }
		};
		}

		public BaseAttributeData(System.IO.BinaryReader reader, bool isLong = true)
		{
			strength = reader.ReadInt32();
			intelligence = reader.ReadInt32();
			willpower = reader.ReadInt32();
			agility = reader.ReadInt32();
			speed = reader.ReadInt32();
			endurance = reader.ReadInt32();
			personality = reader.ReadInt32();
			luck = reader.ReadInt32();
		}
	}
}