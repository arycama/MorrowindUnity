using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Esm;
using UnityEngine;
using UnityEngine.Rendering;

public class WeatherManager : Singleton<WeatherManager>
{
	const int SecondsPerDay = 86400, SecondsPerHour = 3600;
	const int SecondsPerMinute = 60, MinutesPerHour = 60, HoursPerDay = 24, DaysPerWeek = 7, MonthsPerYear = 12;
	const int sunriseSeconds = 6 * SecondsPerHour, sunsetSeconds = 18 * SecondsPerHour;
	const int midDaySeconds = SecondsPerDay / 2;

	// Todo: Initialize from Globals
	public float TimeScale = 30;
	const float SecondsPerDayRecip = 1f / SecondsPerDay;

	[SerializeField]
	private GameObject rain;

	[SerializeField]
	private string skyboxPath = "sky_clouds_01.nif";

	[SerializeField]
	private string nightSkyPath = "sky_night_01.nif";

	[SerializeField]
	private string sunTexture = "tx_Sun_05.dds";

	[SerializeField]
	private Vector3 sunAxis = new Vector3(360, 0, 0);

	[SerializeField]
	private Vector3 sunRotation = new Vector3(-90, -90, 0);

	[SerializeField]
	private float minimumTimeBetweenEnvironmentalSounds;

	[SerializeField]
	private float maximumTimeBetweenEnvironmentalSounds;

	[SerializeField]
	private WeatherSettings currentWeatherSettings;

	private TimeOfDay timeOfDay = TimeOfDay.Day;
	private WeatherType weatherType = WeatherType.Cloudy;

	[SerializeField, Repeat(60)]
	private float seconds;

	[SerializeField, Repeat(60)]
	private int minute = 0;

	[SerializeField, Repeat(24)]
	private int hour = 9;

	[SerializeField, Repeat(365)]
	private int day;

	[SerializeField, Repeat(12)]
	private int month = 7;

	[SerializeField]
	private Material skyboxMaterial;

	private Mesh skyboxMesh;
	private bool shouldUpdate;

	private float SecondsOfDay => seconds + minute * SecondsPerMinute + hour * SecondsPerHour;

	private void OnEnable()
	{
		RenderPipelineManager.beginCameraRendering += OnCameraPreCull;
		CellManager.OnFinishedLoadingCells += SwitchCell;

		shouldUpdate = true;

        // Night Sky
        //var nightNif = new Nif.NiFile(nightSkyPath);
        //var nightGo = nightNif.CreateGameObject();

        //nightCommandBuffer = new CommandBuffer();
        //var nightMeshes = nightGo.GetComponentsInChildren<MeshFilter>();
        //var nightMeshRenderers = nightGo.GetComponentsInChildren<MeshRenderer>();
        //for (var i = 0; i < nightMeshes.Length; i++)
        //{
        //	if (i > 3)
        //		continue;

        //	var material = nightMeshRenderers[i].sharedMaterial;
        //	material.shader = MaterialManager.Instance.NightSkyShader;

        //	var mesh = nightMeshes[i].sharedMesh;
        //	nightCommandBuffer.DrawMesh(mesh, Matrix4x4.identity, material);
        //}

        //Camera.main.AddCommandBuffer(CameraEvent.AfterForwardOpaque, nightCommandBuffer);
        //Destroy(nightGo);

        // Load skybox
        var reader = BsaFileReader.LoadArchiveFileData($"meshes\\{skyboxPath}");
		var nif = new Nif.NiFile(reader);
		var go = nif.CreateGameObject(Camera.main.transform);

		var renderer = go.GetComponentInChildren<Renderer>();

		skyboxMaterial = new Material(MaterialManager.Instance.SkyShader) { enableInstancing = true };
		//var skyCommandBuffer = new CommandBuffer();
		skyboxMesh = go.GetComponentInChildren<MeshFilter>().sharedMesh;
		//skyCommandBuffer.DrawMesh(skyboxMesh, Matrix4x4.TRS(new Vector3(0, -15, 0), Quaternion.identity, Vector3.one), skyboxMaterial);

		//Camera.main.AddCommandBuffer(CameraEvent.BeforeSkybox, skyCommandBuffer);
		if(!Application.isPlaying)
		{
			DestroyImmediate(go);
		}
		else
		{
			Destroy(go);
		}

		minimumTimeBetweenEnvironmentalSounds = IniManager.GetFloat("Weather", "Minimum Time Between Environmental Sounds");
		maximumTimeBetweenEnvironmentalSounds = IniManager.GetFloat("Weather", "Maximum Time Between Environmental Sounds");
	}

