using System;

namespace Esm
{
	[Flags]
	public enum ClassAutoCalcFlags
	{
		Weapon = 0x00001,
		Armor = 0x00002,
		Clothing = 0x00004,
		Books = 0x00008,
		Ingrediant = 0x00010,
		Picks = 0x00020,
		Probes = 0x00040,
		Lights = 0x00080,
		Apparatus = 0x00100,
		Repair = 0x00200,
		Misc = 0x00400,
		Spells = 0x00800,
		MagicItems = 0x01000,
		Potions = 0x02000,
		Training = 0x04000,
		Spellmaking = 0x08000,
		Enchanting = 0x10000,
		RepairItem = 0x20000
	}
}