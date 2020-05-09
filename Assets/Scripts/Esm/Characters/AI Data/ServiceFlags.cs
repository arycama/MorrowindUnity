using System;

[Flags]
public enum ServiceFlags
{
	None,
	Weapon = 0x1,
	Armor = 0x2,
	Clothing = 0x4,
	Books = 0x8,
	Ingredient = 0x10,
	Picks = 0x20,
	Probes = 0x40,
	Lights = 0x80,
	Apparatus = 0x100,
	RepairItem = 0x200,
	Misc = 0x400,
	Spells = 0x800,
	MagicItems = 0x1000,
	Potions = 0x2000,
	Training = 0x4000,
	Spellmaking = 0x8000,
	Enchanting = 0x10000,
	Repair = 0x20000
}