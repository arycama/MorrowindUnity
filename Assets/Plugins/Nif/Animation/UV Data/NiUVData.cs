using System;
using UnityEngine;

namespace Nif
{
	[Serializable]
	class NiUVData : NiObject
	{
		private AnimationClip clip;

		public NiUVData(NiFile niFile) : base(niFile)
		{
			clip = new AnimationClip()
			{
				frameRate = 15,
				legacy = true,
				name = "animation",
				wrapMode = WrapMode.Loop
			};

			// U Translation
			var length = niFile.Reader.ReadInt32();
			if (length > 0)
			{
				var interpolation = (KeyType)niFile.Reader.ReadInt32();
				var uTranslation = new NiFloatKeyframeGroup(length, interpolation, niFile.Reader);
				var animationCurves = uTranslation.GetAnimationCurves();
				clip.SetCurve(string.Empty, typeof(Renderer), "material._MainTex_ST.z", animationCurves[0]);
			}

			// V Translation
			length = niFile.Reader.ReadInt32();
			if (length > 0)
			{
				var interpolation = (KeyType)niFile.Reader.ReadInt32();
				var vTranslation = new NiFloatKeyframeGroup(length, interpolation, niFile.Reader);
				var animationCurves = vTranslation.GetAnimationCurves();
				clip.SetCurve(string.Empty, typeof(Renderer), "material._MainTex_ST.w", animationCurves[0]);
			}

			// U Tiling
			length = niFile.Reader.ReadInt32();
			if (length > 0)
			{
				var interpolation = (KeyType)niFile.Reader.ReadInt32();
				var uTiling = new NiFloatKeyframeGroup(length, interpolation, niFile.Reader);
				var animationCurves = uTiling.GetAnimationCurves();
				clip.SetCurve(string.Empty, typeof(Renderer), "material._MainTex_ST.x", animationCurves[0]);
			}
			else
			{
				var curve = new AnimationCurve();
				curve.AddKey(0, 1);
				curve.AddKey(clip.length, 1);
				clip.SetCurve(string.Empty, typeof(Renderer), "material._MainTex_ST.x", curve);
			}

			// V Tiling
			length = niFile.Reader.ReadInt32();
			if (length > 0)
			{
				var interpolation = (KeyType)niFile.Reader.ReadInt32();
				var vTiling = new NiFloatKeyframeGroup(length, interpolation, niFile.Reader);
				var animationCurves = vTiling.GetAnimationCurves();
				clip.SetCurve(string.Empty, typeof(Renderer), "material._MainTex_ST.y", animationCurves[0]);
			}
			else
			{
				var curve = new AnimationCurve();
				curve.AddKey(0, 1);
				curve.AddKey(clip.length, 1);
				clip.SetCurve(string.Empty, typeof(Renderer), "material._MainTex_ST.y", curve);
			}
		}

		public override void ProcessNiObject(NiObjectNet niObject)
		{
			var animation = niObject.GameObject.AddComponent<Animation>();

			animation.AddClip(clip, clip.name);
			animation.clip = clip;
		}
	}
}