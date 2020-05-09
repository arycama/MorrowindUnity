using System;
using UnityEngine;

namespace Nif
{
	[Serializable]
	class NiSkinData : NiObject
	{
		private Matrix4x4 matrix;
		private int skinPartition;
		private SkinData[] boneList;
		private BoneWeight[] boneWeights;
		private Matrix4x4[] bindPoses;

		public NiSkinData(NiFile niFile) : base(niFile)
		{
			var rotation = niFile.Reader.ReadRotation();
			var position = niFile.Reader.ReadVector3();
			var scale = niFile.Reader.ReadSingle();

			matrix = Matrix4x4.TRS(position, rotation, new Vector3(scale, scale, scale));

			var boneCount = niFile.Reader.ReadInt32();
			skinPartition = niFile.Reader.ReadInt32();

			boneList = new SkinData[boneCount];
			for (var i = 0; i < boneList.Length; i++)
			{
				boneList[i] = new SkinData(niFile.Reader);
			}
		}

		public override void Process()
		{
			boneWeights = new BoneWeight[NiParent.Mesh.vertexCount];
			bindPoses = new Matrix4x4[boneList.Length];

			// Go through each bone, and add it's weight to the required vertices
			for (var i = 0; i < boneList.Length; i++)
			{
				// Each bone contains a matrix to specify the binded pose
				bindPoses[i] = matrix * boneList[i].Matrix;
				boneList[i].SetVertexWeights(boneWeights, i);
			}
		}

		public override void ProcessNiObject(NiObjectNet niObject)
		{
			niObject.Mesh.boneWeights = boneWeights;
			niObject.Mesh.bindposes = bindPoses;
		}

		[Serializable]
		private class SkinData
		{
			private Vector3 boundingSphereOffset;
			private float boundingSphereRadius;
			private SkinWeight[] vertexWeights;

			public SkinData(System.IO.BinaryReader reader)
			{
				var rotation = reader.ReadRotation();
				var position = reader.ReadVector3();
				var scale = reader.ReadSingle();

				Matrix = Matrix4x4.TRS(position, rotation, new Vector3(scale, scale, scale));

				boundingSphereOffset = reader.ReadVector3();
				boundingSphereRadius = reader.ReadSingle();

				var vertexCount = reader.ReadInt16();
				vertexWeights = new SkinWeight[vertexCount];
				for (var i = 0; i < vertexWeights.Length; i++)
				{
					vertexWeights[i] = new SkinWeight(reader);
				}
			}

			public Matrix4x4 Matrix { get; private set; }

			public void SetVertexWeights(BoneWeight[] boneWeights, int index)
			{
				foreach (var skinWeight in vertexWeights)
				{
					var boneWeight = boneWeights[skinWeight.Index];
					if (boneWeight.weight0 == 0)
					{
						boneWeight.boneIndex0 = index;
						boneWeight.weight0 = skinWeight.Weight;
					}
					else if (boneWeight.weight1 == 0)
					{
						boneWeight.boneIndex1 = index;
						boneWeight.weight1 = skinWeight.Weight;
					}
					else if (boneWeight.weight2 == 0)
					{
						boneWeight.boneIndex2 = index;
						boneWeight.weight2 = skinWeight.Weight;
					}
					else if (boneWeight.weight3 == 0)
					{
						boneWeight.boneIndex3 = index;
						boneWeight.weight3 = skinWeight.Weight;
					}
					else
					{
						// Should somehow average the weights if there are more than four
						//Debug.Log("Too many boneWeights");
					}

					boneWeights[skinWeight.Index] = boneWeight;
				}
			}
		}

		[Serializable]
		private class SkinWeight
		{
			public SkinWeight(System.IO.BinaryReader reader)
			{
				Index = reader.ReadInt16();
				Weight = reader.ReadSingle();
			}

			public short Index { get; private set; }
			public float Weight { get; private set; }
		}
	}
}