using System;
using UnityEngine;

namespace Nif
{
	[Serializable]
	class NiKeyframeData : NiObject
	{
		private NiKeyframe<Vector3>[] eulerKeys;
		private KeyType rotationInterpolation;

		private NiQuaternionKeyframeGroup quaternionKeys;
		private NiFloatKeyframeGroup scaleKeyframes;
		private NiVector3KeyframeGroup translationKeyframes;
		public NiKeyframeData(NiFile niFile) : base(niFile)
		{
			LoadRotationKeys();
			LoadTranslationKeys();
			LoadScaleKeys();
		}

		private void LoadRotationKeys()
		{
			// For euler, use m_LocalEuler
			var length = niFile.Reader.ReadInt32();
			if (length != 0)
			{
				rotationInterpolation = (KeyType)niFile.Reader.ReadInt32();
				if (rotationInterpolation == KeyType.EulerAngles)
				{
					throw new NotImplementedException(rotationInterpolation.ToString());
					//eulerKeys = new NiKeyframe<Vector3>[length];
				}
				else
				{
					quaternionKeys = new NiQuaternionKeyframeGroup(length, rotationInterpolation, niFile.Reader);
				}
			}
		}

		private void LoadTranslationKeys()
		{
			var length = niFile.Reader.ReadInt32();
			if (length > 0)
			{
				var interpolation = (KeyType)niFile.Reader.ReadInt32();
				translationKeyframes = new NiVector3KeyframeGroup(length, interpolation, niFile.Reader);
			}
		}

		private void LoadScaleKeys()
		{
			var length = niFile.Reader.ReadInt32();
			if (length > 0)
			{
				var interpolation = (KeyType)niFile.Reader.ReadInt32();
				scaleKeyframes = new NiFloatKeyframeGroup(length, interpolation, niFile.Reader);
			}
		}

		// Go through each animation node
		public override void Process()
		{
			// Some objects may have Keyframe data without text keys. Maybe these should be added into the animation curve of whatever object they are loaded onto? Ignore unless we're having animation issues and can't find any other causes
			if(niFile.animationCache == null)
			{
				return;
			}

			// Go through each clip
			var path = GetRelativePath();
			foreach (var clip in niFile.animationCache.Values)
			{
				if(quaternionKeys != null)
				{
					var animationCurves = quaternionKeys.GetAnimationCurves(clip.Start, clip.Stop);

					// These checks need to sample the curve at the start and stop if the length is 0, and add a start and stop keyfame if needed
					if(animationCurves[0].length > 1)
					{
						clip.clip.SetCurve(path, typeof(Transform), "localRotation.x", animationCurves[0]);
						clip.clip.SetCurve(path, typeof(Transform), "localRotation.y", animationCurves[1]);
						clip.clip.SetCurve(path, typeof(Transform), "localRotation.z", animationCurves[2]);
						clip.clip.SetCurve(path, typeof(Transform), "localRotation.w", animationCurves[3]);
					}

				}

				if(translationKeyframes != null)
				{
					var animationCurves = translationKeyframes.GetAnimationCurves(clip.Start, clip.Stop);
					if (animationCurves[0].length > 1)
					{
						clip.clip.SetCurve(path, typeof(Transform), "localPosition.x", animationCurves[0]);
						clip.clip.SetCurve(path, typeof(Transform), "localPosition.y", animationCurves[1]);
						clip.clip.SetCurve(path, typeof(Transform), "localPosition.z", animationCurves[2]);
					}
				}

				if(scaleKeyframes != null)
				{
					var animationCurves = scaleKeyframes.GetAnimationCurves(clip.Start, clip.Stop);
					if (animationCurves[0].length > 1)
					{
						clip.clip.SetCurve(path, typeof(Transform), "localScale.x", animationCurves[0]);
						clip.clip.SetCurve(path, typeof(Transform), "localScale.y", animationCurves[0]);
						clip.clip.SetCurve(path, typeof(Transform), "localScale.z", animationCurves[0]);
					}
				}
			}
		}
	}
}