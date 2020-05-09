using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using Esm;

public class DialogView : PauseGameUI, IDialogView
{
	[SerializeField]
	private Text title;

	[SerializeField]
	private RectTransform topicsParent;

	[SerializeField]
	private RectTransform servicesParent;

	[SerializeField]
	private RectTransform infoParent;

	[SerializeField]
	private ScrollRect scrollRect;

	[SerializeField]
	private ChargeUI chargeUI;

	[Header("Prefabs")]
	[SerializeField]
	private DialogTopicUI topicPrefab;

	[SerializeField]
	private DialogTopicUI choicePrefab;

	[SerializeField]
	private Text infoPrefab;

	// Save list of current topics so it can be modiifed/sorted when needed
	private List<DialogRecord> currentTopics;
	private List<DialogTopicUI> currentChoices = new List<DialogTopicUI>();

	private IDialogController controller;

	public float Disposition { set { chargeUI.Initialize(value, 100); } }

	public static DialogView Create(IDialogController controller, string name, List<DialogRecord> topics, IList<CharacterService> services, float disposition)
	{
		// Display the Npc's name
		var instance = Instantiate(UIManager.DialogUI);
		instance.controller = controller;

		instance.title.text = name;
		instance.chargeUI.Initialize(disposition, 100);

		// Display the services
		foreach (var service in services)
		{
			var clone = Instantiate(instance.topicPrefab, instance.servicesParent);
			clone.Initialize(service.ServiceName, () => controller.DisplayService(service));
		}

		// Display the current topics
		instance.currentTopics = topics;
		var sortedTopics = topics.OrderBy(topic => topic.name);
		foreach (var topic in sortedTopics)
		{
			var clone = Instantiate(instance.topicPrefab, instance.topicsParent);
			clone.Initialize(topic.name, () => controller.DisplayTopic(topic));
		}

		return instance;
	}

	// Adds a new topic to the topic list
	void IDialogView.AddTopic(DialogRecord topic)
	{
		// Convert tile to title case
		var title = char.ToUpper(topic.name[0]) + topic.name.Substring(1);

		// Create the UI game object
		var clone = Instantiate(topicPrefab, topicsParent);
		clone.Initialize(title, () => controller.DisplayTopic(topic));

		// Add it to the current list of topics, and sort
		currentTopics.Add(topic);
		currentTopics.Sort((x, y) => x.name.CompareTo(y.name));

		// Get the index and use it to position the transform in the list correctly
		var index = currentTopics.IndexOf(topic);
		clone.transform.SetSiblingIndex(index);
	}

	// Displays a dialog choice, and assigns it a callback
	void IDialogView.DisplayChoice(string text, DialogRecord dialog, int choice)
	{
		var textClone = Instantiate(choicePrefab, infoParent);
		textClone.Initialize(text, () => ChoiceSelected(dialog, choice));
		currentChoices.Add(textClone);

		ScrollToBottom();
	}

	// Displays the info of a topic, along with a possible title
	void IDialogView.DisplayInfo(string text, string title)
	{
		if (title != null)
		{
			title = char.ToUpper(title[0]) + title.Substring(1);

			var color = IniManager.GetColor("FontColor", "color_big_header");
			var colorHex = ColorUtility.ToHtmlStringRGB(color);

			if(text == null)
			{
				text = $"<color=#{colorHex}>{title}</color>{text}";
			}
			else
			{
				text = $"<color=#{colorHex}>{title}</color>\r\n{text}";
			}
		}

		if(text != null)
		{
			var infoClone = Instantiate(infoPrefab, infoParent);
			infoClone.text = text;
		}

		ScrollToBottom();
	}

	private void ChoiceSelected(DialogRecord dialog, int choice)
	{
		controller.DisplayTopic(dialog, choice);
		currentChoices.ForEach(component => Destroy(component.gameObject));
		currentChoices.Clear();
	}

	// Scrolls the UI to the bottom, taking newly-added elements into account
	private void ScrollToBottom()
	{
		Canvas.ForceUpdateCanvases();
		scrollRect.verticalNormalizedPosition = 0;
		Canvas.ForceUpdateCanvases();
	}
}