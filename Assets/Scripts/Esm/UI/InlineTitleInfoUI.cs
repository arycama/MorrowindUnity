using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows a left-aligned title  and a right-aligned description in a single line
/// </summary>
public class InlineTitleInfoUI : MonoBehaviour
{
	[SerializeField]
	private Text title;

	[SerializeField]
	private Text info;

	public void Initialize(string title, string info)
	{
		this.title.text = title;
		this.info.text = info;
	}
}