using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[ExecuteInEditMode]
public class UITextPrefab : MonoBehaviour, ILayoutIgnorer
{
	[SerializeField]
	private Text prefab;

	[SerializeField]
	private string text;

	private Text clone;

	bool ILayoutIgnorer.ignoreLayout => true;

	private void OnValidate()
	{
		if (prefab != null)
		{
			name = prefab.name;
		}
	}

	private void OnEnable()
	{
		if (!Application.isPlaying && prefab != null)
		{
			if (clone != null)
			{
				DestroyImmediate(clone.gameObject);
			}

			var siblingIndex = transform.GetSiblingIndex();

			clone = Instantiate(prefab, transform.parent);
			clone.text = text;

			clone.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild | HideFlags.NotEditable | HideFlags.HideInHierarchy;

			clone.transform.SetSiblingIndex(siblingIndex);
		}
	}

	private void OnDisable()
	{
		if (!Application.isPlaying && clone != null)
		{
			DestroyImmediate(clone.gameObject);
		}
	}

	private void Awake()
	{
		if (!Application.isPlaying)
		{
			return;
		}

		if (prefab == null)
		{
			return;
		}

		var siblingIndex = transform.GetSiblingIndex();

		var clone = Instantiate(prefab, transform.parent);
		clone.text = text;

		clone.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild | HideFlags.NotEditable;

		Destroy(gameObject);

		clone.transform.SetSiblingIndex(siblingIndex);
	}
}