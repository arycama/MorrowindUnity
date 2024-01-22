using System;
using System.IO;
using UnityEngine;

namespace Nif
{
	class NiColorData : NiObject
	{
		private readonly int keyCount;
		private readonly KeyType interpolation;
		private readonly ColorKey[] keys;
		
		public NiColorData(NiFile niFile) : base(niFile)
		{
			keyCount = niFile.Reader.ReadInt32();
			interpolation = (KeyType)niFile.Reader.ReadInt32();

			keys = new ColorKey[keyCount];
			for (var i = 0; i < keys.Length; i++)
			{
				keys[i] = new ColorKey(niFile.Reader, interpolation);
			}
		}

		public override void ProcessNiObject(NiObjectNet niObject)
		{

		}

		public Gradient GetGradient()
		{
			var colorKeys = new GradientColorKey[keyCount];
			var alphaKeys = new GradientAlphaKey[keyCount];
			
			for (var i = 0; i < keyCount; i++)
			{
				colorKeys[i].color = keys[i].value;
				colorKeys[i].time = keys[i].time;
				alphaKeys[i].time = keys[i].time;
				alphaKeys[i].alpha = keys[i].value.a;
			}

			var gradient = new Gradient();
			gradient.SetKeys(colorKeys, alphaKeys);
			return gradient;
		}

		class ColorKey
		{
			public float time;
			public Color value, forward, backward;
			//public Tbc tbc;

			public ColorKey(System.IO.BinaryReader reader, KeyType keyType)
			{
				time = reader.ReadSingle();
				value = reader.GetReadColor4();

				if (keyType == KeyType.Quadratic)
				{
					forward = reader.GetReadColor4();
					backward = reader.GetReadColor4();
				}
				else if (keyType == KeyType.Tbc)
				{
					reader.ReadVector3();
					//tbc = new Tbc(reader);
				}
			}
		}
	}
}