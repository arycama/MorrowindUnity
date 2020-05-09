using System;
using System.Linq;
using UnityEngine;

namespace Esm
{
	[Serializable]
	public class ScriptVariable
	{
		[SerializeField]
		private byte index;

		[SerializeField]
		private float floatValue;

		[SerializeField]
		private int intValue;

		[SerializeField]
		private string variable;

		[SerializeField]
		private CompareOp compareOp;

		[SerializeField]
		private ScriptFunction function;

		[SerializeField]
		private ScriptVariableType type;

		[SerializeField]
		private ClassRecord characterClass;

		[SerializeField]
		private Global global;

		[SerializeField]
		private ItemRecord item;

		[SerializeField]
		private Faction faction;

		[SerializeField]
		private Race race;

		public ScriptVariable(System.IO.BinaryReader reader, int size)
		{
			index = reader.ReadByte();
			type = (ScriptVariableType)reader.ReadByte();
			function = (ScriptFunction)reader.ReadInt16();
			compareOp = (CompareOp)reader.ReadByte();
			variable = reader.ReadString(size - 5);

			// Do a switch here to determine the variable to save looking it up each frame
			switch (type)
			{
				case ScriptVariableType.Global:
					global = Record.GetRecord<Global>(variable);
					break;
				case ScriptVariableType.Item:
					item = Record.GetRecord<ItemRecord>(variable);
					break;
				case ScriptVariableType.NotClass:
					characterClass = Record.GetRecord<ClassRecord>(variable);
					break;
				case ScriptVariableType.NotFaction:
					faction = Record.GetRecord<Faction>(variable);
					break;
				case ScriptVariableType.NotRace:
					race = Record.GetRecord<Race>(variable);
					break;
			}
		}

		public float FloatValue { get { return floatValue; } set { floatValue = value; } }

		public int IntValue { get { return intValue; } set { intValue = value; } }

		// Checks an Npc Record against the filter
		public bool CheckFilters(Character player, Character npc, int choice = -1)
		{
			switch (type)
			{
				case ScriptVariableType.Nothing:
					return true;

				case ScriptVariableType.Function:
					return CheckFunction(player, npc, choice);

				case ScriptVariableType.Global:
					return CompareValue(global.Value);

				// if the speaker does not have a script or their script does not contain the variable you specify, this will return 0.
				case ScriptVariableType.Local:
					return CheckLocal(npc.NpcRecord.Script);

				// Returns the highest index that has ever been set for a particular journal.
				case ScriptVariableType.Journal:
					int index;
					return player != null && player.Journal != null ? CompareValue(player.Journal.Entires.TryGetValue(variable, out index) ? index : 0) : false;

				// returns the number of any item that the player has their inventory
				// If the player has none of the item, we still need to compare against 0
				case ScriptVariableType.Item:
					return player != null && CompareValue(player.Inventory.GetItemQuantity(item));

				// This returns the number of any NPC or Creature ID that is dead
				case ScriptVariableType.Dead:
					return CompareValue(0);

				// This is true if the speaker is not this particular ID. For all of these except Not Local, it does not matter what you set it equal to
				case ScriptVariableType.NotId:
					return npc.NpcRecord.name != variable;

				// This is true if the speaker is not in this faction
				case ScriptVariableType.NotFaction:
					return !npc.IsMemberOfFaction(faction);

				// This is true if the speaker is not of this class
				case ScriptVariableType.NotClass:
					return npc.NpcRecord.Class != characterClass;

				// This is true if the speaker is not of this race
				case ScriptVariableType.NotRace:
					return npc.NpcRecord.Race != race;

				// This is true if the player is not in this cell
				case ScriptVariableType.NotCell:
					return player != null && !player.gameObject.scene.name.StartsWith(variable);

				// This is true if the speaker does not have this local variable. Unlike most "Not" functions, this one does care what you set the variable to. Both the dialogue and the variable itself should be set to 0. This can be confusing. Here is a table of how this works: 
				/*
				Not Local Variable Exists Value Pass? 
				(in dialogue) (y/n) (in the script) (speaker will say this) 
				= 0 No NA Yes 
				= 0 Yes 0 No 
				= 0 Yes 5 Yes 
				= 1 No NA Yes 
				= -3 Yes -3 No 
				*/
				case ScriptVariableType.NotLocal:
					return CheckNotLocal(npc.NpcRecord.Script);

				default:
					throw new Exception(type.ToString());
			}
		}

