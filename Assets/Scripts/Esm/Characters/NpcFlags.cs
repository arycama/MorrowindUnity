using System;

namespace Esm
{
	[Flags]
	public enum NpcFlags
	{
		Female = 0x1,
		Essential = 0x2,
		Respawn = 0x4,
		Unknown = 0x8,
		Autocalc = 0x10,
		BloodSkel = 0x400,
		BloodMetal = 0x800
	}
}