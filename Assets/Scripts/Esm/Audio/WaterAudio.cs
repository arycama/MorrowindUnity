#pragma warning disable 0108

using System.Collections;
using System.Collections.Generic;
using Esm;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class WaterAudio : MonoBehaviour
{
	[SerializeField]
	private string audioName = "Water Layer";

	private readonly float maxVolume = 0.7f;
	private AudioSource audio;

	private void Start()
	{
		var soundRecord = Record.GetRecord<SoundRecord>(audioName);
		audio = soundRecord.AddAudioSource(gameObject);
		audio.Play();
	}

	private void Update()
	{
		//audio.volume = maxVolume * (audio.maxDistance - Water.Instance.WaterHeight) / (audio.maxDistance - audio.minDistance);
	}
}