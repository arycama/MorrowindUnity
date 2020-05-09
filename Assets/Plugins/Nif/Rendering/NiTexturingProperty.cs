using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Nif
{
	class NiTexturingProperty : NiProperty
	{
		[SerializeField]
		private ApplyMode applyMode;

		private static Dictionary<string, Material> cache = new Dictionary<string, Material>();
		private TexDesc[] textureDescriptions;

		private static readonly string[] textures = new string[7]
		{
			"_MainTex",
			"_Dark",
			"_Detail",
			"_EmissionMap",
			"_Glow",
			"_BumpMap",
			"_Decal0"
		};

		public NiTexturingProperty(NiFile niFile) : base(niFile)
		{
			var flags = niFile.Reader.ReadInt16();

			applyMode = (ApplyMode)niFile.Reader.ReadInt32();
			var textureCount = niFile.Reader.ReadInt32();

			textureDescriptions = new TexDesc[textureCount];
			for(var i = 0; i < textureDescriptions.Length; i++)
			{
				var hasTexture = niFile.Reader.ReadInt32() != 0;
				if (hasTexture)
				{
					textureDescriptions[i] = new TexDesc(niFile);
				}
			}
		}

		public override void Process()
		{
			base.Process();
			Material = NiParent.Material;

			for(var i = 0; i < textureDescriptions.Length; i++)
			{
				if(textureDescriptions[i] == null)
				{
					continue;
				}

				var name = textureDescriptions[i].source.Target.FileName;

				Material material;
				if(cache.TryGetValue(name, out material))
				{
					Material = material;
					NiParent.Material = material;
				}
				else
				{
					NiParent.Material.SetTexture(textures[i], textureDescriptions[i].LoadTexture());
					NiParent.Material.name = name;
					cache.Add(name, NiParent.Material);
				}
			}
		}

		public override void ProcessNiObject(NiObjectNet niObject)
		{
			
		}

		private enum ApplyMode
		{
			APPLY_REPLACE,
			APPLY_DECAL,
			APPLY_MODULATE,
			APPLY_HILIGHT,
			APPLY_HILIGHT2
		}

		public class TexDesc
		{
			public Ref<NiSourceTexture> source;
			public TexClampMode clampMode;
			public TexFilterMode filterMode;
			public int UVSet;
			public short PS2L;
			public short PS2K;
			public short unknown1;

			public TexDesc(NiFile niFile)
			{
				source = new Ref<NiSourceTexture>(niFile);
				clampMode = (TexClampMode)niFile.Reader.ReadInt32();
				filterMode = (TexFilterMode)niFile.Reader.ReadInt32();
				UVSet = niFile.Reader.ReadInt32();
				PS2L = niFile.Reader.ReadInt16();
				PS2K = niFile.Reader.ReadInt16();
				unknown1 = niFile.Reader.ReadInt16();
			}

			public Texture LoadTexture()
			{
				var texture = source.Target.LoadTexture();
				return texture;
			}
		}
	}
}