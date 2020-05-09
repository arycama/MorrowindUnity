using System.Collections.Generic;
using Esm;
using UnityEngine;

[SelectionBase]
public class Container : Inventory<Container, ContainerRecord>, IActivatable, ILockable
{
	[SerializeField]
	private LockData lockData;

	private InfoPanel infoPanel;

	protected override void Initialize(ContainerRecord record, ReferenceData referenceData)
	{
		base.Initialize(record, referenceData);
		lockData = new LockData(referenceData.LockLevel, referenceData.Trap, referenceData.Key);
	}

	public void DisplayInfo()
	{
		infoPanel = InfoPanel.Create(transform.position);
		infoPanel.AddTitle(record.FullName);
		lockData.DisplayLockInfo(infoPanel);
	}

	void IActivatable.Activate(GameObject target)
	{
		if (lockData.CheckLock(target, "LockedChest"))
		{
			return;
		}

		// Get the target inventory from whatever activated this inventory
		var targetInventory = target.GetComponent<IInventory>();
		ContainerContentsUI.Create(record.FullName, this, targetInventory);
	}

	public void CloseInfo()
	{
		if (infoPanel == null)
		{
			return;
		}

		Destroy(infoPanel.gameObject);
		infoPanel = null;
	}

	bool ILockable.Unlock(float chance)
	{
		if (lockData.Unlock(transform.position, chance))
		{
			CloseInfo();
			DisplayInfo();
			return true;
		}

		return false;
	}
}