using System.IO;
using UnityEngine;

namespace Nif
{
	abstract class NiParticleModifier : NiObject
	{
		protected Ref<NiParticleModifier> nextModifier;
		protected Ref<NiTimeController> controller;

		public NiParticleModifier(NiFile niFile) : base(niFile)
		{
			nextModifier = new Ref<NiParticleModifier>(niFile);
			controller = new Ref<NiTimeController>(niFile);
		}

		public virtual void ProcessParticleSystem(ParticleSystem particleSystem)
		{
			if(nextModifier.Target != null)
			{
				nextModifier.Target.ProcessParticleSystem(particleSystem);
			}
		}
	}
}