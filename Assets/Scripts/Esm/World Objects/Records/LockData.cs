using System;
using Esm;
using UnityEngine;
using Random = UnityEngine.Random;

[Serializable]
public class LockData
{
	[SerializeField]
	private int lockLevel;

	[SerializeField]
	private SpellRecord trap;

	[SerializeField]
	private MiscItemRecord key;

	public LockData(int lockLevel, SpellRecord trap, MiscItemRecord key)
	{
		this.lockLevel = lockLevel;
		this.trap = trap;
		this.key = key;
	}

	public void DisplayLockInfo(InfoPanel infoPanel)
	{
		switch (lockLevel)
		{
			case 0:
				break;
			case -1:
				infoPanel.AddText($"Lock Level: Unlocked");
				break;
			default:
				infoPanel.AddText($"Lock Level: {lockLevel}");
				break;
		}

		if (trap == null)
		{
			return;
		}

		infoPanel.AddText("Trapped");
	}

	// Returns true if the door is locked, and plays the lock sound
	public bool CheckLock(GameObject target, string lockSound)
	{
		if (lockLevel < 1)
		{
			return false;
		}

		if (key != null)
		{
			var inventory = target.GetComponent<IInventory>();
			if (inventory != null && inventory.Items.ContainsKey(key))
			{
				Unlock(target.transform.position);
				return true;
			}
		}

		var sound = Record.GetRecord<SoundRecord>(lockSound);
		sound.PlaySoundAtPoint(target.transform.position);

		return true;
	}

	public bool Unlock(Vector3 position, float chance = 100)
	{
		// Formula is ((Security + (Agility/5) + (Luck/10)) * Lockpick multiplier * (0.75 + 0.5 * Current Fatigue/Maximum Fatigue) - Lock Level)%
		if(Random.Range(0, 100) > chance)
		{
			var lockSound = Record.GetRecord<SoundRecord>("Open Lock Fail");
			lockSound.PlaySoundAtPoint(position);
			return false;
		}

		lockLevel = -1;

		var unlockSound = Record.GetRecord<SoundRecord>("Open Lock");
		unlockSound.PlaySoundAtPoint(position);
		return true;
	}
}