		private bool CheckFunction(Character player, Character npc, int choice)
		{
			switch (function)
			{
				case ScriptFunction.RankLow:
					return CompareValue(0);
				case ScriptFunction.RankHigh:
					return CompareValue(0);

				case ScriptFunction.RankRequirement:
					return player != null ? CompareValue(npc.CheckRankRequirements(player)) : false;

				case ScriptFunction.Reputation:
					return CompareValue(npc.NpcRecord.NpcSubData.Reputation);

				// This returns the percent health of the speaker.
				case ScriptFunction.HealthPercent:
					return CompareValue(npc.GetComponent<Character>().NormalizedHealth * 100);
				case ScriptFunction.PcReputation:
					return player != null ? CompareValue(player.NpcRecord.NpcSubData.Reputation) : false;
				case ScriptFunction.PcLevel:
					return player != null ? CompareValue(player.NpcRecord.NpcSubData.Level) : false;

				case ScriptFunction.PcHealthPercent:
					return player != null ? CompareValue(player.GetComponent<Character>().NormalizedHealth * 100) : false;

				case ScriptFunction.PcGender:
					return player != null ? CompareValue(player.NpcRecord.IsFemale ? 1 : 0) : false;
				case ScriptFunction.PcExpelled:
					return CompareValue(0);
				case ScriptFunction.PcCommonDisease:
					return CompareValue(0);
				case ScriptFunction.PcBlightDisease:
					return CompareValue(0);
				case ScriptFunction.PcClothingModifier:
					return CompareValue(1);
				case ScriptFunction.PcCrimeLevel:
					return CompareValue(0);
				case ScriptFunction.SameGender:
					return player != null ? CompareValue(npc.NpcRecord.IsFemale == player.NpcRecord.IsFemale ? 1 : 0) : false;
				case ScriptFunction.SameRace:
					return player != null ? CompareValue(npc.NpcRecord.Race == player.NpcRecord.Race ? 1 : 0) : false;
				case ScriptFunction.SameFaction:
					return player != null ? CompareValue(npc.IsInSameFaction(player) ? 1 : 0) : false;

				// This is the player's rank in the speaker's faction minus the speaker's rank. Note that the first rank in a faction is 0 and your "rank" is –1 if you do not belong to that faction. A return value of 0 is the same rank, 1 is PC is one rank higher, -2 is PC is two ranks lower.
				case ScriptFunction.FactionRankDiff:
					return CompareValue(0);

				// This is 1 if the speaker detects the player and 0 otherwise.
				case ScriptFunction.Detected:
					return CompareValue(1);

				

				// This is 1 if the speaker is currently Alarmed (has detected a crime) and 0 otherwise.
				case ScriptFunction.Alarmed:
					return CompareValue(0);
				case ScriptFunction.Choice:
					return CompareValue(choice);
				case ScriptFunction.PcCorprus:
					return CompareValue(0);
				case ScriptFunction.Weather:
					return CompareValue(0);
				case ScriptFunction.PcVampire:
					return CompareValue(Record.GetRecord<Global>("PCVampire").Value);

				// This is 1 if the speaker has ever been attacked, and 0 otherwise.
				case ScriptFunction.Attacked:
					return CompareValue(0);
				case ScriptFunction.TalkedToPC:
					return CompareValue(npc.HasTalkedToPlayer ? 1 : 0);
				case ScriptFunction.PcDynamicStat3:
					return CompareValue(0);

				// Returns true (1) if the speaker is targeting a creature.
				case ScriptFunction.CreatureTargetted:
					return CompareValue(0);
				default:
					return false;
			}
		}

		// Returns true if the speaker has this variable in their script
		private bool CheckLocal(Script script)
		{
			if (script != null)
				foreach (var scriptVariable in script.ScriptVariables)
				{
					float result;
					if (script.ScriptVariables.TryGetValue(variable, out result))
					{
						return CompareValue(result);
					}
				}

			return false;
		}

		// Checks if an npc does not have a script with a specified local variable
		private bool CheckNotLocal(Script script)
		{
			if (script != null && script.ScriptVariables.ContainsKey(variable))
			{
				return false;
			}

			return true;
		}

		private bool CompareValue(float value)
		{
			switch (compareOp)
			{
				case CompareOp.Equal:
					return value == IntValue;
				case CompareOp.NotEqual:
					return value != IntValue;
				case CompareOp.GreaterThan:
					return value > IntValue;
				case CompareOp.GreaterThanOrEqual:
					return value >= IntValue;
				case CompareOp.LessThan:
					return value < IntValue;
				case CompareOp.LessThanOrEqual:
					return value <= IntValue;
				default:
					throw new NotImplementedException(compareOp.ToString());
			}
		}
	}
}
