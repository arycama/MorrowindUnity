using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Esm;
using UnityEngine;

public class DialogResultProcessor
{
	// Journal IC14_Ponius 45
	// player->additem gold_001 1000
	// Choice "Offer to track down the clerk and recover the gold." 1 "Thank him for his frankness and leave." 2
	// Journal "VA_VampChild" 100
	// Journal "VA_VampChild" 30
	// Player->additem "extravagant_ring_aund_uni" 1

	// Identifier space string space int
	// identifier arrow identifier space string space int
	// Identifier space string space int space string space int
	// identifier string int

	// "Minabibi Assardarainat"->PositionCell 217 846 74 190 "Sadrith Mora, Wolverine Hall: Mage's Guild"
	// String arrow Identifier int int int int 

	public static void ProcessResult(IDialogController dialogView, string result, DialogRecord dialog, Character player, Character Npc)
	{
		var lexer = new DialogResultLexer(result);
		var tokens = lexer.ProcessResult().ToList();

		var parser = new DialogResultParser();
		var methodCalls = parser.ParseFile(tokens);

		foreach (var methodCall in methodCalls)
		{
			switch (methodCall.Method)
			{
				// Choice "Offer to track down the clerk and recover the gold." 1 "Thank him for his frankness and leave." 2
				case "choice":
				case "Choice": 
				case "Choice,":
					ShowChoices(dialogView, methodCall.Args, dialog);
					break;
				default:
					Debug.Log(methodCall);
					break;
			}
		}

		return;
		// split the result into lines
		var stringReader = new StringReader(result);
		var parameters = new List<string>();

		// Iterate through each line
		string line;
		while ((line = stringReader.ReadLine()) != null)
		{
			// Ignore any lines starting with a semi colon as they are comments
			if (line.StartsWith(";"))
			{
				continue;
			}

			// Get the function name by splitting the first two words. If no spaces are found, then no extra parameters exist
			// Split by spaces, and quotes? 
			var phrase = new StringBuilder();
			var inQuotes = false;
			for (var i = 0; i < line.Length; i++)
			{
				switch (line[i])
				{
					case ' ':
						// If in quotes, append spaces to the parameter
						if (inQuotes)
						{
							phrase.Append(line[i]);
						}
						else
						{
							// If not in quotes, add the current phrase to the list of parameters, and clear the string builder
							// Ensure we're not adding empty spaces to the parameter list
							if (phrase.ToString() != string.Empty)
							{
								parameters.Add(phrase.ToString());
							}

							phrase.Clear();
						}
						break;
					case '"':
						inQuotes = !inQuotes;
						break;
					default:
						phrase.Append(line[i]);
						break;
				}
			}

			// Append the last phrase from the string builder
			parameters.Add(phrase.ToString());

			// Process the function
			// This will probably eventually be replaced by a script interpreter/tokenizer thing, but for now see if this works.
			switch (parameters[0])
			{
				//case "additem":
				//	break;
				//case "addtopic":
				//case "Addtopic":
				//	break;
				// Choice is a word, followed by an option in Quotes, then a number specifying the index. This may occur multipel times
				//case "choice":
				//case "Choice": // Choice "Offer to track down the clerk and recover the gold." 1 "Thank him for his frankness and leave." 2
				//case "Choice,":
				//	ShowChoices(dialogView, parameters, dialog);
				//	break;
				//case "ClearInfoActor":
				// should not add this entry to journal
				//break;
				//case "Goodbye": // Show Goodbye choice, close dialog
				//break;
				case "Journal": //Jouranl ID Index, ID may be in quotes (But not always?)
				case "Journal,":
				case "Journal.":
					var exists = player.Journal.AddOrUpdateEntry(parameters[1], int.Parse(parameters[2]));
					if (!exists) dialogView.DisplayResult(GameSetting.Get("sJournalEntry").StringValue);
					break;
				case "moddisposition":
				case "ModDisposition": // Followed by a space? and then a number (can be positive or negative)
					Npc.DispositionMod += int.Parse(parameters[1]);
					dialogView.SetDisposition(Npc.GetDisposition(player));
					break;
				//case "ModPCFacRep": //ModPCFacRep 10 "Fighters Guild" 
				//	break;
				case "player->AddItem,":
				case "player->Additem,":
				case "Player->AddItem":
					{
						var item = Record.GetRecord<ItemRecord>(parameters[1].TrimEnd(','));
						// If a second parameter exists, it may be the count
						int quantity = parameters.Count > 2 ? int.Parse(parameters[2]) : 1;
						player.Inventory.AddItem(item, quantity);
						var message = GameSetting.Get(quantity == 1 ? "sNotifyMessage60" : "sNotifyMessage61").StringValue;
						message = message.Replace("%s", item.FullName);
						message = message.Replace("%d", quantity.ToString());
						dialogView.DisplayResult(message);
					}
					break;
				//case "PCClearExpelled": // Makes the PC no longer expelled from the speakers faction (Unsure if sometimes followed by a parameter)
				//break;
				case "PCJoinFaction": // Should be followed by the faction name eg PCJoinFaction "Mages Guild"
					player.JoinFaction(Record.GetRecord<Faction>(parameters[1]));
					break;

				// This can also appear instead of joining the faction, so must join the faction first if not a member
				//PCRaiseRank "Hlaalu" (This may not have the faction name written after, in which case the speaker's faction should be used)
				case "PCRaiseRank":
					if (!Npc.IsInSameFaction(player))
						player.JoinFaction(Npc.Factions.First().Key);
					else
						player.Factions[Npc.Factions.First().Key].Rank++;
					break;
				case "player->removeitem":
					{
						var item = Record.GetRecord<ItemRecord>(parameters[1].TrimEnd(','));
						player.Inventory.RemoveItem(item, 1);
						var message = GameSetting.Get("sNotifyMessage62").StringValue;
						message = message.Replace("%s", item.FullName);
						dialogView.DisplayResult(message);
					}
					break;
				//case "set":
				//	if (parameters.Count > 1) Debug.Log(parameters[1]);
				//	break;
				//case "ShowMap":
				//	break;
				default:
					// Should check string for a "->" character, if found, the first part is object id, i.e.:
					//  arrille->moddisposition
					// Should search for an object ID here, and if found, look for the -> symbol, then switch on what follows eg(Player->AddItem Gold_001 500)
					parameters.ForEach((param) => Debug.Log(param));
					break;
			}

			// Clear the list of parameters for the next iteration
			parameters.Clear();
		}
	}

	private static void ShowChoices(IDialogController controller, IEnumerable<MethodArgument> parameters, DialogRecord dialog)
	{
		// Every second parameter should be a number?
		var previousChoice = string.Empty;

		foreach (var parameter in parameters)
		{
			if(parameter.Type == ArgumentType.String)
			{
				previousChoice = parameter.StringValue;
			}
			else if(parameter.Type == ArgumentType.Int)
			{
				var choice = parameter.IntValue;
				controller.DisplayChoice(previousChoice, dialog, choice);
			}
			else
			{
				throw new ArgumentException("Incorrect argument passed to Choices. Must be a String or Int");
			}
		}
	}
}
