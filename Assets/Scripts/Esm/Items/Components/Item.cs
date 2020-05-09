using Esm;
using UnityEngine;

[SelectionBase]
public abstract class Item<T, K> : RecordBehaviour<T, K>, IActivatable where T : Item<T, K> where K : ItemRecord
{
	[SerializeField]
	protected int quantity;

	[SerializeField]
	private OwnerData ownerData;

	protected InfoPanel infoPanel;

	protected override void Initialize(K record, ReferenceData referenceData)
	{
		base.Initialize(record, referenceData);
	
		quantity = referenceData.Quantity;
		ownerData = new OwnerData(referenceData.Owner, referenceData.Global, referenceData.Faction, referenceData.Rank);
	}

	public virtual void Activate(GameObject target)
	{
		var inventory = target.GetComponent<IInventory>();
		if (inventory == null)
		{
			return;
		}

		inventory.AddItem(record, 1);
		record.PickupSound.PlaySoundAtPoint(transform.position);
		Destroy(gameObject);
	}

	public virtual void DisplayInfo()
	{
		infoPanel = record.DisplayInfo(transform.position, quantity, true);
	}

	public virtual void CloseInfo()
	{
		if (infoPanel == null)
		{
			return;
		}

		Destroy(infoPanel.gameObject);
		infoPanel = null;
	}
}