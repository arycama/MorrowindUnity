using System;

namespace Nif
{
	[Serializable]
	abstract class NiTriBasedGeomData : NiGeometryData
	{
		protected int triangleCount;

		public NiTriBasedGeomData(NiFile niFile) : base(niFile)
		{
			triangleCount = niFile.Reader.ReadInt16();
		}
	}
}