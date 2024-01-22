using System;
using UnityEngine;

namespace Nif
{
	class NiGeomMorpherController : NiInterpController
	{
		private readonly int dataIndex;
		private readonly bool alwaysUpdate;

		public NiGeomMorpherController(NiFile niFile) : base(niFile)
		{
			dataIndex = niFile.Reader.ReadInt32();
			alwaysUpdate = niFile.Reader.ReadByte() != 0;
		}

		public override void Process()
		{
			// Don't set parent if there is no data attached
			if(dataIndex == -1)
			{
				return;
			}

			// Set the data object's parent to this object's parent
			var niObject = niFile.NiObjects[dataIndex];
			niObject.NiParent = NiParent;
		}

		public override void ProcessNiObject(NiObjectNet niObject)
		{
			// Add skinned mesh renderer, and set blend shapes
			var skinnedMeshRenderer = target.Target.GameObject.AddComponent<SkinnedMeshRenderer>();
			niFile.NiObjects[dataIndex].ProcessNiObject(target.Target);
			skinnedMeshRenderer.updateWhenOffscreen = alwaysUpdate;

			skinnedMeshRenderer.sharedMesh = target.Target.Mesh;
			skinnedMeshRenderer.sharedMaterial = target.Target.Material;
		}
	}
}