using System;
using System.Collections.Generic;
using Esm;
using UnityEngine;
using Nif;

[Serializable]
public class EquippedPart
{
	[SerializeField]
	private Transform parent;

	[SerializeField]
	private IList<GameObject> equipment;

	[SerializeField, Tooltip("Indicates whether the current object can be unequipped by another object")]
	private bool isOverriden;

	public ItemRecord ItemData { get; private set; }
	public Transform Parent => parent;

	public EquippedPart(Transform parent)
	{
		this.parent = parent;
	}

	// Update this to just take an apparel piece as it already contains the biped part data I think
	public void Equip(string model, ItemRecord itemData, BipedPart part)
	{
		ItemData = itemData;

		if (equipment != null)
		{
			foreach (var equipmentObject in equipment)
			{
				GameObject.Destroy(equipmentObject);
			}
		}

		// Hides body parts
		if (!isOverriden)
		{
			if(part == BipedPart.Chest)
			{
				var chest = parent.Find("Tri Chest");
				chest.gameObject.SetActive(false);
			}
			else
			{
				foreach (Transform transform in parent)
				{
					transform.gameObject.SetActive(false);
				}
			}
		}

		var reader = BsaFileReader.LoadArchiveFileData($"meshes\\{model}");
		var niFile = new NiFile(reader);
		niFile.CreateGameObject(parent);
		equipment = niFile.CreatedObjects;
	}

	public void Unequip(BipedPart part)
	{
		if (equipment != null)
		{
			foreach (var equipmentObject in equipment)
			{
				GameObject.Destroy(equipmentObject);
			}
		}

		// Sets all the body parts on
		if (!isOverriden)
		{
			if (part == BipedPart.Chest)
			{
				var chest = parent.Find("Tri Chest");
				chest.gameObject.SetActive(true);
			}
			else
			{
				foreach (Transform transform in parent)
				{
					transform.gameObject.SetActive(true);
				}
			}
		}
	}
}