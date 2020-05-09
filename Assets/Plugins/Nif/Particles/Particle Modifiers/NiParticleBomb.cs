using System;
using System.IO;
using UnityEngine;

namespace Nif
{
	[Serializable]
	class NiParticleBomb : NiParticleModifier
	{
		public float decay;
		public float duration;
		public float deltaV;
		public float start;
		private DecayType decayType;
		public Vector3 position;
		public Vector3 direction;

		public NiParticleBomb(NiFile niFile) : base(niFile)
		{
			decay = niFile.Reader.ReadSingle();
			duration = niFile.Reader.ReadSingle();
			deltaV = niFile.Reader.ReadSingle();
			start = niFile.Reader.ReadSingle();
			decayType = (DecayType)niFile.Reader.ReadInt32();
			position = niFile.Reader.ReadVector3();
			direction = niFile.Reader.ReadVector3();
		}

		private enum DecayType
		{
			DECAY_NONE,
			DECAY_LINEAR,
			DECAY_EXPONENTIAL
		}

		public override void ProcessNiObject(NiObjectNet niObject)
		{

		}
	}
}