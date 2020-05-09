using System;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nif
{
	class NiAlphaProperty : NiProperty
	{
		private int flags, zWrite;
		private bool alphaBlend, alphaTest;
		private BlendModes sourceBlendMode, destinationBlendMode;
		private TestModes alphaTestMode;
		private byte cutoff;

		[Flags]
		private enum Flags
		{
			AlphaBlend = 0x0001,
			AlphaTest = 0x0100,
			NoSort = 0x1000
		}

		public NiAlphaProperty(NiFile niFile) : base(niFile)
		{
			flags = niFile.Reader.ReadInt16();
			cutoff = niFile.Reader.ReadByte();

			// flags into useful values
			alphaBlend = (flags & 1) == 1;
			sourceBlendMode = (BlendModes)(BitConverter.GetBytes(flags >> 1)[0] & 15);
			destinationBlendMode = (BlendModes)(BitConverter.GetBytes(flags >> 5)[0] & 15);
			alphaTest = (flags & 9) == 1;
			alphaTestMode = (TestModes)(BitConverter.GetBytes(flags >> 10)[0] & 15);
			zWrite = (flags & 13) == 1 ? 1 : 0;
		}

		public override void Process()
		{
			base.Process();

			var par = NiParent;
			if(par == null)
			{
				return;
				throw new Exception(Name);
			}
			var material = NiParent.Material;

			material.SetFloat("_ZWrite", zWrite);
			material.SetFloat("_Cutoff", cutoff);

			material.SetInt("_SrcBlend", (int)ConvertBlendMode(sourceBlendMode));
			material.SetInt("_DstBlend", (int)ConvertBlendMode(destinationBlendMode));

			material.renderQueue = (int)RenderQueue.Transparent;
			material.SetOverrideTag("RenderType", "Transparent");

			if (alphaBlend)
			{
				material.EnableKeyword("_ALPHABLEND_ON");
			}

			if (alphaTest)
			{
				material.EnableKeyword("_ALPHATEST_ON");
			}
		}

		private BlendMode ConvertBlendMode(BlendModes blendMode)
		{
			switch (blendMode)
			{
				case BlendModes.One:
					return BlendMode.One;
				case BlendModes.Zero:
					return BlendMode.Zero;
				case BlendModes.SrcColor:
					return BlendMode.SrcColor;
				case BlendModes.OneMinusSrcColor:
					return BlendMode.OneMinusSrcColor;
				case BlendModes.DstColor:
					return BlendMode.DstColor;
				case BlendModes.OneMinusDstColor:
					return BlendMode.OneMinusDstColor;
				case BlendModes.SrcAlpha:
					return BlendMode.SrcAlpha;
				case BlendModes.OneMinusSrcAlpha:
					return BlendMode.OneMinusSrcAlpha;
				case BlendModes.DstAlpha:
					return BlendMode.DstAlpha;
				case BlendModes.OneMinusDstAlpha:
					return BlendMode.OneMinusDstAlpha;
				case BlendModes.SrcAlphaSaturate:
					return BlendMode.SrcAlphaSaturate;
				default:
					return BlendMode.Zero;
			}
		}

		private enum BlendModes
		{
			One,
			Zero,
			SrcColor,
			OneMinusSrcColor,
			DstColor,
			OneMinusDstColor,
			SrcAlpha,
			OneMinusSrcAlpha,
			DstAlpha,
			OneMinusDstAlpha,
			SrcAlphaSaturate
		}

		private enum TestModes
		{
			GL_ALWAYS,
			GL_LESS,
			GL_EQUAL,
			GL_LEQUAL,
			GL_GREATER,
			GL_NOTEQUAL,
			GL_GEQUAL,
			GL_NEVER
		}
	}
}