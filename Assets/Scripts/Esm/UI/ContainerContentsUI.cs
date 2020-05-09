using System;
using Esm;
using UnityEngine;
using UnityEngine.UI;

[SelectionBase]
public class ContainerContentsUI : PauseGameUI
{
	[SerializeField]
	private Text title;

	[SerializeField]
	private RectTransform contents;

	[SerializeField]
	private InventoryPanelUI prefab;

	public static ContainerContentsUI Create(string title, IInventory inventory, IInventory targetInventory)
	{
		var ui = Resources.Load<ContainerContentsUI>("UI/Container Contents UI");
		var inventoryUI = Instantiate(ui);

		// Set the title of the container
		inventoryUI.title.text = title;

		//Create an icon for each item
		foreach (var item in inventory.Items)
		{
			var panel = Instantiate(ui.prefab, inventoryUI.contents);

			var action = new Action(() => inventory.TransferItem(item.Key, item.Value, targetInventory));
			action += () => Destroy(panel.gameObject);

			panel.Initialize(item.Key, item.Value, action);
		}

		return ui;
	}
}