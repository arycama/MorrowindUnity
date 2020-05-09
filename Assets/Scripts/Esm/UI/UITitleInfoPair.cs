using System;
using UnityEngine;
/// <summary>
/// Stores a pair of strings to be used as a title and a description for a UI list. May be optionally paired with a callback when moused over or clicked
/// </summary>
[Serializable]
public class UITitleInfoPair
{
	[SerializeField]
	private string title;

	[SerializeField]
	private string info;

	public UITitleInfoPair(string title, string info)
	{
		this.title = title;
		this.info = info;
	}

	public string Title => title;
	public string Info => info;
}
