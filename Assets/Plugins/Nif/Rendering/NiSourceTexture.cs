using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Nif
{
	class NiSourceTexture : NiTexture
	{
		private static readonly Dictionary<string, Texture> textureCache = new Dictionary<string, Texture>();

		private readonly byte useExternal;
		public string FileName { get; private set; }
		private readonly PixelLayout pixelLayout;
		private readonly MipMapFormat useMipMaps;
		private readonly AlphaFormat alphaFormat;
		private readonly byte isStatic;

		public NiSourceTexture(NiFile niFile) : base(niFile)
		{
			useExternal = niFile.Reader.ReadByte();
			FileName = niFile.Reader.ReadLengthPrefixedString();
			pixelLayout = (PixelLayout)niFile.Reader.ReadInt32();
			useMipMaps = (MipMapFormat)niFile.Reader.ReadInt32();
			alphaFormat = (AlphaFormat)niFile.Reader.ReadInt32();

			isStatic = niFile.Reader.ReadByte();
		}

		public Texture LoadTexture()
		{
			var path = "textures\\" + FileName;
			Texture texture;
			if(!textureCache.TryGetValue(path, out texture))
			{
				texture = BsaFileReader.LoadTexture(path);
				textureCache.Add(path, texture);
			}

			return texture;
		}

		public enum PixelLayout : uint
		{
			PIX_LAY_PALETTISED = 0,
			PIX_LAY_HIGH_COLOR_16 = 1,
			PIX_LAY_TRUE_COLOR_32 = 2,
			PIX_LAY_COMPRESSED = 3,
			PIX_LAY_BUMPMAP = 4,
			PIX_LAY_PALETTISED_4 = 5,
			PIX_LAY_DEFAULT = 6
		}

		public enum MipMapFormat : uint
		{
			MIP_FMT_NO = 0,
			MIP_FMT_YES = 1,
			MIP_FMT_DEFAULT = 2
		}

		public enum AlphaFormat : uint
		{
			ALPHA_NONE = 0,
			ALPHA_BINARY = 1,
			ALPHA_SMOOTH = 2,
			ALPHA_DEFAULT = 3
		}
	}
}