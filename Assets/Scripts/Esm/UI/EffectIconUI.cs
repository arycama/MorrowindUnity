using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class EffectIconUI : MonoBehaviour
{
	[SerializeField]
	private Image image;

	[SerializeField]
	private Text text;

	public void Initialize(Sprite sprite, string text)
	{
		image.sprite = sprite;
		this.text.text = text;
	}
}