#pragma warning disable 0108

using System.Collections;
using System.Collections.Generic;
using Esm;
using UnityEngine;

public class LightFlicker : MonoBehaviour
{
	[SerializeField]
	private LightFlickerMode lightFlickerMode;
    private float baseIntensity;
    private float direction;
    private float intensity;
    private readonly float randomSpeed;
    private Light light;

	private void Awake()
	{
		light = GetComponent<Light>();
		baseIntensity = light.intensity;

		// Set the starting intensity to a random value, so not all lights are on the exact same intensity
		intensity = Random.Range(baseIntensity * 0.5f, baseIntensity);
		direction = Random.Range(0, 1) * 2 - 1;
	}

	public void Initialize(LightFlags lightFlags)
	{
		if (lightFlags.HasFlag(LightFlags.Flicker))
		{
			lightFlickerMode = LightFlickerMode.Flicker;
		}
		else if (lightFlags.HasFlag(LightFlags.FlickerSlow))
		{
			lightFlickerMode = LightFlickerMode.FlickerSlow;
		}
		else if (lightFlags.HasFlag(LightFlags.Pulse))
		{
			lightFlickerMode = LightFlickerMode.Pulse;
		}
		else if (lightFlags.HasFlag(LightFlags.PulseSlow))
		{
			lightFlickerMode = LightFlickerMode.PulseSlow;
		}
	}

	private void Update()
	{
		//return;
		// Calculate intensity based on light mode
		switch (lightFlickerMode)
		{
			case LightFlickerMode.Flicker:
				intensity = intensity + Mathf.Sin(baseIntensity * Time.deltaTime * direction);
				break;
			case LightFlickerMode.FlickerSlow:
				intensity = intensity + Mathf.Sin(baseIntensity * Time.deltaTime * direction);
				break;
			case LightFlickerMode.Pulse:
				intensity = intensity + Mathf.Sin(baseIntensity * Time.deltaTime * direction);
				break;
			case LightFlickerMode.PulseSlow:
				intensity = intensity + Mathf.Sin(baseIntensity * Time.deltaTime * direction);
				break;
		}

		// Clamp intensity
		if (intensity > baseIntensity)
		{
			direction = -1;
		}
		else if (intensity < baseIntensity * 0.5f)
		{
			direction = 1;
		}

		// Apply to light
		light.intensity = intensity;
	}
}

public enum LightFlickerMode
{
	Flicker,
	FlickerSlow,
	Pulse,
	PulseSlow
};