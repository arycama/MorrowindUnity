using System;

namespace Esm
{
	[Flags]
	public enum Gender : byte
	{
		None = 0xff,
		Male = 0x00,
		Female = 0x01
	};
}