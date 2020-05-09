using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Nif
{
	/// <summary>
	/// All child meshes of this node are treated as collision-only nodes
	/// </summary>
	class RootCollisionNode : NiNode
	{
		public RootCollisionNode(NiFile niFile) : base(niFile) { }

		public override void ProcessNiObject(NiObjectNet niObject)
		{
			IsRootCollision = true;
			base.ProcessNiObject(niObject);
		}
	}
}