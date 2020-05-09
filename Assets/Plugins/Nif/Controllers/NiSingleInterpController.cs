using System;
using System.IO;

namespace Nif
{
	[Serializable]
	abstract class NiSingleInterpController : NiInterpController
	{
		public NiSingleInterpController(NiFile niFile) : base(niFile)
		{
		}
	}
}