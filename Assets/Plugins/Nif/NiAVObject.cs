using System;
using UnityEngine;

namespace Nif
{
	[Serializable]
	abstract class NiAVObject : NiObjectNet
	{
		protected Vector3 position, velocity;
		private Flags flags;
		private Quaternion rotation;
		private float scale;
		protected int propertyCount;
		protected int[] properties;
		private bool hasBoundingBox;
		private BoundingBox boundingBox;

		[Flags]
		protected enum Flags
		{
			SkinInfluence = 0,
			Hidden = 1,
			Triangle = 2,
			BoundingBox = 4,
			Continue = 6,
			None = 8,
			Shadow = 64
		}

		public NiAVObject(NiFile niFile) : base(niFile)
		{
			flags = (Flags)niFile.Reader.ReadInt16();

			position = niFile.Reader.ReadVector3();
			rotation = niFile.Reader.ReadRotation();
			scale = niFile.Reader.ReadSingle();

			velocity = niFile.Reader.ReadVector3();
			propertyCount = niFile.Reader.ReadInt32();
			properties = new int[propertyCount];
			for (var i = 0; i < propertyCount; i++)
			{
				properties[i] = niFile.Reader.ReadInt32();
			}

			hasBoundingBox = niFile.Reader.ReadInt32() != 0;
			if (hasBoundingBox)
			{
				boundingBox = new BoundingBox(niFile.Reader);
			}
		}

		public override void Process()
		{
			base.Process();

			if(propertyCount > 0)
			{
				// Iterate over properties to see if any of them contain an existing material, if not, make a new one
				foreach(var property in properties)
				{
					var ni = niFile.NiObjects[property];
					ni.NiParent = this;

					if (ni.Material != null)
					{
						Material = ni.Material;
						//continue;
						return;
					}
				}

				var shader = MaterialManager.Instance.DefaultShader;
				Material = new Material(shader);
			}
		}

		// If true, child NiTriShapes will be created with Mesh Colliders and no Mesh Renderers
		public bool IsCollisionNode { get; set; }

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

			base.ProcessNiObject(niObject);

			if(NiParent != null)
			{
				GameObject.tag = NiParent.GameObject.tag;
				GameObject.layer = NiParent.GameObject.layer;
			}


			if (flags.HasFlag(Flags.Hidden))
			{
				GameObject.tag = "Hidden"; // Set tag and layer, so we can easily check if an object is hidden/marker etc by checking it's tag
				GameObject.layer = LayerMask.NameToLayer("Hidden");
				//GameObject.hideFlags = HideFlags.HideInHierarchy;
			}

			if (flags.HasFlag(Flags.Shadow))
			{
				GameObject.tag = "Shadow";
			}

			// Set transform (Use localPosition/rotation as this may be already parented)
			GameObject.transform.localPosition = position;
			GameObject.transform.localRotation = rotation;
			GameObject.transform.localScale = new Vector3(scale, scale, scale);

			if (hasBoundingBox)
			{
				boundingBox.AddCollider(GameObject);
			}
		}

		public class BoundingBox
		{
			private int unknownInt;
			private Vector3 position, size;
			private Matrix4x4 rotation;

			public BoundingBox(System.IO.BinaryReader reader)
			{
				unknownInt = reader.ReadInt32();
				position = reader.ReadVector3();
				rotation = reader.ReadMatrix();
				size = reader.ReadVector3();
			}

			public void AddCollider(GameObject gameObject)
			{
				var box = gameObject.AddComponent<BoxCollider>();
				box.center = position - gameObject.transform.localPosition;
				box.size = size * 2;
			}
		}
	}
}