using System;
using System.IO;
using UnityEngine;

namespace Nif
{
	class NiStringExtraData : NiExtraData
	{
		private int bytesRemaining;
		private string data;

		public NiStringExtraData(NiFile niFile) : base(niFile)
		{
			bytesRemaining = niFile.Reader.ReadInt32();
			data = niFile.Reader.ReadLengthPrefixedString();
		}

		public override void ProcessNiObject(NiObjectNet niObject)
		{
			switch (data)
			{
				case "NCO":
				case "NCC":
					niObject.GameObject.tag = "No Collider";
					break;
				case "MRK":
					niObject.GameObject.tag = "Marker";
					break;
			}
		}
	}
}