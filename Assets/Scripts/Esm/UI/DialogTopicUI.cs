using System;
using UnityEngine;
using UnityEngine.UI;

public class DialogTopicUI : MonoBehaviour
{
	[SerializeField]
	private Button button;

	[SerializeField]
	private Text text;

	public bool Enabled { get { return button.interactable; } set { button.interactable = value; } }
	public string Text => text.text;

	public void Initialize(string text, Action action, bool isEnabled = true)
	{
		this.text.text = text;
		button.onClick.AddListener(action.Invoke);
		Enabled = isEnabled;
	}
}