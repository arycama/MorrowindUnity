using System;
using System.IO;

namespace Nif
{
	[Serializable]
	abstract class NiBoolInterpController : NiSingleInterpController
	{
		public NiBoolInterpController(NiFile niFile) : base(niFile)
		{
		}
	}
}