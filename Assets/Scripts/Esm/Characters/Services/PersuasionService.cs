using System;
using System.Collections.Generic;
using Esm;
using UnityEngine;
using Random = UnityEngine.Random;
using static UnityEngine.Mathf;

public class PersuasionService : CharacterService
{
	public override string ServiceName => "Persuasion";

	public override bool IsRefusable => false;
	protected override bool UseCostCalculations => false;

	private float DieRollMult => GameSetting.Get("fPerDieRollMult").FloatValue;
	private float PerTempMult => GameSetting.Get("fPerTempMult").FloatValue;

	private int PerMinChance => GameSetting.Get("iPerMinChance").IntValue;
	private int PerMinChange => GameSetting.Get("iPerMinChange").IntValue;

	protected override string ButtonText => "Cancel";

	public static PersuasionService Create(GameObject gameObject) => gameObject.AddComponent<PersuasionService>();

	private float GetAdmireRating(Character character) => (character.ReputationTerm + character.LuckTerm + character.PersonalityTerm + character.GetSkill(CharacterSkill.sSkillSpeechcraft)) * character.FatigueTerm;

	private bool Admire(Character player, Character npc, int bribeMod = 0)
	{
		// This essentially makes the calculations more likely to move towards 50
		var disposition = npc.GetDisposition(player);
		var d = 1 - Abs(disposition - 50) * 0.02f;
		var target = d * (GetAdmireRating(player) - GetAdmireRating(npc) + 50);
		target = Max(PerMinChance, target);
		target -= Random.Range(0, 100);

		DialogRecord dialog;
		var change = (int)(DieRollMult * target);
		if (change >= 0)
		{
			change = Max(PerMinChange, change);
			dialog = DialogRecord.GetPersuasionDialog(player, npc, PersuasionResult.AdmireSuccess);
		}
		else
		{
			dialog = DialogRecord.GetPersuasionDialog(player, npc, PersuasionResult.AdmireFail);
		}

		// Update the disposition in the UI
		npc.DispositionMod += Clamp(change, -disposition, 100 - disposition);
		controller.SetDisposition(npc.GetDisposition(player));

		// Display the response
		controller.DisplayTopic(dialog);

		return change >= 0;
	}

	private bool Intimidate(Character player, Character npc)
	{
		// Common
		var disposition = npc.GetDisposition(player);
		var playerRating2 = GetAdmireRating(player) + player.LevelTerm;
		var npcRating2 = (npc.LevelTerm + npc.ReputationTerm + npc.LuckTerm + npc.PersonalityTerm + npc.GetSkill(CharacterSkill.sSkillSpeechcraft)) * npc.FatigueTerm;

		var d = 1 - 0.02f * Abs(disposition - 50);
		var target2 = d * (playerRating2 - npcRating2 + 50);

		// Specific
		target2 = Max(PerMinChance, target2);
		var roll = Random.Range(0, 100);
		var win = roll <= target2;

		var r = roll != target2 ? RoundToInt(target2 - roll) : 1;

		if(roll <= target2)
		{
			var s = (int)(r * DieRollMult * PerTempMult);
			var flee = Max(PerMinChange, s);
			var fight = Min(-PerMinChange, -s);
		}

		var c = -Abs((int)(r * DieRollMult));

		int x, y;
		if (win)
		{
			if (Abs(c) < PerMinChange)
			{
				x = 0;
				y = -PerMinChange; // bug, see comments?
			}
			else
			{
				x = -(int)(c * PerTempMult);
				y = c;
			}
		}
		else
		{
			x = (int)(c * PerTempMult);
			y = c;
		}

		var tempChange = x;
	
		// Clamp to 100
		var cappedDispositionChange = Clamp(disposition + tempChange, 0, 100) - disposition;

		// ANother clamp method?
		if(disposition + tempChange > 100)
		{
			cappedDispositionChange = 100 - disposition;
		}
		else if(disposition + tempChange < 0)
		{
			cappedDispositionChange = disposition;
		}

		if (win)
			npc.DispositionMod += (int)(cappedDispositionChange / PerTempMult);
		else
			npc.DispositionMod += y;

		// Update the disposition in the UI
		controller.SetDisposition(npc.GetDisposition(player));

		var dialog = DialogRecord.GetPersuasionDialog(player, npc, win ? PersuasionResult.IntimidateSuccess : PersuasionResult.IntimidateFail);

		// Display the response
		controller.DisplayTopic(dialog);

		return win;
	}

