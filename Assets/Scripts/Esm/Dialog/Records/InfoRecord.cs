using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Esm
{
	[Serializable]
	public class InfoRecord : ScriptableObject
	{
		[SerializeField]
		private string response;

		[SerializeField]
		private string result;

		[SerializeField]
		private ClassRecord classId;

		[SerializeField]
		private Faction faction;

		[SerializeField]
		private Faction playerFaction;

		[SerializeField]
		private Race race;

		[SerializeField]
		private string infoName, previousInfoId, nextInfoId;

		[SerializeField]
		private InfoRecordData infoData;

		[SerializeField]
		private CreatableRecord character;

		[SerializeField]
		private string cell;

		[SerializeField]
		private byte journalName, journalFinished, journalRestart;

		[SerializeField]
		private List<ScriptVariable> scriptVariables = new List<ScriptVariable>();

		[SerializeField]
		private string soundClip;

		public string Response => response;
		public string Result => result;

		public AudioClip AudioClip => soundClip == null ? null : SoundManager.LoadAudio(soundClip);

		public static InfoRecord Create(BinaryReader reader, RecordHeader header)
		{
			var instance = CreateInstance<InfoRecord>();
			instance.Initialize(reader, header);
			return instance;
		}

		public void Initialize(BinaryReader reader, RecordHeader header)
		{
			while (reader.BaseStream.Position < header.DataEndPos)
			{
				var type = (SubRecordType)reader.ReadInt32();
				var size = reader.ReadInt32();

				switch (type)
				{
					case SubRecordType.InfoName:
						infoName = reader.ReadString(size);
						break;
					case SubRecordType.PreviousName:
						previousInfoId = reader.ReadString(size);
						break;
					case SubRecordType.NextName:
						nextInfoId = reader.ReadString(size);
						break;
					case SubRecordType.Data:
						infoData = new InfoRecordData(reader);
						break;
					case SubRecordType.ObjectName:
						character = Record.GetRecord<CreatableRecord>(reader.ReadString(size));
						break;
					case SubRecordType.RaceName:
						race = Record.GetRecord<Race>(reader.ReadString(size));
						break;
					case SubRecordType.CreatureName:
						classId = Record.GetRecord<ClassRecord>(reader.ReadString(size));
						break;
					case SubRecordType.Name:
						var contents = reader.ReadString(size);
						if (contents != "FFFF")
							faction = Record.GetRecord<Faction>(contents);
						break;
					case SubRecordType.Anam:
						cell = reader.ReadString(size);
						break;
					case SubRecordType.DoorName:
						playerFaction = Record.GetRecord<Faction>(reader.ReadString(size));
						break;
					case SubRecordType.Id:
						response = reader.ReadString(size);
						break;
					case SubRecordType.SoundName:
						soundClip = reader.ReadString(size);
						break;
					case SubRecordType.JournalName:
						journalName = reader.ReadByte();
						break;
					case SubRecordType.JournalFinished:
						journalFinished = reader.ReadByte();
						break;
					case SubRecordType.JournalRestart:
						journalRestart = reader.ReadByte();
						break;
					case SubRecordType.ScriptVariable:
						scriptVariables.Add(new ScriptVariable(reader, size));
						break;
					case SubRecordType.IntValue:
						scriptVariables.Last().IntValue = reader.ReadInt32();
						break;
					case SubRecordType.FloatValue:
						scriptVariables.Last().FloatValue = reader.ReadSingle();
						break;
					case SubRecordType.BodyName:
						result = reader.ReadString(size);
						break;
				}
			}

			DialogRecord.currentDialogueInfo.Add(this);
		}

		public bool HasResponse(Character player, Character npc, int choice, bool isServiceRefusal)
		{
			// Each filter must be true for this response to be valid, so check them one by one, returning false if any are not met
			// If ID is specified, race, class and faction are not used, so check ID first
			if (character != null)
			{
				// Checks that the speaker is the correct Npc for this dialog response
				if (npc.NpcRecord != character)
					return false;
			}
			else
			{
				// Checks that the speaker is the correct race for this dialog response
				if (race != null && race != npc.NpcRecord.Race)
					return false;

				// Checks that the speaker is the correct class for this dialog response
				if (classId != null && npc.NpcRecord.Class != classId)
					return false;

				// Checks that the speaker is  a member of the correct Faction for this dialog response
				if (faction != null && !npc.IsMemberOfFaction(faction))
					return false;
			}

			// // Checks that the speaker is in the correct cell for this dialog response
			if (!string.IsNullOrEmpty(cell) && !npc.gameObject.scene.name.StartsWith(cell, StringComparison.OrdinalIgnoreCase))
				return false;

			// Checks that the player is in the correct faction for this dialog response
			if (playerFaction != null && (player == null || !player.IsMemberOfFaction(playerFaction)))
				return false;

			// Checks that all of the script variables for this dialog response are true
			foreach (var scriptVariable in scriptVariables)
			{
				if (!scriptVariable.CheckFilters(player, npc, choice))
					return false;
			}

			// Finally check the conditions in the "Info Data" class
			return infoData.CheckFilters(npc, player, faction, isServiceRefusal);
		}
	}
}