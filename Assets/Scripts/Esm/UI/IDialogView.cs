using System;
using Esm;
using UnityEngine.Events;

public interface IDialogView
{
	float Disposition { set; }

	void AddTopic(DialogRecord topic);
	void DisplayChoice(string text, DialogRecord topic, int index);
	void DisplayInfo(string text, string title = null);
}