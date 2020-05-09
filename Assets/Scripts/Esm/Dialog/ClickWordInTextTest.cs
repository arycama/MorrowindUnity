using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

class ClickWordInTextTest : MonoBehaviour, UnityEngine.EventSystems.IPointerClickHandler
{
	public Text TheTextComponent; // filled in the inspector or somewhere else

	public List<string> ClickableWords = new List<string>();
	public List<UnityEvent> EventHandlers = new List<UnityEvent>();

	private TextGenerator generator;
	private void Start()
	{
		if (TheTextComponent == null) TheTextComponent = GetComponent<Text>();
		if (TheTextComponent == null)
		{
			Debug.LogError("TheTextComponent was not specified");
			this.enabled = false;
			return;
		}
	}

	private Rect GetCharacterRect(int pos)
	{
		Vector2 upperLeft = new Vector2(generator.verts[pos * 4].position.x, generator.verts[pos * 4 + 2].position.y);
		Vector2 bottomright = new Vector2(generator.verts[pos * 4 + 2].position.x, generator.verts[pos * 4].position.y);

		Vector2 size = bottomright - upperLeft;
		return new Rect(upperLeft, size);
	}

	public void OnPointerClick(PointerEventData eventData)
	{
		var clickpos = TheTextComponent.transform.worldToLocalMatrix.MultiplyPoint(eventData.position);

		generator = TheTextComponent.cachedTextGenerator;

		for (int i = 0; i < ClickableWords.Count; i++)
		{
			if (i >= EventHandlers.Count) break;
			if (string.IsNullOrEmpty(ClickableWords[i]) || EventHandlers[i] == null) continue;
			if (!TheTextComponent.text.Contains(ClickableWords[i])) continue;

			for (int pos = TheTextComponent.text.IndexOf(ClickableWords[i]);
				pos <= TheTextComponent.text.IndexOf(ClickableWords[i]) + ClickableWords.Count;
				++pos)
			{
				var r = GetCharacterRect(pos);
				if (r.Contains(clickpos))
				{
					// click found:
					Debug.Log("clicked word: " + ClickableWords[i]);
					EventHandlers[i].Invoke();

					break;
				}

			}

		}
	}
}