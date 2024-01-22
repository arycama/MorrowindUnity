using System;
using UnityEngine;

namespace Nif
{
	abstract class NiGeometryData : NiObject
	{
		protected int vertexCount, uvSetCount;
		private readonly bool hasVertices, hasNormals, hasColors, hasUVs;
		private Vector3 center;
		private readonly float radius;

		public NiGeometryData(NiFile niFile) : base(niFile)
		{
			Mesh = new Mesh();

			// Read Vertices
			vertexCount = niFile.Reader.ReadInt16();
			hasVertices = niFile.Reader.ReadInt32() != 0;
			if (hasVertices)
			{
				Mesh.vertices = niFile.Reader.ReadVertexArray(vertexCount);
			}

			// Read Normals
			hasNormals = niFile.Reader.ReadInt32() != 0;
			if (hasNormals)
			{
				Mesh.normals = niFile.Reader.ReadVector3Array(vertexCount);
			}

			// Center position
			center = niFile.Reader.ReadVector3();
			radius = niFile.Reader.ReadSingle();

			// Vertex Colours
			hasColors = niFile.Reader.ReadInt32() != 0;
			if (hasColors)
			{
				Mesh.colors = niFile.Reader.ReadColor4Array(vertexCount);
			}

			// Read UV Sets
			uvSetCount = niFile.Reader.ReadInt16();
			hasUVs = niFile.Reader.ReadInt32() != 0;
			if (hasUVs)
			{
				var uvSets = new Vector2[uvSetCount][];
				for (var i = 0; i < uvSets.Length; i++)
				{
					switch (i)
					{
						case 0:
							Mesh.uv = niFile.Reader.ReadUvArray(vertexCount);
							break;
						case 1:
							Mesh.uv2 = niFile.Reader.ReadUvArray(vertexCount);
							break;
						case 2:
							Mesh.uv3 = niFile.Reader.ReadUvArray(vertexCount);
							break;
						case 3:
							Mesh.uv4 = niFile.Reader.ReadUvArray(vertexCount);
							break;
					}
				}
			}
		}

		public Mesh Mesh { get; protected set; }
	}
}