namespace Nif
{
	using System.IO;
	using UnityEngine;

	class NiRotatingParticlesData : NiParticlesData
	{
		public bool hasRotations;
		public Quaternion[] rotations;

		public NiRotatingParticlesData(NiFile niFile) : base(niFile)
		{
			hasRotations = niFile.Reader.ReadInt32() != 0;

			if (hasRotations)
			{
				rotations = new Quaternion[vertexCount];
				for (int i = 0; i < rotations.Length; i++)
				{
					rotations[i] = niFile.Reader.ReadQuaternion();
				}
			}
		}
	}
}