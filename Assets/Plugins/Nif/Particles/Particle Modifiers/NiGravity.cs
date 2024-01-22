using System.IO;
using UnityEngine;

namespace Nif
{
	class NiGravity : NiParticleModifier
	{
		public float unknownFloat1;
		public float force;
		private readonly FieldType type;
		private Vector3 position, direction;

		public NiGravity(NiFile niFile) : base(niFile)
		{
			unknownFloat1 = niFile.Reader.ReadSingle();
			force = niFile.Reader.ReadSingle();
			type = (FieldType)niFile.Reader.ReadInt32();
			position = niFile.Reader.ReadVector3();
			direction = niFile.Reader.ReadVector3();
		}

		private enum FieldType
		{
			Wind,
			Point
		}

		public override void ProcessNiObject(NiObjectNet niObject)
		{

		}
	}
}