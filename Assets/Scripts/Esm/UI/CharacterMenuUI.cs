using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CharacterMenuUI : MonoBehaviour
{
	[SerializeField]
	private Text title;

	[SerializeField, Tooltip("Prefab used to show race, class and level")]
	private InlineTitleInfoUI characterInfoPrefab;

	[SerializeField]
	private Transform characterInfoParent;

	[SerializeField, Tooltip("Prefab used to show attributes, skills etc")]
	private InlineTitleInfoUI attributesPrefab;

	[SerializeField]
	private Transform attributesParent;

	[SerializeField]
	private Transform skillsParent;

	public static CharacterMenuUI Create(string name, IEnumerable<UITitleInfoPair> characterInfo, IEnumerable<UITitleInfoPair> attributes, IEnumerable<UITitleInfoPair> skills)
	{
		var instance = Instantiate(UIManager.CharacterMenu);

		instance.title.text = name;

		// Show the status info

		// Show the character info
		foreach(var info in characterInfo)
		{
			var clone = Instantiate(instance.characterInfoPrefab, instance.characterInfoParent);
			clone.Initialize(info.Title, info.Info);
		}

		// Show the character attributes
		foreach(var attribute in attributes)
		{
			var clone = Instantiate(instance.attributesPrefab, instance.attributesParent);
			clone.Initialize(attribute.Title, attribute.Info);
		}

		// Show the skills
		foreach (var skill in skills)
		{
			var clone = Instantiate(instance.attributesPrefab, instance.skillsParent);
			clone.Initialize(skill.Title, skill.Info);
		}

		return instance;
	}
}