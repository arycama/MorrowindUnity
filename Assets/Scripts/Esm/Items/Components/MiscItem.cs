using System.Collections;
using System.Collections.Generic;
using Esm;
using UnityEngine;

[SelectionBase]
public class MiscItem : Item<MiscItem, MiscItemRecord>
{
	[SerializeField]
	private CreatureRecord soul;

	protected override void Initialize(MiscItemRecord record, ReferenceData referenceData)
	{
		base.Initialize(record, referenceData);
		soul = referenceData.Soul;
	}

	public override void Activate(GameObject target)
	{
		var inventory = target.GetComponent<IInventory>();
		if (inventory == null)
		{
			return;
		}

		if (EconomyManager.IsGold(record))
		{
			inventory.AddItem(EconomyManager.Gold, record.Data.Value);
		}
		else
		{
			inventory.AddItem(record, quantity);
		}
		
		record.PickupSound.PlaySoundAtPoint(transform.position);
		Destroy(gameObject);
	}

	public override void DisplayInfo()
	{
		infoPanel = record.DisplayInfo(transform.position, quantity, true, soul);
	}
}