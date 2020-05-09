#pragma warning disable 0108

using System;
using System.Collections;
using System.Collections.Generic;

using Esm;
using UnityEngine;
using Random = UnityEngine.Random;

[RequireComponent(typeof(AudioSource))]
public class CharacterVoice : MonoBehaviour
{
	const float GreetUpdateInterval = 0.5f;

	[SerializeField]
	private int greetDistanceMultiplier;

	[SerializeField]
	private float greetDistanceReset;

	[SerializeField]
	private float voiceIdleOdds;

	[SerializeField]
	private Transform headTransform;

	[SerializeField]
	private NpcRecord npcRecord;

	private AudioSource audio;
	
	private HashSet<Collider> greetedNpcs = new HashSet<Collider>();

	private float GreetDistance => npcRecord.AiData.Hello;

	public void Initialize(NpcRecord npcRecord)
	{
		greetDistanceMultiplier = GameSetting.Get("iGreetDistanceMultiplier").IntValue;
		greetDistanceReset = GameSetting.Get("fGreetDistanceReset").FloatValue;
		voiceIdleOdds = GameSetting.Get("fVoiceIdleOdds").FloatValue;

		this.npcRecord = npcRecord;
	}

	private void OnDrawGizmosSelected()
	{
		if(headTransform == null || npcRecord == null)
		{
			return;
		}

		Gizmos.DrawWireSphere(headTransform.position, GreetDistance * greetDistanceMultiplier);
	}

	private void Awake()
	{
		// Set initial audio settings
		audio = GetComponent<AudioSource>();
		audio.spatialBlend = 1;
		audio.minDistance = ( GameSetting.Get("fAudioVoiceDefaultMinDistance").FloatValue *  GameSetting.Get("fAudioMinDistanceMult").FloatValue);
		audio.maxDistance = ( GameSetting.Get("fAudioVoiceDefaultMaxDistance").FloatValue *  GameSetting.Get("fAudioMaxDistanceMult").FloatValue);
		audio.volume = 0.7f;

		// Need a reference to the head transform, so we can do raycats to check if the Npc can see the player
		// Should eventually use the character body component and get the head or something
		headTransform = transform.Find("Bip01/Bip01 Pelvis/Bip01 Spine/Bip01 Spine1/Bip01 Spine2/Bip01 Neck/Bip01 Head/Head"); // Ewwwwwwwwwwww
	}

	private IEnumerator Start()
	{
		// Do an intial overlap sphere so that any Npc's already in range of others don't greet eachother
		var colliders = Physics.OverlapSphere(transform.position, GreetDistance * greetDistanceMultiplier, LayerMask.GetMask("Npc"));
		foreach (var collider in colliders)
		{
			// Do a raycast to make sure the NPC is actually visible
			var direction = collider.bounds.center - headTransform.position;
			RaycastHit hit;
			if (!Physics.Raycast(headTransform.position, direction, out hit, GreetDistance * greetDistanceMultiplier, LayerMask.GetMask("Default", "Npc", "Raycast Only"))
				|| hit.collider != collider)
			{
				continue;
			}

			greetedNpcs.Add(collider);
		}

		while (isActiveAndEnabled)
		{
			yield return new WaitForSeconds(GreetUpdateInterval);

			// First, check if any of the previously-greeted Npc's have moved far enough away that they should be re-greeted if they get close. Add the greet distance to the rest ddistance, so that npcs with a long greet range don't re-greet players early
			greetedNpcs.RemoveWhere(col => Vector3.Distance(transform.position, col.transform.position) >= greetDistanceReset + GreetDistance * greetDistanceMultiplier);

			// Now periodically check for new Npcs, and if so, greet them
			colliders = Physics.OverlapSphere(headTransform.position, GreetDistance * greetDistanceMultiplier, LayerMask.GetMask("Npc"));
			foreach (var collider in colliders)
			{
				if (greetedNpcs.Contains(collider))
				{
					continue;
				}

				// Raycast to the NPC first, to make sure there's nothing in the way. (This may not work, as it will probably raycast from the foot position
				var direction = collider.bounds.center - headTransform.position;
				RaycastHit hit;
				if (!Physics.Raycast(headTransform.position, direction, out hit, GreetDistance * greetDistanceMultiplier, LayerMask.GetMask("Default", "Npc", "Raycast Only"))
					|| hit.collider != collider)
				{
					continue;
				}

				// Record this npc so we don't forget them
				greetedNpcs.Add(collider);

				// Get the Npc's NpcRecord
				var listener = collider.GetComponentInParent<DialogController>();
				if(listener == null)
				{
					continue;
				}

				// Play greeting for any new Npc's detected
				var audioClip = DialogRecord.GetDialogInfo(DialogType.Voice, GetComponent<Character>(), listener.GetComponent<Character>(), VoiceType.Hello.ToString());
				if(audio != null)
				{
					audio.PlayOneShot(audioClip.AudioClip);
				}
			}
		}
	}

	// Don't need to do this every frame. Should make it a coroutine like above
	private void Update()
	{
		var random = Random.Range(0, 10000);
		var chance = voiceIdleOdds * Time.deltaTime;
		if (random < chance)
		{
			var audioClip = DialogRecord.GetDialogInfo(DialogType.Voice, GetComponent<Character>(), null, VoiceType.Idle.ToString());
			if (audioClip != null && audio != null)
			{
				audio.PlayOneShot(audioClip.AudioClip);
			}
		}
	}
}