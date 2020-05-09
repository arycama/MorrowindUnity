using UnityEngine;

public class RainWeatherSettings : WeatherSettings
{
	[Header("Rain")]
	[SerializeField]
	private bool usingPrecip;

	[SerializeField]
	private int rainDiameter;

	[SerializeField]
	private int rainHeightMin;

	[SerializeField]
	private int rainHeightMax;

	[SerializeField]
	private float rainThreshold;

	[SerializeField]
	private int rainEntranceSpeed;

	[SerializeField]
	private string rainLoopSoundID;

	[SerializeField]
	private int maxRaindrops;
}