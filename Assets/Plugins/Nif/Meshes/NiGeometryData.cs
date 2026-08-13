using System;
using UnityEngine;
using UnityEngine.Rendering;

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
			var attributeCount = 0;
			Span<VertexAttributeDescriptor> vertexAttributeDescriptors = stackalloc VertexAttributeDescriptor[7];

			Vector3[] vertices = null, normals = null;
			Vector2[] uv0 = null, uv1 = null, uv2 = null, uv3 = null;
			Color[] colors = null;

			// Read Vertices
			vertexCount = niFile.Reader.ReadInt16();
			hasVertices = niFile.Reader.ReadInt32() != 0;
			if (hasVertices)
			{
				vertexAttributeDescriptors[attributeCount] = new(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, attributeCount++);
				vertices = niFile.Reader.ReadVertexArray(vertexCount);
			}

			// Read Normals
			hasNormals = niFile.Reader.ReadInt32() != 0;
			if (hasNormals)
			{
				vertexAttributeDescriptors[attributeCount] = new(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, attributeCount++);
				normals = niFile.Reader.ReadVector3Array(vertexCount);
			}

			// Center position
			center = niFile.Reader.ReadVector3();
			radius = niFile.Reader.ReadSingle();

			// Vertex Colours
			hasColors = niFile.Reader.ReadInt32() != 0;
			if (hasColors)
			{
				vertexAttributeDescriptors[attributeCount] = new(VertexAttribute.Color, VertexAttributeFormat.Float32, 4, attributeCount++);
				colors = niFile.Reader.ReadColor4Array(vertexCount);
			}

			// Read UV Sets
			uvSetCount = niFile.Reader.ReadInt16();
			hasUVs = niFile.Reader.ReadInt32() != 0;
			if (hasUVs)
			{
				if (uvSetCount > 0)
				{
					vertexAttributeDescriptors[attributeCount] = new(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2, attributeCount++);
					uv0 = niFile.Reader.ReadUvArray(vertexCount);
				}

				if (uvSetCount > 1)
				{
					vertexAttributeDescriptors[attributeCount] = new(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 2, attributeCount++);
					uv1 = niFile.Reader.ReadUvArray(vertexCount);
				}

				if (uvSetCount > 2)
				{
					vertexAttributeDescriptors[attributeCount] = new(VertexAttribute.TexCoord2, VertexAttributeFormat.Float32, 2, attributeCount++);
					uv2 = niFile.Reader.ReadUvArray(vertexCount);
				}

				if (uvSetCount > 3)
				{
					vertexAttributeDescriptors[attributeCount] = new(VertexAttribute.TexCoord3, VertexAttributeFormat.Float32, 2, attributeCount++);
					uv3 = niFile.Reader.ReadUvArray(vertexCount);
				}
			}

			Mesh = new Mesh();
			Mesh.SetVertexBufferParams(vertexCount, vertexAttributeDescriptors.AsNativeArray(attributeCount));

			var n = 0;
			if (hasVertices)
				Mesh.SetVertexBufferData(vertices, 0, 0, vertexCount, n++);

			if (hasNormals)
				Mesh.SetVertexBufferData(normals, 0, 0, vertexCount, n++);

			if (hasColors)
				Mesh.SetVertexBufferData(colors, 0, 0, vertexCount, n++);

			if (hasUVs)
			{
				if (uvSetCount > 0)
					Mesh.SetVertexBufferData(uv0, 0, 0, vertexCount, n++);

				if (uvSetCount > 1)
					Mesh.SetVertexBufferData(uv1, 0, 0, vertexCount, n++);

				if (uvSetCount > 2)
					Mesh.SetVertexBufferData(uv2, 0, 0, vertexCount, n++);

				if (uvSetCount > 3)
					Mesh.SetVertexBufferData(uv3, 0, 0, vertexCount, n++);
			}

			Mesh.RecalculateBounds();
		}

		public Mesh Mesh { get; protected set; }
	}
}