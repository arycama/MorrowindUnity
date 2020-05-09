using System;
using System.IO;
using UnityEngine;

namespace Nif
{
	[Serializable]
	class NiParticlesData : NiGeometryData
	{
		public short numParticles;
		public float particleRadius;
		public short numActive;
		public bool hasSizes;
		public float[] sizes;

		public NiParticlesData(NiFile niFile) : base(niFile)
		{
			numParticles = niFile.Reader.ReadInt16();
			particleRadius = niFile.Reader.ReadSingle();
			numActive = niFile.Reader.ReadInt16();

			hasSizes = niFile.Reader.ReadInt32() != 0;
			if (hasSizes)
			{
				sizes = new float[vertexCount];
				for (int i = 0; i < sizes.Length; i++)
				{
					sizes[i] = niFile.Reader.ReadSingle();
				}
			}
		}

		public override void ProcessNiObject(NiObjectNet niObject)
		{

		}
	}
}