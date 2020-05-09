using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Nif
{
	[Serializable]
	class NiVisData : NiObject
	{
		private NiByteKeyframeGroup keyframes;

		public NiVisData(NiFile niFile) : base(niFile)
		{
			var length = niFile.Reader.ReadInt32();
			keyframes = new NiByteKeyframeGroup(length, KeyType.Constant, niFile.Reader);
		}

		public override void Process()
		{
			var path = GetRelativePath();
			foreach (var clip in niFile.animationCache.Values)
			{
				var animationCurves = keyframes.GetAnimationCurves(clip.Start, clip.Stop);

				if(animationCurves == null)
				{
					continue;
				}

				clip.clip.SetCurve(path, typeof(Renderer), "m_Enabled", animationCurves[0]);
			}
		}
	}
}