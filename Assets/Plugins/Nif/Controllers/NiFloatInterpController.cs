using System;
using System.IO;

namespace Nif
{
	[Serializable]
	abstract class NiFloatInterpController : NiSingleInterpController
	{
		public NiFloatInterpController(NiFile niFile) : base(niFile)
		{

		}
	}
}