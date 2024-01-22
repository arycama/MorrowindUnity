using System;

namespace Nif
{
	[Serializable]
	class NiTriShapeData : NiTriBasedGeomData
	{
		private readonly int matchGroupCount;
		private readonly MatchGroup[] matchGroups;

		public NiTriShapeData(NiFile niFile) : base(niFile)
		{
			var indexCount = niFile.Reader.ReadInt32();
			Mesh.triangles = niFile.Reader.ReadTriangles(indexCount);

			matchGroupCount = niFile.Reader.ReadInt16();

			matchGroups = new MatchGroup[matchGroupCount];
			for (var i = 0; i < matchGroups.Length; i++)
			{
				matchGroups[i] = new MatchGroup(niFile.Reader);
			}
		}

		private class MatchGroup
		{
			private readonly short vertexCount;
			private readonly short[] vertexIndices;

			public MatchGroup(System.IO.BinaryReader reader)
			{
				vertexCount = reader.ReadInt16();
				vertexIndices = new short[vertexCount];
				for (var i = 0; i < vertexIndices.Length; i++)
				{
					vertexIndices[i] = reader.ReadInt16();
				}
			}
		}
	}
}