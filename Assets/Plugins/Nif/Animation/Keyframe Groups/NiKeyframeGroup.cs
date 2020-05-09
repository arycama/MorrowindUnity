using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nif
{
	abstract class NiKeyframeGroup<T>
	{
		protected abstract T GetValue();

		protected System.IO.BinaryReader reader;
		public NiKeyframe<T>[] Keyframes { get; protected set; }
		protected Keyframe[][] animationKeyframes;

		private int length;
		private KeyType interpolation;

		public NiKeyframeGroup(int length, KeyType interpolation, System.IO.BinaryReader reader)
		{
			this.length = length;
			this.interpolation = interpolation;
			this.reader = reader;

			Keyframes = new NiKeyframe<T>[length];

			// Read all the keys
			for (var i = 0; i < length; i++)
			{
				var time = reader.ReadSingle();
				var value = GetValue();

				switch (interpolation)
				{
					case KeyType.Linear:
					case KeyType.Constant:
						Keyframes[i] = new NiKeyframe<T>(time, value);
						break;
					case KeyType.Quadratic:
						var forward = GetValue();
						var backward = GetValue();
						Keyframes[i] = new NiKeyframe<T>(time, value, forward, backward);
						break;
					case KeyType.Tbc:
						var tension = reader.ReadSingle();
						var bias = reader.ReadSingle();
						var continuity = reader.ReadSingle();
						Keyframes[i] = new NiKeyframe<T>(time, value, tension, bias, continuity);
						break;
					default:
						throw new NotImplementedException(interpolation.ToString());
				}
			}

			// After keys are read, they should be "processed" so tangents for quadratic/tbc can  be calculated
			animationKeyframes = new Keyframe[AnimCurveCount][];
			for (var i = 0; i < AnimCurveCount; i++)
			{
				animationKeyframes[i] = new Keyframe[Keyframes.Length];
			}

			// Set tangents for first frame 
			// Some animations only have one frame, do this to avoid errors
			if (Keyframes.Length == 1)
			{
				switch (interpolation)
				{
					case KeyType.Linear:
						SetLinearCurves(0, 0, 0);
						break;
					case KeyType.Quadratic:
						SetQuadraticCurves(0);
						break;
					case KeyType.Tbc:
						SetTbcCurves(0, 0, 0);
						break;
					case KeyType.Constant:
						SetConstantCurves(0);
						break;
					default:
						throw new NotImplementedException(interpolation.ToString());
				}

				return;
			}


			switch (interpolation)
			{
				case KeyType.Linear:
					SetLinearCurves(0, 0, 1);
					break;
				case KeyType.Quadratic:
					SetQuadraticCurves(0);
					break;
				case KeyType.Tbc:
					SetLinearCurves(0, 0, 1);
					break;
				case KeyType.Constant:
					SetConstantCurves(0);
					break;
				default:
					throw new NotImplementedException(interpolation.ToString());
			}

			// Don't set tangents here for first or last frame
			for (var i = 1; i < Keyframes.Length - 2; i++)
			{
				switch (interpolation)
				{
					case KeyType.Linear:
						SetLinearCurves(i, i - 1, i + 1);
						break;
					case KeyType.Quadratic:
						SetQuadraticCurves(i);
						break;
					case KeyType.Tbc:
						SetTbcCurves(i, i - 1, i + 1);
						break;
					case KeyType.Constant:
						SetConstantCurves(i);
						break;
					default:
						throw new NotImplementedException(interpolation.ToString());
				}
			}

			// Set tangents for last frame
			switch (interpolation)
			{
				case KeyType.Linear:
					SetLinearCurves(Keyframes.Length - 1, Keyframes.Length - 2, Keyframes.Length - 1);
					break;
				case KeyType.Quadratic:
					SetQuadraticCurves(Keyframes.Length - 1);
					break;
				case KeyType.Tbc:
					SetTbcCurves(Keyframes.Length - 1, Keyframes.Length - 2, Keyframes.Length - 1);
					break;
				case KeyType.Constant:
					SetConstantCurves(Keyframes.Length - 1);
					break;
				default:
					throw new NotImplementedException(interpolation.ToString());
			}
		}

		// Gets an animation curve for each property across the entire animation timeline
		public AnimationCurve[] GetAnimationCurves()
		{
			// Initialize the curves
			var keyframes = new List<Keyframe>[AnimCurveCount];
			for (var i = 0; i < AnimCurveCount; i++)
			{
				keyframes[i] = new List<Keyframe>();
			}

			for (var i = 0; i < this.Keyframes.Length; i++)
			{
				for (var j = 0; j < AnimCurveCount; j++)
				{
					var key = animationKeyframes[j][i];
					key.time = this.Keyframes[i].time;
					keyframes[j].Add(key);
				}
			}

			var animationCurves = new AnimationCurve[AnimCurveCount];
			for (var i = 0; i < animationCurves.Length; i++)
			{
				animationCurves[i] = new AnimationCurve(keyframes[i].ToArray());
			}

			return animationCurves;
		}

		public AnimationCurve[] GetAnimationCurves(float start, float stop)
		{
			// Initialize the curves
			var keyframes = new List<Keyframe>[AnimCurveCount];
			for (var i = 0; i < AnimCurveCount; i++)
			{
				keyframes[i] = new List<Keyframe>();
			}

			for (var i = 0; i < this.Keyframes.Length; i++)
			{
				if (this.Keyframes[i].time < start)
				{
					continue;
				}

				if (this.Keyframes[i].time > stop)
				{
					break;
				}

				var time = this.Keyframes[i].time - start;
				for (var j = 0; j < AnimCurveCount; j++)
				{
					var key = animationKeyframes[j][i];
					key.time = time;
					keyframes[j].Add(key);
				}
			}

			var animationCurves = new AnimationCurve[AnimCurveCount];
			for (var i = 0; i < animationCurves.Length; i++)
			{
				animationCurves[i] = new AnimationCurve(keyframes[i].ToArray());
			}

			return animationCurves;
		}

		protected abstract int AnimCurveCount { get; }

		protected abstract void SetLinearCurves(int current, int previous, int next);

		protected abstract void SetQuadraticCurves(int index);

		protected abstract void SetTbcCurves(int current, int previous, int next);

		protected abstract void SetConstantCurves(int index);

		protected abstract T CalculateLinearTangent(NiKeyframe<T> index, NiKeyframe<T> toIndex);
	}
}