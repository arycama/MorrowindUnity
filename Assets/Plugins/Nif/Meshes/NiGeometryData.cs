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

			long vertexPosition = -1, normalPosition = -1, colorPosition = -1, uv0Position = -1, uv1Position = -1, uv2Position = -1, uv3Position = -1;

			// Read Vertices
			vertexCount = niFile.Reader.ReadInt16();
			hasVertices = niFile.Reader.ReadInt32() != 0;
			if (hasVertices)
			{
				vertexAttributeDescriptors[attributeCount] = new(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, attributeCount++);
				vertexPosition = niFile.Reader.BaseStream.Position;
				niFile.Reader.BaseStream.Position += vertexCount * 3 * sizeof(float);
			}

			// Read Normals
			hasNormals = niFile.Reader.ReadInt32() != 0;
			if (hasNormals)
			{
				vertexAttributeDescriptors[attributeCount] = new(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, attributeCount++);
				normalPosition = niFile.Reader.BaseStream.Position;
				niFile.Reader.BaseStream.Position += vertexCount * 3 * sizeof(float);
			}

			// Center position
			center = niFile.Reader.ReadVector3();
			radius = niFile.Reader.ReadSingle();

			// Vertex Colours
			hasColors = niFile.Reader.ReadInt32() != 0;
			if (hasColors)
			{
				vertexAttributeDescriptors[attributeCount] = new(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4, attributeCount++);
				colorPosition = niFile.Reader.BaseStream.Position;
				niFile.Reader.BaseStream.Position += vertexCount * 4 * sizeof(float);
			}

			// Read UV Sets
			uvSetCount = niFile.Reader.ReadInt16();
			hasUVs = niFile.Reader.ReadInt32() != 0;
			if (hasUVs)
			{
				if (uvSetCount > 0)
				{
					vertexAttributeDescriptors[attributeCount] = new(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2, attributeCount++);
					uv0Position = niFile.Reader.BaseStream.Position;
					niFile.Reader.BaseStream.Position += vertexCount * 2 * sizeof(float);
				}

				if (uvSetCount > 1)
				{
					vertexAttributeDescriptors[attributeCount] = new(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 2, attributeCount++);
					uv1Position = niFile.Reader.BaseStream.Position;
					niFile.Reader.BaseStream.Position += vertexCount * 2 * sizeof(float);
				}

				if (uvSetCount > 2)
				{
					vertexAttributeDescriptors[attributeCount] = new(VertexAttribute.TexCoord2, VertexAttributeFormat.Float32, 2, attributeCount++);
					uv2Position = niFile.Reader.BaseStream.Position;
					niFile.Reader.BaseStream.Position += vertexCount * 2 * sizeof(float);
				}

				if (uvSetCount > 3)
				{
					vertexAttributeDescriptors[attributeCount] = new(VertexAttribute.TexCoord3, VertexAttributeFormat.Float32, 2, attributeCount++);
					uv3Position = niFile.Reader.BaseStream.Position;
					niFile.Reader.BaseStream.Position += vertexCount * 2 * sizeof(float);
				}
			}

			var endPosition = niFile.Reader.BaseStream.Position;

			MeshDataArray = Mesh.AllocateWritableMeshData(1);
			HasMeshData = true;
			MeshDataArray[0].SetVertexBufferParams(vertexCount, vertexAttributeDescriptors.AsNativeArray(attributeCount));

			var n = 0;
			if (hasVertices)
			{
				niFile.Reader.BaseStream.Position = vertexPosition;
				var vertices = MeshDataArray[0].GetVertexData<Vector3>(n++);
				for (var i = 0; i < vertexCount; i++)
					vertices[i] = niFile.Reader.ReadVector3();
			}

			if (hasNormals)
			{
				niFile.Reader.BaseStream.Position = normalPosition;
				var normals = MeshDataArray[0].GetVertexData<Vector3>(n++);
				for (var i = 0; i < vertexCount; i++)
					normals[i] = niFile.Reader.ReadVector3();
			}

			if (hasColors)
			{
				niFile.Reader.BaseStream.Position = colorPosition;
				var colors = MeshDataArray[0].GetVertexData<Color32>(n++);
				for (var i = 0; i < vertexCount; i++)
					colors[i] = niFile.Reader.GetReadColor4();
			}

			if (hasUVs)
			{
				if (uvSetCount > 0)
				{
					niFile.Reader.BaseStream.Position = uv0Position;
					var uv0 = MeshDataArray[0].GetVertexData<Vector2>(n++);
					for (var i = 0; i < vertexCount; i++)
						uv0[i] = new Vector2(niFile.Reader.ReadSingle(), niFile.Reader.ReadSingle());
				}

				if (uvSetCount > 1)
				{
					niFile.Reader.BaseStream.Position = uv1Position;
					var uv1 = MeshDataArray[0].GetVertexData<Vector2>(n++);
					for (var i = 0; i < vertexCount; i++)
						uv1[i] = new Vector2(niFile.Reader.ReadSingle(), niFile.Reader.ReadSingle());
				}

				if (uvSetCount > 2)
				{
					niFile.Reader.BaseStream.Position = uv2Position;
					var uv2 = MeshDataArray[0].GetVertexData<Vector2>(n++);
					for (var i = 0; i < vertexCount; i++)
						uv2[i] = new Vector2(niFile.Reader.ReadSingle(), niFile.Reader.ReadSingle());
				}

				if (uvSetCount > 3)
				{
					niFile.Reader.BaseStream.Position = uv3Position;
					var uv3 = MeshDataArray[0].GetVertexData<Vector2>(n++);
					for (var i = 0; i < vertexCount; i++)
						uv3[i] = new Vector2(niFile.Reader.ReadSingle(), niFile.Reader.ReadSingle());
				}
			}

			niFile.Reader.BaseStream.Position = endPosition;
		}

		public bool HasMeshData { get; set; }
		public Mesh.MeshDataArray MeshDataArray { get; protected set; }
	}
}