using Esm;
using UnityEngine;

[SelectionBase]
public class Clothing : Item<Clothing, ClothingRecord>
{
	[SerializeField]
	private float charge;

	protected override void Initialize(ClothingRecord record, ReferenceData referenceData)
	{
		base.Initialize(record, referenceData);
		charge = referenceData.Charge;
	}
}