#pragma warning disable 0108

using System.Collections;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class MusicManager : MonoBehaviour
{
	[SerializeField]
	private string[] exploreMusic;

	[SerializeField]
	private string[] battleMusic;

	private readonly Coroutine musicLoopRoutine;

	private AudioSource audio;

	private void Awake()
	{
		audio = GetComponent<AudioSource>();
	}


	private IEnumerator Start()
	{
		while (isActiveAndEnabled)
		{
			// Pick a random song and play it 
			var index = Random.Range(0, exploreMusic.Length);
			var path = exploreMusic[index];

			audio.clip = SoundManager.StreamAudio("Explore/" + path);
			audio.Play();
			yield return new WaitForSeconds(audio.clip.length);
		}
	}
}