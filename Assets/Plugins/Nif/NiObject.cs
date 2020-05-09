using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Nif
{
	[Serializable]
	public abstract class NiObject
	{
		protected NiFile niFile;

		public NiObjectNet NiParent { get; set; }
		public int Layer { get; set; }
		public bool IsBiped { get; set; }
		public GameObject GameObject { get; protected set; }
		public Transform Parent { get; set; }
		public bool IsRootCollision { get; set; }

		public Material Material { get; set; }

		public virtual void ProcessNiObject(NiObjectNet niObject) { }

		public NiObject(NiFile niFile)
		{
			this.niFile = niFile;
		}

		public virtual void Process() { }

		protected string GetRelativePath()
		{
			if (NiParent == null)
			{
				return string.Empty;
			}

			if (NiParent.NiParent == null)
			{
				// This should maybe be string.empty
				return NiParent.Name;
			}

			var sb = new StringBuilder(NiParent.Name);
			var parent = NiParent.NiParent;
			while (parent.NiParent != null)
			{
				sb.Insert(0, parent.Name + "/");
				parent = parent.NiParent;
			}

			return sb.ToString();
		}
	}
}