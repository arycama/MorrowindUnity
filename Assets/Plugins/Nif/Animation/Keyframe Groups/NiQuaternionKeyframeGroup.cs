using System;
using UnityEngine;

namespace Nif
{
	class NiQuaternionKeyframeGroup : NiKeyframeGroup<Quaternion>
	{
		public NiQuaternionKeyframeGroup(int length, KeyType interpolation, System.IO.BinaryReader reader) : base(length, interpolation, reader) { }

		protected override int AnimCurveCount { get { return 4; } }

		protected override Quaternion GetValue()
		{
			return reader.ReadQuaternion();
		}

		protected override void SetLinearCurves(int current, int previous, int next)
		{
			var inTangent = CalculateLinearTangent(Keyframes[previous], Keyframes[current]);
			var outTangent = CalculateLinearTangent(Keyframes[current], Keyframes[next]);

			animationKeyframes[0][current] = new Keyframe(Keyframes[current].time, Keyframes[current].value.x, inTangent.x, outTangent.x);
			animationKeyframes[1][current] = new Keyframe(Keyframes[current].time, Keyframes[current].value.y, inTangent.y, outTangent.y);
			animationKeyframes[2][current] = new Keyframe(Keyframes[current].time, Keyframes[current].value.z, inTangent.z, outTangent.z);
			animationKeyframes[3][current] = new Keyframe(Keyframes[current].time, Keyframes[current].value.w, inTangent.w, outTangent.w);
		}

		protected override Quaternion CalculateLinearTangent(NiKeyframe<Quaternion> index, NiKeyframe<Quaternion> toIndex)
		{
			float num = index.time - toIndex.time;
			if (Mathf.Approximately(num, 0f))
			{
				return Quaternion.identity;
			}

			return new Quaternion
			(
				(index.value.x - toIndex.value.x) / num,
				(index.value.y - toIndex.value.y) / num,
				(index.value.z - toIndex.value.z) / num,
				(index.value.w - toIndex.value.w) / num
			);
		}

		protected override void SetQuadraticCurves(int index)
		{
			animationKeyframes[0][index] = new Keyframe(Keyframes[index].time, Keyframes[index].value.x, Keyframes[index].inTangent.x * Mathf.Deg2Rad, Keyframes[index].outTangent.x * Mathf.Deg2Rad);
			animationKeyframes[1][index] = new Keyframe(Keyframes[index].time, Keyframes[index].value.y, Keyframes[index].inTangent.y * Mathf.Deg2Rad, Keyframes[index].outTangent.y * Mathf.Deg2Rad);
			animationKeyframes[2][index] = new Keyframe(Keyframes[index].time, Keyframes[index].value.z, Keyframes[index].inTangent.z * Mathf.Deg2Rad, Keyframes[index].outTangent.z * Mathf.Deg2Rad);
			animationKeyframes[3][index] = new Keyframe(Keyframes[index].time, Keyframes[index].value.w, Keyframes[index].inTangent.w * Mathf.Deg2Rad, Keyframes[index].outTangent.w * Mathf.Deg2Rad);
		}

		protected override void SetTbcCurves(int current, int previous, int next)
		{
			var inTangent = CalculateLinearTangent(Keyframes[previous], Keyframes[current]);
			var outTangent = CalculateLinearTangent(Keyframes[current], Keyframes[next]);

			animationKeyframes[0][current] = new Keyframe(Keyframes[current].time, Keyframes[current].value.x, inTangent.x, outTangent.x);
			animationKeyframes[1][current] = new Keyframe(Keyframes[current].time, Keyframes[current].value.y, inTangent.y, outTangent.y);
			animationKeyframes[2][current] = new Keyframe(Keyframes[current].time, Keyframes[current].value.z, inTangent.z, outTangent.z);
			animationKeyframes[3][current] = new Keyframe(Keyframes[current].time, Keyframes[current].value.w, inTangent.w, outTangent.w);
		}

		protected override void SetConstantCurves(int index)
		{
			animationKeyframes[0][index] = new Keyframe(Keyframes[index].time, Keyframes[index].value.x, float.PositiveInfinity, float.PositiveInfinity);
			animationKeyframes[1][index] = new Keyframe(Keyframes[index].time, Keyframes[index].value.y, float.PositiveInfinity, float.PositiveInfinity);
			animationKeyframes[2][index] = new Keyframe(Keyframes[index].time, Keyframes[index].value.z, float.PositiveInfinity, float.PositiveInfinity);
			animationKeyframes[3][index] = new Keyframe(Keyframes[index].time, Keyframes[index].value.w, float.PositiveInfinity, float.PositiveInfinity);
		}
	}
}