using System;
using System.IO;
using UnityEngine;

namespace Nif
{
	[Serializable]
	class NiWireframeProperty : NiProperty
	{
		[SerializeField]
		private int enabled;

		public NiWireframeProperty(NiFile niFile) : base(niFile)
		{
			enabled = niFile.Reader.ReadInt16();
		}

		public override void ProcessNiObject(NiObjectNet niObject)
		{
			
		}
	}
}