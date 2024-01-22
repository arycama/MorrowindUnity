using System;
using UnityEngine;

namespace Nif
{
	[Serializable]
	class NiSkinInstance : NiObject
	{
		private readonly int data, rootBone;
		private readonly int[] bones;

		public NiSkinInstance(NiFile niFile) : base(niFile)
		{
			data = niFile.Reader.ReadInt32();
			rootBone = niFile.Reader.ReadInt32();

			var boneCount = niFile.Reader.ReadInt32();
			bones = new int[boneCount];
			for (var i = 0; i < bones.Length; i++)
			{
				bones[i] = niFile.Reader.ReadInt32();
			}
		}

		public override void Process()
		{
			if(data == -1)
			{
				return;
			}

			niFile.NiObjects[data].NiParent = NiParent;
		}

		public override void ProcessNiObject(NiObjectNet niObject)
		{
			niFile.NiObjects[data].ProcessNiObject(niObject);

			var skinnedMeshRenderer = niObject.GameObject.AddComponent<SkinnedMeshRenderer>();

			skinnedMeshRenderer.sharedMesh = niObject.Mesh;
			skinnedMeshRenderer.sharedMaterial = niObject.Material;
			skinnedMeshRenderer.rootBone = niFile.NiObjects[rootBone].GameObject.transform;

			var boneList = new Transform[bones.Length];
			for (var i = 0; i < bones.Length; i++)
			{
				var boneNiObject = niFile.NiObjects[bones[i]];
				var gameObject = boneNiObject.GameObject;
				var transform = gameObject.transform;
				boneList[i] = transform;
			}

			skinnedMeshRenderer.bones = boneList;
			skinnedMeshRenderer.localBounds = skinnedMeshRenderer.sharedMesh.bounds;
			skinnedMeshRenderer.updateWhenOffscreen = true;
		}
	}
}