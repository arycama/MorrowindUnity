using Esm;
using UnityEngine;

[SelectionBase]
public class LightItem : Item<LightItem, LightRecord>
{
	[SerializeField]
	private float time;

	protected override void Initialize(LightRecord record, ReferenceData referenceData)
	{
		base.Initialize(record, referenceData);
		time = referenceData.Health;
	}
}