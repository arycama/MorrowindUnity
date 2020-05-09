using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu]
public class WeatherSettings : ScriptableObject
{
	[Header("Colors")]
	[SerializeField]
	private Gradient skyGradient;

	[SerializeField]
	private Gradient fogGradient;

	[SerializeField]
	private Gradient ambientGradient;

	[SerializeField]
	private Gradient sunGradient;

	[Header("Data")]
	[SerializeField]
	private AnimationCurve landFogDepth;

	[SerializeField]
	private float windSpeed;

	[SerializeField]
	private float cloudSpeed;

	[SerializeField]
	private bool glareView;

	[SerializeField]
	private Texture2D cloudTexture;

	[SerializeField]
	private string ambientLoopSoundID;

	public void Create(WeatherType weatherType, Material material)
	{
		var section = $"Weather {weatherType}";

		// Create the gradients
		skyGradient = GetGradient(section, "Sky");
		fogGradient = GetGradient(section, "Fog");
		ambientGradient = GetGradient(section, "Ambient");
		sunGradient = GetGradient(section, "Sun");

		// Sky needs alpha keys for fading to night sky too
		skyGradient.alphaKeys = new GradientAlphaKey[]
		{
			new GradientAlphaKey(1, 19f / 24f),
			new GradientAlphaKey(0, 21f / 24f),
			new GradientAlphaKey(0, 4f / 24f),
			new GradientAlphaKey(1, 6f / 24f)
		};

		// Based on the following settings, plus sunrise/sunset times
		//Stars Post - Sunset Start = 1
		//Stars Pre-Sunrise Finish = 2
		//Stars Fading Duration = 2

		// Initializing some settings
		cloudTexture = BsaFileReader.LoadTexture("textures/" + IniManager.GetString(section, "Cloud Texture")) as Texture2D;
		material.mainTexture = cloudTexture;

		RenderSettings.sun.transform.eulerAngles = new Vector3(90, 270, 0);
		RenderSettings.sun.shadows = LightShadows.Soft;

		RenderSettings.ambientMode = AmbientMode.Flat;

		RenderSettings.fog = true;
		RenderSettings.fogMode = FogMode.Linear;

		RenderSettings.fogEndDistance = Camera.main.farClipPlane;
		RenderSettings.fogStartDistance = RenderSettings.fogEndDistance * (1 - IniManager.GetFloat(section, $"Land Fog Day Depth"));
	}

	private Gradient GetGradient(string section, string key)
	{
		var gradient = new Gradient();
		var colorKeys = new List<GradientColorKey>();

		// All times are normalized from 0-1, where 0 = midnight, 0.5 = midday, then 1 = midnight again
		var transitionDelta = IniManager.GetFloat(section, "Transition Delta");

		// Transition delta is said to be in realtime, but lets make it independant of current time scale
		var secondsPerDay = (60 * 60 * 24) / 30; // 2880, number of realtime seconds in a MW day
		transitionDelta = (1f / transitionDelta) / secondsPerDay;

		var sunriseColor = IniManager.GetColor(section, $"{key} Sunrise Color");
		var dayColor = IniManager.GetColor(section, $"{key} Day Color");
		var nightColor = IniManager.GetColor(section, $"{key} Night Color");
		var sunsetColor = IniManager.GetColor(section, $"{key} Sunset Color");

		// Sunrise
		var sunriseTime = IniManager.GetFloat("Weather", "Sunrise Time") / 24;
		var sunriseDuration = IniManager.GetFloat("Weather", "Sunrise Duration") / 24;
		var preSunriseTime = IniManager.GetFloat("Weather", $"{key} Pre-Sunrise Time") / 24;
		var postSunriseTime = IniManager.GetFloat("Weather", $"{key} Post-Sunrise Time") / 24;

		// Night/Sunrise/Day transition
		var sunriseStart = sunriseTime - preSunriseTime;
		var sunriseStop = sunriseTime + sunriseDuration + postSunriseTime;

		colorKeys.Add(new GradientColorKey(nightColor, sunriseStart));
		colorKeys.Add(new GradientColorKey(sunriseColor, sunriseStart + transitionDelta));

		colorKeys.Add(new GradientColorKey(sunriseColor, sunriseStop));
		colorKeys.Add(new GradientColorKey(dayColor, sunriseStop + transitionDelta));

		// Day/sunset/night transition
		var sunsetTime = IniManager.GetFloat("Weather", "Sunset Time") / 24;
		var sunsetDuration = IniManager.GetFloat("Weather", "Sunset Duration") / 24;
		var preSunsetTime = IniManager.GetFloat("Weather", $"{key} Pre-Sunset Time") / 24;
		var postSunsetTime = IniManager.GetFloat("Weather", $"{key} Post-Sunset Time") / 24;

		var sunsetStart = sunsetTime - preSunsetTime;
		var sunsetStop = sunsetTime + sunsetDuration + postSunsetTime;

		colorKeys.Add(new GradientColorKey(dayColor, sunsetStart));
		colorKeys.Add(new GradientColorKey(sunsetColor, sunsetStart + transitionDelta));

		colorKeys.Add(new GradientColorKey(sunsetColor, sunsetStop));
		colorKeys.Add(new GradientColorKey(nightColor, sunsetStop + transitionDelta));

		gradient.colorKeys = colorKeys.ToArray();
		return gradient;
	}

	public void UpdateWeather(float time)
	{
		Shader.SetGlobalColor("_SkyColor", skyGradient.Evaluate(time));
		Camera.main.backgroundColor = fogGradient.Evaluate(time);

		// Sun glare
		// RenderSettings.sun.GetComponent<LensFlare>().brightness = IniManager.GetFloat(weatherName, "Glare View");

		// Sun Light
		RenderSettings.sun.color = sunGradient.Evaluate(time);

		// Ambient Light
		RenderSettings.ambientLight = ambientGradient.Evaluate(time);

		// Calculate Fog
		RenderSettings.fogColor = fogGradient.Evaluate(time);
	}
}