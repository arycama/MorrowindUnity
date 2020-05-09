using System;

namespace Nif
{
	class NiAlphaController : NiFloatInterpController
	{
		public int data;

		public NiAlphaController(NiFile niFile) : base(niFile)
		{
			data = niFile.Reader.ReadInt32();
		}

		public override void ProcessNiObject(NiObjectNet niObject)
		{
			
		}
	}
}