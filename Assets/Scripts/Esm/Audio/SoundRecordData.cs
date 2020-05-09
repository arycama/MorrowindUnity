using System;
using UnityEngine;

[Serializable]
public class SoundRecordData
{
	[SerializeField]
	public float Volume;

	[SerializeField]
	public float MinRange;

	[SerializeField]
	public float MaxRange;

	public SoundRecordData(System.IO.BinaryReader reader)
	{
		Volume = reader.ReadByte() / 255f;
		MinRange = reader.ReadByte();
		MaxRange = reader.ReadByte();

		if (MinRange == 0)
		{
			MinRange = GameSetting.Get("fAudioDefaultMinDistance").FloatValue;
		}

		if (MaxRange == 0)
		{
			MaxRange =  GameSetting.Get("fAudioDefaultMaxDistance").FloatValue;
		}

		MinRange = (MinRange *  GameSetting.Get("fAudioMinDistanceMult").FloatValue);
		MaxRange = (MaxRange *  GameSetting.Get("fAudioMaxDistanceMult").FloatValue);
	}
}