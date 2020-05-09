using Esm;
using UnityEngine;

[SelectionBase]
public class Probe : Item<Probe, ProbeRecord>
{
	[SerializeField]
	private int uses;

	protected override void Initialize(ProbeRecord record, ReferenceData referenceData)
	{
		base.Initialize(record, referenceData);
		uses = referenceData.Health;
	}

	public override void DisplayInfo()
	{
		infoPanel = record.DisplayInfo(transform.position, quantity, true, uses);
	}
}