	private bool Taunt(Character player, Character npc)
	{
		var disposition = npc.GetDisposition(player);
		var d = 1 - 0.02f * Abs(disposition - 50);
		var target = d * (GetAdmireRating(player) - GetAdmireRating(npc) + 50);
		target = Max(PerMinChance, target);

		var roll = Random.Range(0, 100);
		var win = roll <= target;

		var change = Abs((int)(target - roll));
		var x = (int)(-change * DieRollMult);
		if (win)
		{
			var s = change * DieRollMult * PerTempMult;
			var flee = Min(-PerMinChange, (int)(-s));
			var fight = Max(PerMinChange, (int)(s));

			if(Abs(x) < PerMinChange)
				x = -PerMinChange;
		}

		// Update the disposition in the UI
		npc.DispositionMod += Clamp(x, -disposition, 100 - disposition);
		controller.SetDisposition(npc.GetDisposition(player));

		// Display the response
		var dialog = DialogRecord.GetPersuasionDialog(player, npc, win ? PersuasionResult.TauntSuccess : PersuasionResult.TauntFail);
		controller.DisplayTopic(dialog);

		return win;
	}

	private bool Bribe(Character player, Character npc, int amount)
	{
		var disposition = npc.GetDisposition(player);
		var d = 1 - 0.02f * Abs(disposition - 50);

		var playerRating = (player.GetSkill(CharacterSkill.sSkillMercantile) + player.LuckTerm + player.PersonalityTerm) * player.FatigueTerm;
		var npcRating = (npc.GetSkill(CharacterSkill.sSkillMercantile) + npc.ReputationTerm + npc.LuckTerm + npc.PersonalityTerm) * npc.FatigueTerm;

		float bribeMod;
		if (amount == 10)
			bribeMod = GameSetting.Get("fBribe10Mod").FloatValue;
		else if (amount == 100)
			bribeMod = GameSetting.Get("fBribe100Mod").FloatValue;
		else
			bribeMod = GameSetting.Get("fBribe1000Mod").FloatValue;

		var target3 = Max(PerMinChance, d * (playerRating - npcRating + 50) + bribeMod);
		var roll = Random.Range(0, 100);

		var change = (int)((target3 - roll) * DieRollMult);
		DialogRecord dialog;
		if (roll <= target3)
		{
			change = Max(PerMinChange, change);
			dialog = DialogRecord.GetPersuasionDialog(player, npc, PersuasionResult.BribeSuccess);
		}
		else
		{
			dialog = DialogRecord.GetPersuasionDialog(player, npc, PersuasionResult.BribeFail);
		}

		// Update the disposition in the UI
		npc.DispositionMod += Clamp(change, -disposition, 100 - disposition);
		controller.SetDisposition(npc.GetDisposition(player));

		// Display the response
		controller.DisplayTopic(dialog);

		return roll <= target3;
	}

	protected override IEnumerable<ServiceOption> GetOptions(Character player, Character npc)
	{
		yield return new ServiceOption("Admire", () => Admire(player, npc));
		yield return new ServiceOption("Intimidate", () => Intimidate(player, npc));
		yield return new ServiceOption("Taunt", () => Taunt(player, npc));
		yield return new ServiceOption("Bribe 10 Gold", () => Bribe(player, npc, 10), 10);
		yield return new ServiceOption("Bribe 100 Gold", () => Bribe(player, npc, 100), 100);
		yield return new ServiceOption("Bribe 1000 Gold", () => Bribe(player, npc, 1000), 1000);
	}
}