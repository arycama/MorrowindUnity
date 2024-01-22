using System;
using System.IO;
using UnityEngine;

namespace Nif
{
	[Serializable]
	class NiKeyframeController : NiSingleInterpController
	{
		private readonly int data;

		public NiKeyframeController(NiFile niFile) : base(niFile)
		{
			data = niFile.Reader.ReadInt32();
		}

		public override void Process()
		{
			if(data != -1)
			{
				niFile.NiObjects[data].NiParent = target.Target;
			}
		}
	}
}