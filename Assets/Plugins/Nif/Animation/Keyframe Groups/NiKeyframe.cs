using System;

namespace Nif
{
	[Serializable]
	class NiKeyframe<T>
	{
		public float time, tension, bias, continuity;
		public T value, inTangent, outTangent;

		public NiKeyframe(float time, T value)
		{
			this.time = time;
			this.value = value;
		}

		public NiKeyframe(float time, T value, float tension, float bias, float continuity)
		{
			this.time = time;
			this.value = value;
			this.tension = tension;
			this.bias = bias;
			this.continuity = continuity;
		}

		public NiKeyframe(float time, T value, T inTangent, T outTangent)
		{
			this.time = time;
			this.value = value;
			this.inTangent = inTangent;
			this.outTangent = outTangent;
		}
	}
}