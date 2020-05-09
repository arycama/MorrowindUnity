using System;

namespace Esm
{
	[Flags]
	public enum LightFlags
	{
		Dynamic = 0x0001,
		CanCarry = 0x0002,
		Negative = 0x0004,
		Flicker = 0x0008,
		Fire = 0x0010,
		OffByDefault = 0x0020,
		FlickerSlow = 0x0040,
		Pulse = 0x0080,
		PulseSlow = 0x0100
	}
}