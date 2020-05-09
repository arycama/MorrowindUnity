using UnityEngine;
using System;
using System.Collections.Generic;

namespace Nif
{
	[Serializable]
	public abstract class NiObjectNet : NiObject
	{
		public string Name { get; protected set; }
		protected int extraData, controller;

		public NiObjectNet(NiFile niFile) : base(niFile)
		{
			Name = niFile.Reader.ReadLengthPrefixedString();
			extraData = niFile.Reader.ReadInt32();
			controller = niFile.Reader.ReadInt32();
		}
		
		public Mesh Mesh { get; set; }

		public override void Process()
		{
			if (controller != -1)
			{
				niFile.NiObjects[controller].NiParent = this;
			}
		}

		public override void ProcessNiObject(NiObjectNet niObject)
		{
			// If name is Bip01, this is a skinned mesh. (Though it could also be root bone)
			// If name is Bip01, find a transform of the same name and load additively from that
			if (Parent != null && this.Name == "Bip01")
			{
				var bip01 = FindBip01(Parent);
				if(bip01 != null)
				{
					IsBiped = true;
					GameObject = bip01.gameObject;
					return;
				}
			}

			if (IsBiped && Parent != null && this.Name.Contains("Bip"))
			{
				var child = Parent.transform.Find(this.Name);
				if(child != null)
				{
					GameObject = child.gameObject;
					return;
				}
			}

			// If this has no name, use the type as the name
			var name = this.Name;
			if (string.IsNullOrEmpty(name))
			{
				name = GetType().Name;
			}

			// Create the GameObject
			GameObject = new GameObject(name);
			niFile.CreatedObjects.Add(GameObject);
			GameObject.layer = Layer;

			if(Parent != null)
			{
				GameObject.transform.parent = Parent;
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

		// Recursivley searches up the hierachy to find an object named Bip01
		private Transform FindBip01(Transform transform)
		{
			var bip01 = transform.Find("Bip01");
			if(bip01 == null)
			{
				if(transform.parent != null)
				{
					return FindBip01(transform.parent);
				}
				else
				{
					return null;
				}
			}

			return bip01;
		}
	}
}