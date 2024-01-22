#pragma warning disable 0108

using System;
using System.Collections.Generic;
using System.Linq;
using Esm;
using UnityEngine;

public class Character : MonoBehaviour
{
	[SerializeField]
	private NpcRecord npcData;
    private float fatigue;
    private float health;
    private float magicka;
    private readonly float maxFatigueMod;
    private readonly float maxHealthMod;
    private readonly float maxMagickaMod;
    private readonly byte reputationMod, levelMod;

	private byte[] attributeMods, skillMods;

	private List<SpellRecord> spells;

	public int Bounty { get; private set; }
	public int DispositionMod { get; set; }

	private NpcRecordData SubData => npcData.NpcSubData;
	private DerivedAttributeData DerivedData => SubData.DerivedAttributeData;

	public bool IsFemale => npcData.IsFemale;
	public bool HasTalkedToPlayer { get; set; }

	private float MaxFatigue => DerivedData.Fatigue + maxFatigueMod;
	private float MaxHealth => DerivedData.Health + maxHealthMod;
	private float MaxMagicka => DerivedData.Magicka + maxMagickaMod;

	public float NormalizedFatigue => fatigue / MaxFatigue;
	public float NormalizedHealth => health / MaxHealth;
	public float NormalizedMagicka => magicka / MaxMagicka;

	public int Level => SubData.Level + levelMod;
	public int Reputation => SubData.Reputation + reputationMod;

	// A commonly-used term for influencing actions by fatigue
	public float FatigueTerm => GameSetting.Get("fFatigueBase").FloatValue - GameSetting.Get("fFatigueMult").FloatValue * (1 - NormalizedFatigue);

	// Persuasion-related stuff:
	public float PersonalityTerm => GetAttribute(CharacterAttribute.sAttributePersonality) / GameSetting.Get("fPersonalityMod").FloatValue;
	public float LuckTerm => GetAttribute(CharacterAttribute.sAttributeLuck) / GameSetting.Get("fLuckMod").FloatValue;
	public float ReputationTerm => Reputation * GameSetting.Get("fReputationMod").FloatValue;
	public float LevelTerm => Level * GameSetting.Get("fLevelMod").FloatValue;

	public string FullName => npcData.FullName;
	public string RankName => Factions.FirstOrDefault().Key.GetRankName(npcData.NpcSubData.Rank);

	public ClassRecord Class => npcData.Class;
	public Journal Journal => GetComponent<Journal>();
	public NpcRecord NpcRecord => npcData;
	public Race Race => npcData.Race;

	public IReadOnlyList<byte> Attributes => SubData.Attributes;
	public IReadOnlyList<byte> Skills => SubData.Skills;

	public IList<SpellRecord> Spells => spells;

	public IInventory Inventory => GetComponent<IInventory>();

	public Dictionary<Faction, FactionRankReputationPair> Factions = new Dictionary<Faction, FactionRankReputationPair>();

	private float DispCrimeMod => GameSetting.Get("fDispCrimeMod").FloatValue;
	private float DispDiseaseMod => GameSetting.Get("fDispDiseaseMod").FloatValue;
	private float DispWeaponDrawn => GameSetting.Get("fDispWeaponDrawn").FloatValue;
	private float DispFactionMod => GameSetting.Get("fDispFactionMod").FloatValue;
	private float DispFactionRankBase => GameSetting.Get("fDispFactionRankBase").FloatValue;
	private float DispFactionRankMult => GameSetting.Get("fDispFactionRankMult").FloatValue;
	private float PersonalityMult => GameSetting.Get("fDispPersonalityMult").FloatValue;
	private float PersonalityBase => GameSetting.Get("fDispPersonalityBase").FloatValue;

	public void Initialize(NpcRecord npcRecord)
	{
		this.npcData = npcRecord;
		if(npcRecord.Faction != null)
		{
			Factions.Add(npcRecord.Faction, new FactionRankReputationPair(npcRecord.NpcSubData.Rank, 0));
		}
		
		// Initialize various things
		attributeMods = new byte[Enum.GetNames(typeof(CharacterAttribute)).Length];
		skillMods = new byte[Enum.GetNames(typeof(CharacterSkill)).Length];
		spells = new List<SpellRecord>(npcRecord.Spells);

		fatigue = MaxFatigue;
		health = MaxHealth;
		magicka = MaxMagicka;
	}

