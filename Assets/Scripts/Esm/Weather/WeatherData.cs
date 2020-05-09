using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class WeatherData
{
	[SerializeField]
	private byte[] weatherChances;

	public WeatherData(System.IO.BinaryReader reader, int size)
	{
		weatherChances = reader.ReadBytes(size);
	}

	public IReadOnlyList<byte> WeatherChances => weatherChances;
}