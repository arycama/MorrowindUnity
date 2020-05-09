using System;
using System.IO;
using UnityEngine;

namespace Nif
{
	[Serializable]
	abstract class NiExtraData : NiObject
	{
		private Ref<NiExtraData> nextExtraData;

		public NiExtraData(NiFile niFile) : base(niFile)
		{
			nextExtraData = new Ref<NiExtraData>(niFile);
		}

		public override void ProcessNiObject(NiObjectNet niObject)
		{
			if(nextExtraData.Target != null)
			{
				nextExtraData.Target.ProcessNiObject(niObject);
			}
		}
	}
}