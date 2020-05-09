using System;

namespace Esm
{
	[Serializable]
		public class MagicEffectData
		{
			public SpellSchool spellSchool;
			public float baseCost;
			public Flags flags;
			public int red, green, blue;
			public float speedX, sizeX, sizeCap;

			public MagicEffectData(System.IO.BinaryReader reader)
			{
				spellSchool = (SpellSchool)reader.ReadInt32();
				baseCost = reader.ReadSingle();
				flags = (Flags)reader.ReadInt32();
				red = reader.ReadInt32();
				green = reader.ReadInt32();
				blue = reader.ReadInt32();
				speedX = reader.ReadSingle();
				sizeX = reader.ReadSingle();
				sizeCap = reader.ReadSingle();
			}

			public enum SpellSchool
			{
				Alteration,
				Conjuration,
				Destruction,
				Illusion,
				Mysticism,
				Restoration
			}

			[Flags]
			public enum Flags
			{
				Spellmaking = 0x0200,
				Enchanting = 0x0400,
				Negative = 0x0800
			}
		}
	}