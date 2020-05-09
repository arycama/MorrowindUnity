namespace Nif
{
	using System.IO;
	using UnityEngine;

	class NiParticleSystemController : NiTimeController
	{
		private float speed, speedRandom, verticalDirection, verticalAngle, horizontalDirection, horizontalAngle, size, emitStartTime, emitStopTime, emitRate, lifetime, lifetimeRandom, unknownFloat13;
		private int unknownByte, emitFlags, unknownShort2, unknownInt1, unknownInt2, unknownShort3, numParticles, numValid, unknownLink, unknownLink2, trailer;

		private int emitter;
		private Ref<NiParticleModifier> particleExtra;

		private Color unknownColor;
		private Particle[] particles;
		private Vector3 unknownNormal, startRandom;

		public NiParticleSystemController(NiFile niFile) : base(niFile)
		{
			speed = niFile.Reader.ReadSingle();
			speedRandom = niFile.Reader.ReadSingle() * 0.5f;
			verticalDirection = niFile.Reader.ReadSingle();
			verticalAngle = niFile.Reader.ReadSingle();
			horizontalDirection = niFile.Reader.ReadSingle();
			horizontalAngle = niFile.Reader.ReadSingle();
			unknownNormal = niFile.Reader.ReadVector3();
			unknownColor = niFile.Reader.GetReadColor4();
			size = niFile.Reader.ReadSingle() * 2;
			emitStartTime = niFile.Reader.ReadSingle();
			emitStopTime = niFile.Reader.ReadSingle();
			unknownByte = niFile.Reader.ReadByte();
			emitRate = niFile.Reader.ReadSingle();
			lifetime = niFile.Reader.ReadSingle();
			lifetimeRandom = niFile.Reader.ReadSingle();
			emitFlags = niFile.Reader.ReadInt16();
			startRandom = niFile.Reader.ReadVector3();
			emitter = niFile.Reader.ReadInt32();
			unknownShort2 = niFile.Reader.ReadInt16();
			unknownFloat13 = niFile.Reader.ReadSingle();
			unknownInt1 = niFile.Reader.ReadInt32();
			unknownInt2 = niFile.Reader.ReadInt32();
			unknownShort3 = niFile.Reader.ReadInt16();
			numParticles = niFile.Reader.ReadInt16();
			numValid = niFile.Reader.ReadInt16();

			particles = new Particle[numParticles];
			for (var i = 0; i < particles.Length; i++)
			{
				particles[i] = new Particle(niFile);
			}

			unknownLink = niFile.Reader.ReadInt32();
			particleExtra = new Ref<NiParticleModifier>(niFile);
			unknownLink2 = niFile.Reader.ReadInt32();
			trailer = niFile.Reader.ReadByte();
		}

		public override void ProcessNiObject(NiObjectNet niObject)
		{
			// Place this in the same location as the emitter
			var emitter = niFile.NiObjects[this.emitter].GameObject;
			emitter.layer = LayerMask.NameToLayer("Default"); // Emitter layer might be set to hidden, so enable it

			var particleSystem = emitter.AddComponent<ParticleSystem>();
			var particleSystemRenderer = emitter.GetComponent<ParticleSystemRenderer>();
			particleSystemRenderer.sharedMaterial = niObject.Material;

			// This sets the alpha of the vertex to be transparency
			// Should probably just use a different material, as we want to support soft particles anyway
			particleSystemRenderer.sharedMaterial.EnableKeyword("VERTEX_DIFFUSE");
			particleSystemRenderer.sharedMaterial.EnableKeyword("VERTEX_ALPHA");

			// Duration
			var main = particleSystem.main;
			main.startColor = unknownColor;

			particleSystem.Stop();
			main.duration = stopTime - startTime; // Don't set while system is playaing?
			particleSystem.Play();

			main.startSize = size;
			main.startLifetime = lifetime;

			main.maxParticles = numParticles;

			// Speed and Direction
			var velocityOverLifetime = particleSystem.velocityOverLifetime;
			var vertDir = verticalAngle * (speed + speedRandom);

			velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(-vertDir, vertDir);
			velocityOverLifetime.y = new ParticleSystem.MinMaxCurve((speed - speedRandom), (speed + speedRandom));
			velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(-vertDir, vertDir);
			velocityOverLifetime.enabled = true;
			main.startSpeed = 0;

			// Disabling as we want to handle direciton seperately
			var shape = particleSystem.shape;
			shape.enabled = false;

			// Rate
			var emission = particleSystem.emission;
			emission.rateOverTime = new ParticleSystem.MinMaxCurve(emitRate);

			if (particleExtra.Target != null)
			{
				particleExtra.Target.ProcessParticleSystem(particleSystem);
			}
		}

		private class Particle
		{
			private Vector3 velocity, unknownVector;
			private float lifetime, lifespan, timestamp;
			private int unknownShort, vertexID;

			public Particle(NiFile niFile)
			{
				velocity = niFile.Reader.ReadVector3();
				unknownVector = niFile.Reader.ReadVector3();
				lifetime = niFile.Reader.ReadSingle();
				lifespan = niFile.Reader.ReadSingle();
				timestamp = niFile.Reader.ReadSingle();
				unknownShort = niFile.Reader.ReadInt16();
				vertexID = niFile.Reader.ReadInt16();
			}
		}
	}
}