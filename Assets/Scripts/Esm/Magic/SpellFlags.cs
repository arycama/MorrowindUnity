using System;

namespace Esm
{
	[Flags]
	public enum SpellFlags
	{
		AutoCalculateCost = 0x1,
		PcStartSpell = 0x2,
		AlwaysSucceeds = 0x4
	};
}