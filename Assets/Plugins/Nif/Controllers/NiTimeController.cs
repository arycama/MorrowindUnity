using System;

namespace Nif
{
	[Serializable]
	abstract class NiTimeController : NiObject
	{
		protected Ref<NiTimeController> nextController;
		private readonly Flags flags;
		protected Ref<NiObjectNet> target;
		protected float frequency, phase, startTime, stopTime;

		[Flags]
		private enum Flags : short
		{
			APP_INIT = 0x0,
			Reverse = 0x1,
			Loop = 0x2,
			Active = 0x4,
			PlayBackwards = 0x8,
		};

		private enum AnimType
		{
			AppTime,
			AppInit
		}

		private enum CycleType
		{
			Loop,
			Reverse
		}

		public NiTimeController(NiFile niFile) : base(niFile)
		{
			nextController = new Ref<NiTimeController>(niFile);
			flags = (Flags)niFile.Reader.ReadInt16();
			frequency = niFile.Reader.ReadSingle();
			phase = niFile.Reader.ReadSingle();
			startTime = niFile.Reader.ReadSingle();
			stopTime = niFile.Reader.ReadSingle();
			target = new Ref<NiObjectNet>(niFile);
		}

		

		/*
		Controller flags(usually 0x000C). Probably controls loops.
Bit 0 : Anim type, 0=APP_TIME 1=APP_INIT
Bit 1-2 : Cycle type  00=Loop 01=Reverse 10=Loop
Bit 3 : Active
Bit 4 : Play backwards*/
	}
}