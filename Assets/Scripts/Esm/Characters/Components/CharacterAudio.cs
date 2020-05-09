#pragma warning disable 0108

using Esm;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class CharacterAudio : MonoBehaviour
{
	[SerializeField]
	private SoundGenerator[] soundGenerators;

	private AudioSource audio;

	public static CharacterAudio Create(GameObject gameObject, string soundGeneratorName = "DEFAULT")
	{
		var component = gameObject.AddComponent<CharacterAudio>();

		var soundGenerators = new SoundGenerator[8];
		for(var i = 0; i < soundGenerators.Length; i++)
		{
			soundGenerators[i] = global::SoundGenerator.Get(soundGeneratorName, i);
		}

		component.soundGenerators = soundGenerators;

		return component;
	}

	private void Awake()
	{
		audio = GetComponent<AudioSource>();
		audio.spatialBlend = 1;
	}

	public void Sound(string name)
	{
		var sound = Record.GetRecord<SoundRecord>(name);
		sound.PlaySoundFromAudioSource(audio);
	}

	public void SoundGenerator(string name)
	{
		switch (name)
		{
			case "left":
			case "Left":
				soundGenerators[0].PlaySound(audio);
				break;
			case "right":
			case "Right":
				soundGenerators[1].PlaySound(audio);
				break;
			case "SwimLeft":
				soundGenerators[2].PlaySound(audio);
				break;
			case "SwimRight":
				soundGenerators[3].PlaySound(audio);
				break;
			case "moan":
			case "Moan":
				soundGenerators[4].PlaySound(audio);
				break;
			case "Roar":
				soundGenerators[5].PlaySound(audio);
				break;
			case "Scream":
				soundGenerators[6].PlaySound(audio);
				break;
			case "Land":
				soundGenerators[7].PlaySound(audio);
				break;
			default:
				throw new System.Exception(name);
		}
	}
}