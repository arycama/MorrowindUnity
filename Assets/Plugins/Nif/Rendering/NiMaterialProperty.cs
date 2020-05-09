using System;
using System.IO;
using UnityEngine;

namespace Nif
{
	[Serializable]
	class NiMaterialProperty : NiProperty
	{
		private short flags;
		private Color ambient, diffuse, specular, emissive;
		private float glossiness;

		public NiMaterialProperty(NiFile niFile) : base(niFile)
		{
			flags = niFile.Reader.ReadInt16();
			ambient = niFile.Reader.ReadColor3();
			diffuse = niFile.Reader.ReadColor3();
			specular = niFile.Reader.ReadColor3();
			emissive = niFile.Reader.ReadColor3();
			glossiness = niFile.Reader.ReadSingle();
			diffuse.a = niFile.Reader.ReadSingle();
		}

		public override void Process()
		{
			base.Process();

			if(NiParent == null || NiParent.Material == null)
			{
				//Debug.Log(Name);
				return;
			}

			var material = NiParent.Material;

			material.SetColor("_Ambient", ambient);
			material.SetColor("_Color", diffuse);
			material.SetColor("_Specular", specular);
			material.SetColor("_EmissionColor", emissive);
			material.SetFloat("_Glossiness", glossiness);
		}
	}
}