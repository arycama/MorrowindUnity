using System;
using System.Collections.Generic;
using Esm;
using UnityEngine;

public class DialogController : RecordBehaviour<DialogController, NpcRecord>, IActivatable, IDialogController
{
	private Character player;
	private IDialogView dialogView;
	private InfoPanel infoPanel;

	private List<DialogRecord> topics;

	public Character Npc => GetComponent<Character>();

	void IActivatable.DisplayInfo()
	{
		infoPanel = InfoPanel.Create(new Vector2(0.5f, 0.5f));
		infoPanel.AddTitle(record.FullName);
	}

	void IActivatable.CloseInfo()
	{
		Destroy(infoPanel.gameObject);
		infoPanel = null;
	}

	void IActivatable.Activate(GameObject target)
	{
		// Get the dialog component of the listener, so we can use it's NpcRecord
		player = target.GetComponent<Character>();

		// Get the npc greeting, as if there is no greeting, the Npc can't be talked to.
		var greeting = DialogRecord.GetDialogInfo(DialogType.Greeting, Npc, player);
		if (greeting == null)
		{
			return;
		}

		// Check if the listener knows the current topic, otherwise it will not be available
		// This could be done within the same loop as getting the dialog topic
		topics = DialogRecord.GetDialog(Npc, player);

		var knownTopics = new List<DialogRecord>();
		foreach (var topic in topics)
		{
			// Only show topics that the player knows 
			if (player.Journal.Topics.ContainsKey(topic.name))
				knownTopics.Add(topic);
		}

		// Display services
		var services = GetComponents<CharacterService>();

		// Load the dialog and instantiate 
		dialogView = DialogView.Create(this, record.FullName, knownTopics, services, Npc.GetDisposition(player));

		// Process the greeting after getting topics, or there will be no topics to check against
		DisplayInfo(null, greeting);

		// Set this last, so it isn't true the first time the player talks to an Npc.
		GetComponent<Character>().HasTalkedToPlayer = true;
	}

	void IDialogController.SetDisposition(int disposition) => dialogView.Disposition = disposition;

	void IDialogController.DisplayService(CharacterService service)
	{
		// Check if the service can be refused 
		if (service.IsRefusable)
		{
			var dialog = DialogRecord.GetPersuasionDialog(player, Npc, PersuasionResult.ServiceRefusal);
			var info = dialog.GetInfo(player, Npc, isServiceRefusal: true);

			// If a response was found, the service has been refused by the npc
			if (info != null)
			{
				DisplayInfo(dialog, info);
				return;
			}
		}

		service.DisplayService(this, player, Npc);
	}

	void IDialogController.DisplayTopic(DialogRecord dialog, int choice)
	{
		var info = dialog.GetInfo(player, Npc, choice);
		DisplayInfo(dialog, info);
	}

	void IDialogController.DisplayResult(string result)
	{
		dialogView.DisplayInfo(null, result);
	}

	void IDialogController.DisplayChoice(string description, DialogRecord dialog, int choice) => dialogView.DisplayChoice(description, dialog, choice);

	private void DisplayInfo(DialogRecord dialog, InfoRecord info)
	{
		// Replace any text defines with actual text
		var dialogText = TextDefineProcessor.ProcessText(info.Response, player, Npc);

		dialogText = CheckForTopicsInText(dialogText);
		dialogView.DisplayInfo(dialogText, dialog?.name);

		if (info.Result != null)
			DialogResultProcessor.ProcessResult(this, info.Result, dialog, player, Npc);
	}

	// Moved from dialog UI. Needs to be done after a topic is selected, and it's text filtered before it is displayed.
	// Will eventually need to change teh color for detected topics, and somehow add clickable hyperlinks
	private string CheckForTopicsInText(string text)
	{
		// Loops through every available topic to see if it is contained in the text.
		foreach (var topic in topics)
		{
			// Add new topics to the journal and the list of available topics for this dialog. (Should trigger some kind of callback)
			// This currently does not remove the text from the string, but should so that other topics do not detect it.
			var index = text.IndexOf(topic.name, StringComparison.OrdinalIgnoreCase);
			if (index == -1)
			{
				continue;
			}

			// Should also make the text blue here
			// Make text blue()
			var hyperlinkText = "<color=#707ecfff>";
			text = text.Insert(index, hyperlinkText);
			text = text.Insert(index + hyperlinkText.Length + topic.name.Length, "</color>");

			// If the player already knows this topic, it will already be included, so we wouldn't need to check for it. 
			// However we do need to check for it for hyperlinks sadly, so evenetually skip this check. 
			// Maybe we could check for available topics for hyperlinks instead?
			if (player.Journal.Topics.ContainsKey(topic.name))
			{
				continue;
			}

			// Add the topic to the journal
			player.Journal.AddTopic(topic.name, topic);
			dialogView.AddTopic(topic);
		}

		// The text may have had it's color modified, so return this
		return text;
	}
}
