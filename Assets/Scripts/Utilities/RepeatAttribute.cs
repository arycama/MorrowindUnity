using UnityEngine;

public class RepeatAttribute : PropertyAttribute
{
	public float Range { get; }

	public RepeatAttribute(float range)
	{
		Range = range;
	}
}