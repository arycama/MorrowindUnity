using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class SoundRecord : EsmRecord
{
	[SerializeField]
	private AudioClip audioClip;

	[SerializeField]
	private SoundRecordData data;

	public AudioClip AudioClip => audioClip;

	public override void Initialize(BinaryReader reader, RecordHeader header)
	{
		while (reader.BaseStream.Position < header.DataEndPos)
		{
			var type = (SubRecordType)reader.ReadInt32();
			var size = reader.ReadInt32();

			switch (type)
			{
				case SubRecordType.Id:
					name = reader.ReadString(size);
					break;
				case SubRecordType.Name:
					audioClip = SoundManager.LoadAudio(reader.ReadString(size));
					break;
				case SubRecordType.Data:
					data = new SoundRecordData(reader);
					break;
			}
		}
	}

	public void PlaySound2D()
	{
		var audioGameObject = new GameObject(name);

		var audioSource = audioGameObject.AddComponent<AudioSource>();
		audioSource.clip = audioClip;

		audioSource.volume = data.Volume;

		audioSource.Play();

		// Add a bit of time so the sound doesn't get cut off
		Destroy(audioGameObject, audioClip.length + 1);
	}

	public void PlaySoundAtPoint(Vector3 point)
	{
		var audioGameObject = new GameObject(name);
		audioGameObject.transform.position = point;

		var audioSource = audioGameObject.AddComponent<AudioSource>();
		audioSource.clip = audioClip;

		audioSource.spatialBlend = 1;
		audioSource.maxDistance = data.MaxRange;
		audioSource.minDistance = data.MinRange;

		audioSource.volume = data.Volume;

		audioSource.Play();

		// Add a bit of time so the sound doesn't get cut off
		Destroy(audioGameObject, audioClip.length + 1);
	}

	public void PlaySoundFromAudioSource(AudioSource audioSource, bool loop = false)
	{
		audioSource.maxDistance = data.MaxRange;
		audioSource.minDistance = data.MinRange;

		if (loop)
		{
			audioSource.clip = audioClip;
			audioSource.loop = loop;
			audioSource.volume = data.Volume;

			audioSource.Play();
		}
		else
		{
			audioSource.PlayOneShot(audioClip, data.Volume);
		}
	}

	public AudioSource AddAudioSource(GameObject gameObject, bool isLooping = false, bool play = false)
	{
		var audio = gameObject.AddComponent<AudioSource>();
		audio.clip = audioClip;

		audio.spatialBlend = 1;

		audio.volume = data.Volume;
		audio.minDistance = data.MinRange;
		audio.maxDistance = data.MaxRange;

		audio.loop = isLooping;

		if (play)
		{
			audio.Play();
		}

		return audio;
	}
}