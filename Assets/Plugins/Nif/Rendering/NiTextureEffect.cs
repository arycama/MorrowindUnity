using System;
using System.IO;
using UnityEngine;

namespace Nif
{
	[Serializable]
	class NiTextureEffect : NiDynamicEffect
	{
		public Matrix4x4 modelProjectionMatrix;
		public Vector3 modelProjectionTransform;
		public TexFilterMode textureFiltering;
		public TexClampMode textureClamping;
		private readonly EffectType textureType;
		private readonly CoordGenType coordinateGenerationType;
		public int sourceTexture;
		public byte clippingPlane;
		public Vector3 unknownVector;
		public float unknownFloat;
		public short PS2L;
		public short PS2K;
		public short unknownShort;

		public NiTextureEffect(NiFile niFile) : base(niFile)
		{
			modelProjectionMatrix = niFile.Reader.ReadMatrix();
			modelProjectionTransform = niFile.Reader.ReadVector3();
			textureFiltering = (TexFilterMode)niFile.Reader.ReadInt32();
			textureClamping = (TexClampMode)niFile.Reader.ReadInt32();
			textureType = (EffectType)niFile.Reader.ReadInt32();
			coordinateGenerationType = (CoordGenType)niFile.Reader.ReadInt32();
			sourceTexture = niFile.Reader.ReadInt32();
			clippingPlane = niFile.Reader.ReadByte();
			unknownVector = niFile.Reader.ReadVector3();
			unknownFloat = niFile.Reader.ReadSingle();
			PS2L = niFile.Reader.ReadInt16();
			PS2K = niFile.Reader.ReadInt16();
			unknownShort = niFile.Reader.ReadInt16();
		}

		private enum CoordGenType
		{
			WorldParallel,
			WorldPerspective,
			SphereMap,
			SpecularCubeMap,
			DiffuseCubeMap
		}

		private enum EffectType
		{
			ProjectedLight,
			ProjectedShadow,
			EnvironmentMap,
			FogMap
		}
	}
}