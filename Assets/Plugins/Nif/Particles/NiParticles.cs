using System.IO;
using UnityEngine;

namespace Nif
{
	class NiParticles : NiGeometry
	{
		public NiParticles(NiFile niFile) : base(niFile)
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
		}
	}
}