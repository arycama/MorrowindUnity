using UnityEngine;

public class ToggleVisibilityAttribute : PropertyAttribute
{
	public string fieldName;

	public ToggleVisibilityAttribute(string fieldName)
	{
		this.fieldName = fieldName;
	}
}