using System;
using System.IO;

namespace Esm
{
	[Serializable]
	class SkillData
	{
		private readonly int block, armorer, mediumArmor, heavyArmor, bluntWeapon, longBlade, axe, spear, athletics, enchant, destruction, alteration, illustion, conjuration, mysticism, restoration, alchemy, unarmored, security, sneak, acrobatics, lightArmor, shortBlade, marksman, mercantile, speechcraft, handToHand;

		public SkillData(System.IO.BinaryReader reader)
		{
			block = reader.ReadByte();
			armorer = reader.ReadByte();
			mediumArmor = reader.ReadByte();
			heavyArmor = reader.ReadByte();
			bluntWeapon = reader.ReadByte();
			longBlade = reader.ReadByte();
			axe = reader.ReadByte();
			spear = reader.ReadByte();
			athletics = reader.ReadByte();
			enchant = reader.ReadByte();
			destruction = reader.ReadByte();
			alteration = reader.ReadByte();
			illustion = reader.ReadByte();
			conjuration = reader.ReadByte();
			mysticism = reader.ReadByte();
			restoration = reader.ReadByte();
			alchemy = reader.ReadByte();
			unarmored = reader.ReadByte();
			security = reader.ReadByte();
			sneak = reader.ReadByte();
			acrobatics = reader.ReadByte();
			lightArmor = reader.ReadByte();
			shortBlade = reader.ReadByte();
			marksman = reader.ReadByte();
			mercantile = reader.ReadByte();
			speechcraft = reader.ReadByte();
			handToHand = reader.ReadByte();
		}
	}
}