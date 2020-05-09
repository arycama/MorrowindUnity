using Esm;
using UnityEngine;

[SelectionBase]
public class Armor : Item<Armor, ArmorRecord>
{
	[SerializeField]
	private int health;

	[SerializeField]
	private float charge;

	protected override void Initialize(ArmorRecord record, ReferenceData referenceData)
	{
		base.Initialize(record, referenceData);
	
		health = referenceData.Health;
		charge = referenceData.Charge;
	}

	public override void DisplayInfo()
	{
		infoPanel = record.DisplayInfo(transform.position, quantity, true, health, charge);
	}
}