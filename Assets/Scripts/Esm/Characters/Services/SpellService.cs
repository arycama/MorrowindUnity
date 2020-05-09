using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SpellService : CharacterService
{
	public override string ServiceName => "Spells";

	protected override bool CloseOnPurchase => false;
	protected override bool CloseUIOnPurchase => false;

	protected override string Description => "Select spell to buy";

	public static SpellService Create(GameObject gameObject) => gameObject.AddComponent<SpellService>();

	protected override IEnumerable<ServiceOption> GetOptions(Character player, Character npc)
	{
		var availableSpells = npc.Spells.Except(player.Spells);
		foreach (var spell in availableSpells)
		{
			Func<bool> callback = () => { player.Spells.Add(spell); return true; };
			var cost = spell.Data.SpellCost * GameSetting.Get("fSpellValueMult").FloatValue;
			yield return new ServiceOption(spell.FullName, callback, cost);
		}
	}
}