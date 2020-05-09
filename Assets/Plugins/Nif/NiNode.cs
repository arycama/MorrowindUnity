using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Nif
{
	[Serializable]
	class NiNode : NiAVObject
	{
		protected int[] children, effects;

		public NiNode(NiFile niFile) : base(niFile)
		{
			var childCount = niFile.Reader.ReadInt32();
			children = new int[childCount];
			for (var i = 0; i < childCount; i++)
			{
				children[i] = niFile.Reader.ReadInt32();
			}

			var effectCount = niFile.Reader.ReadInt32();
			effects = new int[effectCount];
			for (var i = 0; i < effectCount; i++)
			{
				effects[i] = niFile.Reader.ReadInt32();
			}
		}

		public override void Process()
		{
			base.Process();
			foreach (var child in children)
			{
				if (child == -1)
				{
					continue;
				}

				niFile.NiObjects[child].NiParent = this;
			}
		}

		public override void ProcessNiObject(NiObjectNet niObject)
		{
			base.ProcessNiObject(niObject);

			foreach (var child in children)
			{
				if(child == -1)
				{
					continue;
				}
				
				niFile.NiObjects[child].IsRootCollision = IsRootCollision;
				niFile.NiObjects[child].Layer = GameObject.layer;
				niFile.NiObjects[child].IsBiped = IsBiped;
				niFile.NiObjects[child].Parent = GameObject.transform;
				niFile.NiObjects[child].ProcessNiObject(this);
			}

			foreach(var effect in effects)
			{
				niFile.NiObjects[effect].ProcessNiObject(this);
			}
		}
	}
}