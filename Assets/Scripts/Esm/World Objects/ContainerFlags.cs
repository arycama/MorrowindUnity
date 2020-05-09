using System;

namespace Esm
{
	[Flags]
	public enum ContainerFlags
	{
		Organic = 0x0001,
		Respawns = 0x0002,
		Default = 0x0008
	}
}