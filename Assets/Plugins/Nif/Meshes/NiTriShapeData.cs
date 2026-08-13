using System;
using UnityEngine;
using UnityEngine.Rendering;

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
			var meshData = MeshDataArray[0];

			meshData.SetIndexBufferParams(indexCount, IndexFormat.UInt16);
			var indexBuffer = meshData.GetIndexData<ushort>();

			for (var i = 0; i < indexCount; i += 3)
			{
				var i0 = niFile.Reader.ReadUInt16();
				var i2 = niFile.Reader.ReadUInt16();
				var i1 = niFile.Reader.ReadUInt16();

				indexBuffer[i] = i0;
				indexBuffer[i + 1] = i1;
				indexBuffer[i + 2] = i2;
			}

			meshData.subMeshCount = 1;
			meshData.SetSubMesh(0, new SubMeshDescriptor(0, indexCount));

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