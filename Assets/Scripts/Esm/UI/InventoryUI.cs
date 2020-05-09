using System;
using Esm;
using UnityEngine;
using UnityEngine.UI;

[SelectionBase]
public class InventoryUI : PauseGameUI
{
	[SerializeField]
	private Text title;

	[SerializeField]
	private RectTransform contents;

	[SerializeField]
	private InventoryPanelUI prefab;

	public static InventoryUI Create(GameObject gameObject, IInventory inventory, string title)
	{
		var prefab = Resources.Load<InventoryUI>("UI/Inventory UI");
		var clone = Instantiate(prefab);

		// Set the title of the container
		clone.title.text = title;

		//Create an icon for each item
		foreach (var item in inventory.Items)
		{
			var panel = Instantiate(clone.prefab, clone.contents);
			panel.Initialize(item.Key, item.Value, () => item.Key.UseItem(gameObject));
		}

		return clone;
	}
}