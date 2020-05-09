using System;
using System.IO;

namespace Nif
{
	[Serializable]
	abstract class NiInterpController : NiTimeController
	{
		public NiInterpController(NiFile niFile) : base(niFile)
		{
		}
	}
}