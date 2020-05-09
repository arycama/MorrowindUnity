using Esm;
using UnityEngine;

[SelectionBase]
public class Weapon : Item<Weapon, WeaponRecord>
{
	[SerializeField]
	private int health;

	[SerializeField]
	private float charge;

	protected override void Initialize(WeaponRecord record, ReferenceData referenceData)
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