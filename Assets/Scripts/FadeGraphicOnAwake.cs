using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FadeGraphicOnAwake : MonoBehaviour
{
	[SerializeField]
	private float duration = 0.25f;


	private void Start()
	{
		var graphic = GetComponent<Graphic>();
		var color = graphic.color;
		color.a = 0.1f;
		graphic.color = color;

		graphic.CrossFadeAlpha(255, duration, true);
	}
}