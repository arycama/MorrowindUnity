using System;
using UnityEngine;

namespace Nif
{
	[Serializable]
	class NiMorphData : NiObject
	{
		private bool relativeTargets;
		private Morph[] morphs;

		public NiMorphData(NiFile niFile) : base(niFile)
		{
			var morphCount = niFile.Reader.ReadInt32();
			var vertexCount = niFile.Reader.ReadInt32();
			relativeTargets = niFile.Reader.ReadByte() != 0;

			morphs = new Morph[morphCount];
			for (var i = 0; i < morphs.Length; i++)
			{
				morphs[i] = new Morph(niFile, vertexCount);
			}
		}

		public override void Process()
		{
			var mesh = NiParent.Mesh;

			foreach (var morph in morphs)
			{
				var count = mesh.blendShapeCount;
				mesh.AddBlendShapeFrame(count.ToString(), 1, morph.Vectors, null, null);
			}

			var path = GetRelativePath();

			if(niFile.animationCache == null)
			{
				return;
			}

			// Go through each clip
			foreach (var clip in niFile.animationCache.Values)
			{
				for (var i = 0; i < morphs.Length; i++)
				{
					var keys = morphs[i].Keys;

					if(keys == null)
					{
						continue;
					}

					var animationCurves = keys.GetAnimationCurves(clip.Start, clip.Stop);

					if(animationCurves == null)
					{
						continue;
					}

					clip.clip.SetCurve(path, typeof(SkinnedMeshRenderer), "blendShape." + i, animationCurves[0]);
				}
			}
		}

		/// <summary>
		/// Contains Blend Shape vertex positions and animation frames
		/// </summary>
		[Serializable]
		private class Morph
		{
			public Morph(NiFile niFile, int numVertices)
			{
				var length = niFile.Reader.ReadInt32();
				var interpolation = (KeyType)niFile.Reader.ReadInt32();

				if (length != 0)
				{
					Keys = new NiFloatKeyframeGroup(length, interpolation, niFile.Reader);
				}
				
				Vectors = new Vector3[numVertices];
				for (var i = 0; i < Vectors.Length; i++)
				{
					Vectors[i] = niFile.Reader.ReadVector3();
				}
			}

			public NiFloatKeyframeGroup Keys { get; private set; }
			public Vector3[] Vectors { get; private set; }
		}
	}
}