using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A generic UI Box that can be used to show a title, description, and a list of items with callbacks.
/// </summary>
public class ListPanelUI : MonoBehaviour
{
	[SerializeField]
	private RectTransform content;

	[SerializeField]
	private DialogTopicUI prefab;

	[SerializeField]
	private Text title;

	[SerializeField]
	private Text description;

	[SerializeField]
	private Text gold;

	[SerializeField]
	private Text button;

	private readonly Dictionary<string, DialogTopicUI> currentOptions = new Dictionary<string, DialogTopicUI>();

	public static ListPanelUI Create(IEnumerable<ListUIOption> options, string title, string description, string button, int gold, PauseGameUI parent, bool closeOnComplete = false, bool closeUIOnComplete = false)
	{
		var instance = Instantiate(UIManager.ListPanelUI);

		instance.title.text = title;
		instance.description.text = description;
		//this.button.text = button;

		instance.gold.text = $"Gold: {gold}";

		foreach(var option in options)
		{
			var clone = Instantiate(instance.prefab, instance.content);
			if (closeOnComplete || closeUIOnComplete)
			{
				var action = new Action(option.Action);

				if (closeUIOnComplete)
					action += () => parent.CloseUI();

				if (closeOnComplete)
					action += () => instance.CloseUI();

				clone.Initialize(option.Description, action.Invoke, option.IsEnabled);
			}
			else
			{
				clone.Initialize(option.Description, option.Action, option.IsEnabled);
			}

			instance.currentOptions.Add(option.Description, clone);
		}

		return instance;
	}

	public void RemoveItem(string item)
	{
		Destroy(currentOptions[item].gameObject);
	}

	public void CloseUI()
	{
		Destroy(gameObject);
	}
}
