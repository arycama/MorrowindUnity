using System;
using System.IO;
using UnityEngine;

namespace Nif
{
	[Serializable]
	class NiVertexColorProperty : NiProperty
	{
		private readonly int flags;
		private readonly VertMode vertexMode;
		private readonly LightMode lightingMode;

		public NiVertexColorProperty(NiFile niFile) : base(niFile)
		{
			flags = niFile.Reader.ReadInt16();
			vertexMode = (VertMode)niFile.Reader.ReadInt32();
			lightingMode = (LightMode)niFile.Reader.ReadInt32();
		}

		private enum VertMode
		{
			VERT_MODE_SRC_IGNORE,
			VERT_MODE_SRC_EMISSIVE,
			VERT_MODE_SRC_AMB_DIF
		}

		private enum LightMode
		{
			LIGHT_MODE_EMISSIVE,
			LIGHT_MODE_EMI_AMB_DIF
		}

		public override void Process()
		{
			base.Process();

			NiParent.Material.EnableKeyword(vertexMode.ToString());
			NiParent.Material.EnableKeyword(lightingMode.ToString());
			//NiParent.Material.EnableKeyword()
		}
	}
}