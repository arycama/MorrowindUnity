using System;
using System.IO;
using System.Collections.Generic;
using NAudio.Wave;
using UnityEngine;

public class SoundManager
{
	private static string soundPath = "C:/Program Files (x86)/Steam/SteamApps/common/Morrowind/Data Files/Sound/";
	private static string musicPath = "C:/Program Files (x86)/Steam/SteamApps/common/Morrowind/Data Files/Music/";

	private static Dictionary<string, AudioClip> AudioClipCache = new Dictionary<string, AudioClip>();

	// Creates a Unity AudioClip from an audio file
	public static AudioClip LoadAudio(string filePath)
	{
		AudioClip audioClip;
		if(AudioClipCache.TryGetValue(filePath, out audioClip))
		{
			return audioClip;
		}

		try
		{
			using (var reader = new AudioFileReader(soundPath + filePath))
			{
				var length = reader.Length / 4;
				var samples = new float[length];

				reader.Read(samples, 0, samples.Length);

				var name = Path.GetFileNameWithoutExtension(filePath);
				var channels = reader.WaveFormat.Channels;
				var frequency = reader.WaveFormat.SampleRate;

				audioClip = AudioClip.Create(name, (int)length, channels, frequency, false);
				audioClip.SetData(samples, 0);
				AudioClipCache.Add(filePath, audioClip);

				return audioClip;
			}
		}
		catch (Exception)
		{
			return null;
		}
	}

	private static AudioFileReader reader;

	public static AudioClip StreamAudio(string filePath)
	{
		reader = new AudioFileReader(musicPath + filePath);
		
		var length = reader.Length / 4;
		//var samples = new float[length];

		//reader.Read(samples, 0, samples.Length);

		var name = Path.GetFileNameWithoutExtension(filePath);
		var channels = reader.WaveFormat.Channels;
		var frequency = reader.WaveFormat.SampleRate;

		var audioClip = AudioClip.Create(name, (int)length, channels, frequency, true, OnAudioRead, OnAudioSetPosition);
		//audioClip.SetData(samples, 0);

		return audioClip;
	}

	private static void OnAudioRead(float[] data)
	{
		reader.Read(data, 0, data.Length);
	}

	private static void OnAudioSetPosition(int newPosition)
	{
		reader.Position = newPosition;
	}
}