using UnityEngine;

public class RangeVariableAttribute : PropertyAttribute
{
	private string name;

	public string Name { get { return name; } }

	public RangeVariableAttribute(string name)
	{
		this.name = name;
	}
}