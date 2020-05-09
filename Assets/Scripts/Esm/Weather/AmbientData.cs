using System;
using UnityEngine;

[Serializable]
public class AmbientData
{
	[SerializeField]
	private Color32 ambient;

	[SerializeField]
	private Color32 sunColor;

	[SerializeField]
	private Color32 fogColor;

	[SerializeField]
	private float fogDensity;

	public AmbientData(System.IO.BinaryReader reader)
	{
		ambient = reader.ReadColor32();
		sunColor = reader.ReadColor32();
		fogColor = reader.ReadColor32();
		fogDensity = reader.ReadSingle();
	}

	public void SetRenderSettings()
	{
		RenderSettings.sun.color = sunColor;
		RenderSettings.sun.transform.eulerAngles = new Vector3(45, 135, 0);
		RenderSettings.sun.shadows = LightShadows.None;

		RenderSettings.ambientLight = ambient;
		RenderSettings.fogColor = fogColor;

		RenderSettings.fog = true;
		RenderSettings.fogMode = FogMode.Linear;
		RenderSettings.fogEndDistance = Camera.main.farClipPlane;
		RenderSettings.fogStartDistance = RenderSettings.fogEndDistance * (1 - fogDensity);
	}
}