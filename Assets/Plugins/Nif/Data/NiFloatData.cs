using System;
using System.IO;
using UnityEngine;

namespace Nif
{
	[Serializable]
	class NiFloatData : NiObject
	{
		public NiFloatKeyframeGroup data;

		public NiFloatData(NiFile niFile) : base(niFile)
		{
			var length = niFile.Reader.ReadInt32();

			if (length < 0)
			{
				return;
			}

			var interpolation = (KeyType)niFile.Reader.ReadInt32();

			data = new NiFloatKeyframeGroup(length, interpolation, niFile.Reader);
		}

		public override void ProcessNiObject(NiObjectNet niObject)
		{

		}
	}
}