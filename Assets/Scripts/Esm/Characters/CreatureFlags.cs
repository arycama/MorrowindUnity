using System;

[Flags]
public enum CreatureFlags
{
	Biped = 0x0001,
	Respawn = 0x0002,
	WeaponAndShield = 0x0004,
	None = 0x0008,
	Swims = 0x0010,
	Flies = 0x0020,
	Walks = 0x0040,
	DefaultFlags = 0x0048,
	Essential = 0x0080,
	SkeletonBlood = 0x0400,
	MetalBlood = 0x0800
}