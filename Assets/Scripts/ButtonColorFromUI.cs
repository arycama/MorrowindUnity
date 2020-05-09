using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ButtonColorFromUI : MonoBehaviour
{
	[SerializeField]
	private Button button;

	[SerializeField]
	private string key = "normal";

	private void OnValidate()
	{
		if(button == null)
		{
			button = GetComponent<Button>();
		}
	}

	private void Awake()
	{
		button.colors = new ColorBlock
		{
			normalColor = IniManager.GetColor("FontColor", $"color_{key}"),
			highlightedColor = IniManager.GetColor("FontColor", $"color_{key}_over"),
			pressedColor = IniManager.GetColor("FontColor", $"color_{key}_pressed"),
			disabledColor = IniManager.GetColor("FontColor", $"color_disabled"),
			colorMultiplier = 1
		};
	}
}