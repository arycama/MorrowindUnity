namespace Nif
{
	using System.IO;

	class NiBSParticleNode : NiNode
	{
		public NiBSParticleNode(NiFile niFile) : base(niFile)
		{

		}

		public override void ProcessNiObject(NiObjectNet niObject)
		{
			// Process properties first, as some NiNodes such as NiGeomControl will add a mesh renderer which needs the material
			if (propertyCount > 0)
			{
				// Create a new material for the properties to modify

				//Material = new Material(material);
				foreach (var property in properties)
				{
					niFile.NiObjects[property].NiParent = this;
				}
			}

			// Process Extra Data
			if (extraData != -1)
			{
				niFile.NiObjects[extraData].ProcessNiObject(this);
			}

			// Process Controller
			if (controller != -1)
			{
				niFile.NiObjects[controller].ProcessNiObject(this);
			}

			foreach (var child in children)
			{
				if (child == -1)
				{
					continue;
				}

				niFile.NiObjects[child].IsRootCollision = IsRootCollision;
				niFile.NiObjects[child].Layer = NiParent.Layer;
				niFile.NiObjects[child].IsBiped = IsBiped;
				niFile.NiObjects[child].Parent = NiParent.GameObject.transform;
				niFile.NiObjects[child].ProcessNiObject(this);
			}

			foreach (var effect in effects)
			{
				niFile.NiObjects[effect].ProcessNiObject(this);
			}
		}
	}
}