	public bool IsMemberOfFaction(Faction faction) => Factions.ContainsKey(faction);

	public int GetSkill(CharacterSkill skill) => SubData.GetSkill(skill) + skillMods[(int)skill];
	public int GetAttribute(CharacterAttribute attribute) => SubData.GetAttribute(attribute) + attributeMods[(int)attribute];

	public int GetDisposition(Character player)
	{
		int disposition = npcData.NpcSubData.Disposition + DispositionMod;

		if (player.Race == Race)
		{
			disposition += (int)(PersonalityMult * (player.GetAttribute(CharacterAttribute.sAttributePersonality) - PersonalityBase));
		}

		int reaction = 0;
		byte rank = 0;

		// If the player is in the same faction as an npc, add the reaction to the disposition value
		var sameFaction = Factions.FirstOrDefault((faction) => player.IsMemberOfFaction(faction.Key));
		if(sameFaction.Key != null)
		{
			reaction = sameFaction.Key.GetReaction(sameFaction.Key);
			rank = npcData.NpcSubData.Rank;
		}
		else
		{
			// Otherwise, if not in the same faction, get the faction with the lowest reaction
			// For now just assume Npc can only have one faction
			var npcFaction = Factions.FirstOrDefault();
			if(npcFaction.Key != null)
			{
				var minReaction = 100;

				// Loop over all player factions, getting the reaction from the Npc faction for each one
				foreach(var playerFaction in player.Factions)
				{
					reaction = npcFaction.Key.GetReaction(playerFaction.Key);
					if(reaction < minReaction)
					{
						minReaction = reaction;
					}
				}
			}
		}

		disposition += (int)((DispFactionRankMult * rank + DispFactionRankBase) * DispFactionMod * reaction);
		disposition -= (int)(DispCrimeMod * player.Bounty);
		//if pc has a disease: x += fDispDiseaseMod
		//if pc has weapon drawn: x += fDispWeaponDrawn

		return Mathf.Clamp(disposition, 0, 100);
	}

	public void JoinFaction(Faction faction)
	{
		Factions.Add(faction, new FactionRankReputationPair(0, 0));
	}

	// Gets the Character's rank in a faction. Returns 0 if they are not a member.
	public int GetFactionRank(Faction faction, out int reputation)
	{
		FactionRankReputationPair result;
		if(Factions.TryGetValue(faction, out result))
		{
			reputation = result.Reputation;
			return result.Rank;
		}

		reputation = 0;
		return 0;
	}

	// Returns if the character is at least the specified rank in any faction
	public bool IsRankOfAnyFaction(byte rank)
	{
		foreach (var faction in Factions)
		{
			if (faction.Value.Rank >= rank)
			{
				return true;
			}
		}

		return false;
	}

	// Returns whether the npc is a member of a faction with a certain rank
	public bool IsFactionRank(Faction faction, byte rank)
	{
		FactionRankReputationPair rankReputationPair;
		return (Factions.TryGetValue(faction, out rankReputationPair) && rankReputationPair.Rank >= rank);
	}

	public int GetFactionReputation(Faction faction)
	{
		FactionRankReputationPair rankReputationPair;
		return (Factions.TryGetValue(faction, out rankReputationPair) ? rankReputationPair.Reputation : 0);
	}

	public void SetFactionReputation(Faction faction, byte rank)
	{
		FactionRankReputationPair rankReputationPair;
		if (Factions.TryGetValue(faction, out rankReputationPair))
			rankReputationPair.Reputation = rank;
	}

	// Returns whether two characters are in any faction together
	public bool IsInSameFaction(Character faction)
	{
		return Factions.Keys.Any(faction.Factions.ContainsKey);
	}

	// public if another character can join or rank up in this faction
	public int CheckRankRequirements(Character character)
	{
		return Factions.First().Key.CheckRankRequirements(character);
	}
}