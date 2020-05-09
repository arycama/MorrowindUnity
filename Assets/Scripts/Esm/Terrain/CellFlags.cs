using System;

[Flags]
public enum CellFlags
{
	Interior = 0x01,
	HasWater = 0x02,
	IllegalToSleepHere = 0x04,
	BehaveLikeExterior = 0x40
}