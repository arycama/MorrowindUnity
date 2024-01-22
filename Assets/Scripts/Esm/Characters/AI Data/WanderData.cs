using System;
using System.IO;
using UnityEngine;
using Random = UnityEngine.Random;

[Serializable]
public class WanderData
{
	[SerializeField]
	private int distance;

	[SerializeField]
	private int duration;

	[SerializeField]
	private int timeOfDay;

	[SerializeField]
	private byte[] idles;

	private readonly int unknown;

	public WanderData(System.IO.BinaryReader reader)
	{
		distance = reader.ReadInt16();
		duration = reader.ReadInt16();
		timeOfDay = reader.ReadByte();
		idles = reader.ReadBytes(8);
		unknown = reader.ReadByte();
	}

	public int Distance => distance;
	public int Duration => duration;

	public int GetIdle()
	{
		var idlePicked = 0;
		var randomMax = 0f;

		for (var i = 0; i < idles.Length; i++)
		{
			var probability =  GameSetting.Get("fIdleChanceMultiplier").FloatValue * idles[i];
			var random = Random.Range(0, 100f);

			if (random < probability && random > randomMax)
			{
				randomMax = random;
				idlePicked = i + 2;
			}
		}

		return idlePicked;
	}
}