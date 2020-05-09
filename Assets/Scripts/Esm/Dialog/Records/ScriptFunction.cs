public enum ScriptFunction : short
{
	RankLow = '0' + ('0' << 8),
	RankHigh = '0' + ('1' << 8),
	RankRequirement = '0' + ('2' << 8),
	Reputation = '0' + ('3' << 8),
	HealthPercent = '0' + ('4' << 8),
	PcReputation = '0' + ('5' << 8),
	PcLevel = '0' + ('6' << 8),
	PcHealthPercent = '0' + ('7' << 8),
	PcDynamicStat = '0' + ('8' << 8),
	PcDynamicStat2 = '0' + ('9' << 8),
	PcAttribute0 = '1' + ('0' << 8),
	PcSkill0 = '1' + ('1' << 8),
	PcSkill1 = '1' + ('2' << 8),
	PcSkill2 = '1' + ('3' << 8),
	PcSkill3 = '1' + ('4' << 8),
	PcSkill4 = '1' + ('5' << 8),
	PcSkill5 = '1' + ('6' << 8),
	PcSkill6 = '1' + ('7' << 8),
	PcSkill7 = '1' + ('8' << 8),
	PcSkill8 = '1' + ('9' << 8),
	PcSkill9 = '2' + ('0' << 8),
	PcSkill10 = '2' + ('1' << 8),
	PcSkill11 = '2' + ('2' << 8),
	PcSkill12 = '2' + ('3' << 8),
	PcSkill13 = '2' + ('4' << 8),
	PcSkill14 = '2' + ('5' << 8),
	PcSkill15 = '2' + ('6' << 8),
	PcSkill16 = '2' + ('7' << 8),
	PcSkill17 = '2' + ('8' << 8),
	PcSkill18 = '2' + ('9' << 8),
	PcSkill19 = '3' + ('0' << 8),
	PcSkill20 = '3' + ('1' << 8),
	PcSkill21 = '3' + ('2' << 8),
	PcSkill22 = '3' + ('3' << 8),
	PcSkill23 = '3' + ('4' << 8),
	PcSkill24 = '3' + ('5' << 8),
	PcSkill25 = '3' + ('6' << 8),
	PcSkill26 = '3' + ('7' << 8),
	PcGender = '3' + ('8' << 8),
	PcExpelled = '3' + ('9' << 8),
	PcCommonDisease = '4' + ('0' << 8),
	PcBlightDisease = '4' + ('1' << 8),
	PcClothingModifier = '4' + ('2' << 8),
	PcCrimeLevel = '4' + ('3' << 8),
	SameGender = '4' + ('4' << 8),
	SameRace = '4' + ('5' << 8),
	SameFaction = '4' + ('6' << 8),
	FactionRankDiff = '4' + ('7' << 8),
	Detected = '4' + ('8' << 8),
	Alarmed = '4' + ('9' << 8),
	Choice = '5' + ('0' << 8),
	PcAttribute1 = '5' + ('1' << 8),
	PcAttribute2 = '5' + ('2' << 8),
	PcAttribute3 = '5' + ('3' << 8),
	PcAttribute4 = '5' + ('4' << 8),
	PcAttribute5 = '5' + ('5' << 8),
	PcAttribute6 = '5' + ('6' << 8),
	PcAttribute7 = '5' + ('7' << 8),
	PcCorprus = '5' + ('8' << 8),
	Weather = '5' + ('9' << 8),
	PcVampire = '6' + ('0' << 8),
	Level = '6' + ('1' << 8),
	Attacked = '6' + ('2' << 8),
	TalkedToPC = '6' + ('3' << 8),
	PcDynamicStat3 = '6' + ('4' << 8),
	CreatureTargetted = '6' + ('5' << 8),
	FriendlyHit = '6' + ('6' << 8),
	ShouldAttack = '7' + ('1' << 8),
	Werewolf = '7' + ('2' << 8),
	WerewolfKills = '7' + ('3' << 8)
};

//case  0: return Function_RankLow;
//case  1: return Function_RankHigh;
//case  2: return Function_RankRequirement;
//case  3: return Function_Reputation;
//case  4: return Function_HealthPercent;
//case  5: return Function_PCReputation;
//case  6: return Function_PcLevel;
//case  7: return Function_PcHealthPercent;
//case  8: case  9: return Function_PcDynamicStat;
//case 10: return Function_PcAttribute;
//case 11: case 12: case 13: case 14: case 15: case 16: case 17: case 18: case 19: case 20:
//case 21: case 22: case 23: case 24: case 25: case 26: case 27: case 28: case 29: case 30:
//case 31: case 32: case 33: case 34: case 35: case 36: case 37: return Function_PcSkill;
//case 38: return Function_PcGender;
//case 39: return Function_PcExpelled;
//case 40: return Function_PcCommonDisease;
//case 41: return Function_PcBlightDisease;
//case 42: return Function_PcClothingModifier;
//case 43: return Function_PcCrimeLevel;
//case 44: return Function_SameGender;
//case 45: return Function_SameRace;
//case 46: return Function_SameFaction;
//case 47: return Function_FactionRankDiff;
//case 48: return Function_Detected;
//case 49: return Function_Alarmed;
//case 50: return Function_Choice;
//case 51: case 52: case 53: case 54: case 55: case 56: case 57: return Function_PcAttribute;
//case 58: return Function_PcCorprus;
//case 59: return Function_Weather;
//case 60: return Function_PcVampire;
//case 61: return Function_Level;
//case 62: return Function_Attacked;
//case 63: return Function_TalkedToPc;
//case 64: return Function_PcDynamicStat;
//case 65: return Function_CreatureTargetted;
//case 66: return Function_FriendlyHit;
//case 67: case 68: case 69: case 70: return Function_AiSetting;
//case 71: return Function_ShouldAttack;
//case 72: return Function_Werewolf;
//case 73: return Function_WerewolfKills;