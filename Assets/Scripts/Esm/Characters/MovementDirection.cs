using System;

[Flags]
public enum MovementDirection
{
	None = 0x0,
	Forward = 0x1,
	Back = 0x2,
	Left = 0x4,
	Right = 0x8,
	ForwardLeft = Forward | Left,
	ForwardRight = Forward | Right,
	BackLeft = Back | Left,
	BackRight = Back | Right,
	ForwardLeftRight = Forward | Left | Right,
	BackLeftRight = Back | Left | Right,
	LeftForwardBack = Left | Forward | Back,
	RightForwardBack = Right | Forward | Back
};