	private void OnCameraPreCull(ScriptableRenderContext context, Camera camera)
	{
		if (!shouldUpdate)
			return;

        var localMatrix = Matrix4x4.TRS(camera.transform.position, Quaternion.identity, Vector3.one * camera.farClipPlane / 1000f);
		Graphics.DrawMesh(skyboxMesh, localMatrix, skyboxMaterial, 0, camera);
	}

	private void OnDisable()
	{
        RenderPipelineManager.beginCameraRendering -= OnCameraPreCull;
		CellManager.OnFinishedLoadingCells -= SwitchCell;
	}

	private void SwitchCell(CellRecord cell)
	{
        shouldUpdate = !cell.CellData.IsInterior;

		if (cell.CellData.IsInterior)
		{
			cell.AmbientData.SetRenderSettings();
			rain.SetActive(false);

			StopCoroutine(PlayEnvironmentalSounds(cell.Region));
		}
		else
		{
			var weatherChances = cell.Region.WeatherData.WeatherChances;

			// Calculate weather chances 
			var maxChance = weatherChances.Sum((b) => b);
			var value = Random.Range(0, maxChance);

			// Get a max value in the range of all the weather types, and choose a type
			for(var i = 0; i < weatherChances.Count; i++)
			{
				if(value < weatherChances[i])
				{
					weatherType = (WeatherType)i;
					break;
				}
				else
				{
					value -= weatherChances[i];
				}
			}

			SetWeatherSettings(weatherType);
			StartCoroutine(PlayEnvironmentalSounds(cell.Region));
		}
	}

	private void Update()
	{
		if (!shouldUpdate)
			return;

		//if(Random.value > 0.9999f)
		//{
		//	weatherType = (WeatherType)Random.Range(1, 8);
		//	transitionCoroutine = StartCoroutine(UpdateWeatherSettings());
		//}


		//// Update the current time of day. Eventually this might be handled in a seperate time class
		UpdateTime(Time.deltaTime * TimeScale);

		//// Set shader global (Will only be used if timescale clouds is on. 
		var time = SecondsPerDayRecip * SecondsOfDay;
		//Shader.SetGlobalFloat("_TimeOfDay", time);

		//// Update sun rotation
		RenderSettings.sun.transform.eulerAngles = sunRotation + sunAxis * time;

		//// A nice fade in/out equation (y = 1 - ((x - 12)/ 6) ^ 2);
		////var intensity = (SecondsOfDay - midDaySeconds) / sunriseSeconds;
		////sun.shadowStrength = 1 - intensity * intensity;

		currentWeatherSettings?.UpdateWeather(time);
	}

	private void UpdateTime(float time)
	{
		seconds += time;

		// Check if minute needs to change
		if (seconds < SecondsPerMinute)
		{
			return;
		}

		// Increase the current minute, reset seconds
		minute++;
		seconds -= SecondsPerMinute;

		// Check if hour needs to change
		if (minute < MinutesPerHour)
		{
			return;
		}

		// Increase current hour, reset minutes
		hour++;
		minute -= MinutesPerHour;

		// Check if day needs to change
		if (hour < HoursPerDay)
		{
			return;
		}

		// Increase the day, reset hours
		day++;
		hour -= HoursPerDay;
	}

	private void SetWeatherSettings(WeatherType weatherType)
	{
		currentWeatherSettings = ScriptableObject.CreateInstance<WeatherSettings>();
		currentWeatherSettings.name = weatherType.ToString();
		currentWeatherSettings.Create(weatherType, skyboxMaterial);

		currentWeatherSettings.UpdateWeather(SecondsOfDay / SecondsPerDay);

		// Rain
		if (weatherType == WeatherType.Rain || weatherType == WeatherType.Thunderstorm)
		{
			rain.SetActive(true);
		}
		else
		{
			rain.SetActive(false);
		}
	}

	private IEnumerator PlayEnvironmentalSounds(Region region)
	{
		while (isActiveAndEnabled)
		{
			var maxChance = region.Sounds.Sum((s) => s.Chance);
			var value = Random.Range(0, maxChance);
			var sound = region.Sounds.Last().Sound;

			// Get a max value in the range of all the weather types, and choose a type
			for (var i = 0; i < region.Sounds.Count; i++)
			{
				if (value < region.Sounds[i].Chance)
				{
					sound = region.Sounds[i].Sound;
					break;
				}
				else
				{
					value -= region.Sounds[i].Chance;
				}
			}

			sound.PlaySound2D();

			var delay = Random.Range(minimumTimeBetweenEnvironmentalSounds, maximumTimeBetweenEnvironmentalSounds) + sound.AudioClip.length;
			yield return new WaitForSeconds(delay);
		}
	}
}