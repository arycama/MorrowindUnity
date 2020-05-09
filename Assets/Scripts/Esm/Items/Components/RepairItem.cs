using Esm;
using UnityEngine;

[SelectionBase]
public class RepairItem : Item<RepairItem, RepairItemRecord>
{
	[SerializeField]
	private int uses;

	protected override void Initialize(RepairItemRecord record, ReferenceData referenceData)
	{
		base.Initialize(record, referenceData);
		uses = referenceData.Health;
	}

	public override void DisplayInfo()
	{
		infoPanel = record.DisplayInfo(transform.position, quantity, true, uses);
	}
}