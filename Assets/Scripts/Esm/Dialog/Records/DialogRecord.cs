using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

namespace Esm
{
	[Serializable]
	public class DialogRecord : EsmRecordCollection<DialogRecord>
	{
		private static readonly Dictionary<string, DialogRecord>
			TopicCache = new Dictionary<string, DialogRecord>(),
			VoiceCache = new Dictionary<string, DialogRecord>(),
			GreetingCache = new Dictionary<string, DialogRecord>(),
			PersuasionCache = new Dictionary<string, DialogRecord>(),
			JournalCache = new Dictionary<string, DialogRecord>();

		public static List<InfoRecord> currentDialogueInfo;

		[SerializeField]
		private DialogType dialogueType;

		[SerializeField]
		private List<InfoRecord> infoRecords = new List<InfoRecord>();

		public override void Initialize(BinaryReader reader, RecordHeader header)
		{
			while (reader.BaseStream.Position < header.DataEndPos)
			{
				var type = (SubRecordType)reader.ReadInt32();
				var size = reader.ReadInt32();

				switch (type)
				{
					case SubRecordType.Id:
						name = reader.ReadString(size);
						break;
					case SubRecordType.Data:
						dialogueType = (DialogType)reader.ReadByte();
						break;
				}
			}

			// Add the dialog type to a cache so there are less items to iterate over
			switch (dialogueType)
			{
				case DialogType.Topic:
					TopicCache.Add(name, this);
					break;
				case DialogType.Voice:
					VoiceCache.Add(name, this);
					break;
				case DialogType.Greeting:
					GreetingCache.Add(name, this);
					break;
				case DialogType.Persuasion:
					PersuasionCache.Add(name, this);
					break;
				case DialogType.Journal:
					JournalCache.Add(name, this);
					break;
			}

			currentDialogueInfo = infoRecords;
		}

		public static DialogRecord GetPersuasionDialog(Character player, Character npc, PersuasionResult type)
		{
			var key = GameSetting.Get($"s{type}").StringValue;
			return PersuasionCache[key];
		}

		public static InfoRecord GetPersuasionInfo(Character player, Character npc, PersuasionResult type)
		{
			var key = GameSetting.Get($"s{type.ToString()}").StringValue;

			if(type == PersuasionResult.ServiceRefusal)
			{
				return PersuasionCache[key].GetInfo(player, npc, isServiceRefusal: true);
			}

			return PersuasionCache[key].GetInfo(player, npc);
		}

		public static InfoRecord GetDialogInfo(DialogType type, Character npc, Character player, string key = null, int choice = -1)
		{
			switch (type)
			{
				case DialogType.Greeting:
					foreach (var greeting in GreetingCache)
					{
						var info = greeting.Value.GetInfo(player, npc);
						if (info != null)
						{
							return info;
						}
					}
					return null;
				case DialogType.Journal:
					return JournalCache[key].GetInfo(player, npc, choice);
				case DialogType.Persuasion:
					return PersuasionCache[key].GetInfo(player, npc, choice);
				case DialogType.Voice:
					return VoiceCache[key].GetInfo(player, npc, choice);
				case DialogType.Topic:
				default:
					throw new NotImplementedException(type.ToString());
			}
		}

		public static List<DialogRecord> GetDialog(Character npc, Character player)
		{
			var topics = new List<DialogRecord>();

			// Check each dialog option to see if any of it's filters match
			foreach (var dialog in TopicCache)
			{
				// Go through each Info Record for the Dialog and check if it's filters match
				var info = dialog.Value.GetInfo(player, npc);
				if (info == null)
				{
					continue;
				}

				topics.Add(dialog.Value);
			}

			return topics;
		}


		public InfoRecord GetInfo(Character player, Character npc, int choice = -1, bool isServiceRefusal = false)
		{
			foreach (var info in infoRecords)
			{
				if (info.HasResponse(player, npc, choice, isServiceRefusal))
					return info;
			}

			return null;
		}
	}
}