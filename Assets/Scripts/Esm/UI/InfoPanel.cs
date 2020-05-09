using System;
using UnityEngine;
using UnityEngine.UI;

public class InfoPanel : MonoBehaviour
{
	[SerializeField]
	private Image titleIcon;

	[SerializeField]
	private Text titleText;

	[SerializeField]
	private ChargeUI chargePrefab;

	[SerializeField]
	private Transform content;

	[SerializeField]
	private Text textPrefab;

	[SerializeField]
	private EffectIconUI effectIconPrefab;

	public static InfoPanel Create(Vector2 position)
	{
		var prefab = UIManager.InfoPanel;
		var infoPanel = Instantiate(UIManager.InfoPanel);
		return infoPanel;
	}

	public void AddTitle(string text)
	{
		titleText.text = text;
	}

	public void AddText(string text)
	{
		var textClone = Instantiate(textPrefab, content);
		textClone.text = text;
		textClone.alignment = TextAnchor.MiddleCenter;
	}

	public void DisplayIcon(Sprite sprite)
	{
		titleIcon.sprite = sprite;
	}

	public void AddEffectIcon(Sprite sprite, string text)
	{
		var effectIconClone = Instantiate(effectIconPrefab, content);
		effectIconClone.Initialize(sprite, text);
	}

	public void AddCharge(float currentCharge, float maxCharge)
	{
		var clone = Instantiate(chargePrefab, content);
		clone.Initialize(currentCharge, maxCharge);
	}
}