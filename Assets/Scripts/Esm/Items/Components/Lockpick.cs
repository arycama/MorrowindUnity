using Esm;
using UnityEngine;

[SelectionBase]
public class Lockpick : Item<Lockpick, LockpickRecord>
{
	[SerializeField]
	private int uses;

	protected override void Initialize(LockpickRecord record, ReferenceData referenceData)
	{
		base.Initialize(record, referenceData);
		uses = referenceData.Health;
	}

	public override void DisplayInfo()
	{
		infoPanel = record.DisplayInfo(transform.position, quantity, true, uses);
	}
}