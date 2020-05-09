using System;
using System.IO;

namespace Nif
{
	[Serializable]
	class NiParticleMeshModifier : NiParticleModifier
	{
		public int numParticleMeshes;
		public int[] particleMeshes;

		public NiParticleMeshModifier(NiFile niFile) : base(niFile)
		{
			numParticleMeshes = niFile.Reader.ReadInt32();

			particleMeshes = new int[numParticleMeshes];
			for (int i = 0; i < particleMeshes.Length; i++)
			{
				particleMeshes[i] = niFile.Reader.ReadInt32();
			}
		}

		public override void ProcessNiObject(NiObjectNet niObject)
		{

		}
	}
}