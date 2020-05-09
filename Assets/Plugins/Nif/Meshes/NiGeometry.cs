using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nif
{
	[Serializable]
	class NiGeometry : NiAVObject
	{
		private Ref<NiGeometryData> data;
		private Ref<NiSkinInstance> skin;

		public NiGeometry(NiFile niFile) : base(niFile)
		{
			data = new Ref<NiGeometryData>(niFile);
			skin = new Ref<NiSkinInstance>(niFile);
		}

		public override void Process()
		{
			base.Process();

			// Process the mesh data first, as there may be blend shapes that need to add to the mesh data
			if (data.Target != null)
			{
				data.Target.NiParent = this;
				Mesh = data.Target.Mesh;
			}

			if(skin.Target != null)
			{
				skin.Target.NiParent = this;
			}
		}

		public override void ProcessNiObject(NiObjectNet niObject)
		{
			base.ProcessNiObject(niObject);

			// Create a skinned Mesh Renderer if skin is not null
			if (skin.Target != null)
			{
				skin.Target.ProcessNiObject(this);
				return;
			}

			// Check for a skinned mesh renderer as a controller may have added it
			if (GameObject.GetComponent<SkinnedMeshRenderer>())
			{
				return;
			}
				
			if (IsRootCollision)
			{
				GameObject.AddComponent<MeshCollider>().sharedMesh = Mesh;
				return;
			}

			// Create a mesh filer and renderer
			GameObject.AddComponent<MeshFilter>().sharedMesh = Mesh;
			var meshRenderer = GameObject.AddComponent<MeshRenderer>();
			meshRenderer.sharedMaterial = Material;
			meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
		}
	}
}