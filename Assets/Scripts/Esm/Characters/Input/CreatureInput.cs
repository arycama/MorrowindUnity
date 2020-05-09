#pragma warning disable 0108

using UnityEngine;

public class CreatureInput : NpcInput
{
	public CreatureFlags CreatureFlags { get; set; }

	protected override void OnHit(GameObject attacker)
	{
		Target = attacker;
		NextState = new AttackCreatureState();
	}
}