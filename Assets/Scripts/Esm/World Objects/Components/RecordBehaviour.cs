using UnityEngine;

public abstract class RecordBehaviour<T, K> : MonoBehaviour where T : RecordBehaviour<T, K> where K : EsmRecord
{
	[SerializeField]
	protected K record;

	[SerializeField]
	protected ReferenceData referenceData;

	public static T Create(GameObject gameObject, K record, ReferenceData referenceData)
	{
		var component = gameObject.AddComponent<T>();
		component.Initialize(record, referenceData);
		return component;
	}

	protected virtual void Initialize(K record, ReferenceData referenceData)
	{
		this.record = record;
		this.referenceData = referenceData;
	}
}