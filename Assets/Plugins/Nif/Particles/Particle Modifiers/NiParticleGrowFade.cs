using System.IO;
using UnityEngine;

namespace Nif
{
	class NiParticleGrowFade : NiParticleModifier
	{
		private float grow, fade;

		public NiParticleGrowFade(NiFile niFile) : base(niFile)
		{
			grow = niFile.Reader.ReadSingle();
			fade = niFile.Reader.ReadSingle();
		}

		public override void ProcessParticleSystem(ParticleSystem particleSystem)
		{
			base.ProcessParticleSystem(particleSystem);

			var sizeOverLifetime = particleSystem.sizeOverLifetime;
			sizeOverLifetime.enabled = true;

			var life = particleSystem.main.startLifetime.constant;

			var sizeCurve = new AnimationCurve();

			if(grow > 0)
			{
				sizeCurve.AddKey(grow / life, 1);
			}

			if(fade != 0)
			{
				sizeCurve.AddKey(1 - fade / life, 1);
				sizeCurve.AddKey(1, 0);
			}

			sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1, sizeCurve);
		}

		public override void ProcessNiObject(NiObjectNet niObject)
		{

		}
	}
}