using System;
using UnityEngine;

namespace Nif
{
	[Serializable]
	class NiVector3KeyframeGroup : NiKeyframeGroup<Vector3>
	{
		public NiVector3KeyframeGroup(int length, KeyType interpolation, System.IO.BinaryReader reader) : base(length, interpolation, reader) { }

		protected override int AnimCurveCount { get { return 3; } }

		protected override Vector3 GetValue()
		{
			return reader.ReadVector3();
		}

		
		protected override void SetLinearCurves(int current, int previous, int next)
		{
			var inTangent = CalculateLinearTangent(Keyframes[previous], Keyframes[current]);
			var outTangent = CalculateLinearTangent(Keyframes[current], Keyframes[next]);

			animationKeyframes[0][current] = new Keyframe(Keyframes[current].time, Keyframes[current].value.x, inTangent.x, outTangent.x);
			animationKeyframes[1][current] = new Keyframe(Keyframes[current].time, Keyframes[current].value.y, inTangent.y, outTangent.y);
			animationKeyframes[2][current] = new Keyframe(Keyframes[current].time, Keyframes[current].value.z, inTangent.z, outTangent.z);
		}

		protected override Vector3 CalculateLinearTangent(NiKeyframe<Vector3> index, NiKeyframe<Vector3> toIndex)
		{
			float num = index.time - toIndex.time;
			if (Mathf.Approximately(num, 0f))
			{
				return Vector3.zero;
			}

			return new Vector3
			(
				(index.value.x - toIndex.value.x) / num, 
				(index.value.y - toIndex.value.y) / num, 
				(index.value.z - toIndex.value.z) / num
			);
		}

		protected override void SetQuadraticCurves(int index)
		{
			animationKeyframes[0][index] = new Keyframe(Keyframes[index].time, Keyframes[index].value.x, Keyframes[index].inTangent.x * Mathf.Deg2Rad, Keyframes[index].outTangent.x * Mathf.Deg2Rad);
			animationKeyframes[1][index] = new Keyframe(Keyframes[index].time, Keyframes[index].value.y, Keyframes[index].inTangent.y * Mathf.Deg2Rad, Keyframes[index].outTangent.y * Mathf.Deg2Rad);
			animationKeyframes[2][index] = new Keyframe(Keyframes[index].time, Keyframes[index].value.z, Keyframes[index].inTangent.z * Mathf.Deg2Rad, Keyframes[index].outTangent.z * Mathf.Deg2Rad);
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
			var a = (m1 * oDelta + m2 * qDelta) * fo;
			var b = (m2 * oDelta + m1 * qDelta) * fq;

			animationKeyframes[0][current] = new Keyframe(Keyframes[current].time, Keyframes[current].value.x, a.x, b.x);
			animationKeyframes[1][current] = new Keyframe(Keyframes[current].time, Keyframes[current].value.y, a.y, b.y);
			animationKeyframes[2][current] = new Keyframe(Keyframes[current].time, Keyframes[current].value.z, a.z, b.z);
		}

		protected override void SetConstantCurves(int index)
		{
			animationKeyframes[0][index] = new Keyframe(Keyframes[index].time, Keyframes[index].value.x, float.PositiveInfinity, float.PositiveInfinity);
			animationKeyframes[1][index] = new Keyframe(Keyframes[index].time, Keyframes[index].value.y, float.PositiveInfinity, float.PositiveInfinity);
			animationKeyframes[2][index] = new Keyframe(Keyframes[index].time, Keyframes[index].value.z, float.PositiveInfinity, float.PositiveInfinity);
		}
	}
}