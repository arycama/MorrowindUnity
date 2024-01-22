using System.IO;
using UnityEngine;

namespace Nif
{
	class NiParticleColorModifier : NiParticleModifier
	{
		private readonly Ref<NiColorData> data;

		public NiParticleColorModifier(NiFile niFile) : base(niFile)
		{
			data = new Ref<NiColorData>(niFile);
		}

		public override void ProcessParticleSystem(ParticleSystem particleSystem)
		{
			base.ProcessParticleSystem(particleSystem);

			if(data.Target == null)
			{
				return;
			}

			var colorOverLifetime = particleSystem.colorOverLifetime;
			colorOverLifetime.enabled = true;

			var color = colorOverLifetime.color;
			color.mode = ParticleSystemGradientMode.Gradient;

			var gradient = data.Target.GetGradient();
			color.gradient = gradient;

			colorOverLifetime.color = color;
		}

		public override void ProcessNiObject(NiObjectNet niObject)
		{

		}
	}
}