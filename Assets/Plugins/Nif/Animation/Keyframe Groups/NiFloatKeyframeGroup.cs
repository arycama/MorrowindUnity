using System;
using UnityEngine;

namespace Nif
{
	class NiFloatKeyframeGroup : NiKeyframeGroup<float>
	{
		public NiFloatKeyframeGroup(int length, KeyType interpolation, System.IO.BinaryReader reader) : base(length, interpolation, reader) { }

		protected override int AnimCurveCount { get { return 1; } }

		protected override float GetValue()
		{
			return reader.ReadSingle();
		}

		protected override void SetLinearCurves(int current, int previous, int next)
		{
			var inTangent = CalculateLinearTangent(Keyframes[previous], Keyframes[current]);
			var outTangent = CalculateLinearTangent(Keyframes[current], Keyframes[next]);

			animationKeyframes[0][current] = new Keyframe(Keyframes[current].time, Keyframes[current].value, inTangent, outTangent);
		}

		protected override float CalculateLinearTangent(NiKeyframe<float> index, NiKeyframe<float> toIndex)
		{
			float num = index.time - toIndex.time;
			if (Mathf.Approximately(num, 0f))
			{
				return 0;
			}

			return (index.value - toIndex.value) / num;
		}

		protected override void SetQuadraticCurves(int index)
		{
			animationKeyframes[0][index] = new Keyframe(Keyframes[index].time, Keyframes[index].value, Keyframes[index].inTangent * Mathf.Deg2Rad, Keyframes[index].outTangent * Mathf.Deg2Rad);
		}

		protected override void SetTbcCurves(int current, int previous, int next)
		{
			// Time deltas
			var wo = Keyframes[current].time - Keyframes[previous].time;
			var wq = Keyframes[next].time - Keyframes[current].time;

			// Value deltas
			var oDelta = Keyframes[current].value - Keyframes[previous].value;
			var qDelta = Keyframes[next].value - Keyframes[current].value;

			// Speed control
			var fo = (wo + wo) / (wo + wq);
			var fq = (wq + wq) / (wo + wq);

			// The "incoming" (backward) Tangent equation:
			var m1 = (1 - Keyframes[current].tension) * (1 - Keyframes[current].continuity) * (1 + Keyframes[current].bias) * 0.5f;

			// The "outgoing" (forward) Tangent equation:
			var m2 = (1 - Keyframes[current].tension) * (1 + Keyframes[current].continuity) * (1 - Keyframes[current].bias) * 0.5f;

			// Factors for herp()
			var a = (m1 * oDelta +m2 * qDelta) *fo;
			var b = (m2 * oDelta +m1 * qDelta) *fq;

			animationKeyframes[0][current] = new Keyframe(Keyframes[current].time, Keyframes[current].value, a, b);
		}

		protected override void SetConstantCurves(int index)
		{
			animationKeyframes[0][index] = new Keyframe(Keyframes[index].time, Keyframes[index].value, float.PositiveInfinity, float.PositiveInfinity);
		}
	}
}