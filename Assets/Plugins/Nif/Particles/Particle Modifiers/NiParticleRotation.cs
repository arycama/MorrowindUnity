using System;
using System.IO;
using UnityEngine;

namespace Nif
{
	[Serializable]
	class NiParticleRotation : NiParticleModifier
	{
		public byte randomInitialAxis;
		public Vector3 initialAxis;
		public float rotationSpeed;

		public NiParticleRotation(NiFile niFile) : base(niFile)
		{
			randomInitialAxis = niFile.Reader.ReadByte();
			initialAxis = niFile.Reader.ReadVector3();
			rotationSpeed = niFile.Reader.ReadSingle();
		}

		public override void ProcessNiObject(NiObjectNet niObject)
		{

		}
	}
}