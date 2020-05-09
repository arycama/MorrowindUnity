using UnityEngine;
using UnityEditor;

public class ShownByDrawer : MaterialPropertyDrawer
{
	private string propertyName;
	private float value;

	public ShownByDrawer(string propertyName, float value)
	{
		this.propertyName = propertyName;
		this.value = value;
	}

	public override void OnGUI(Rect position, MaterialProperty prop, string label, MaterialEditor editor)
	{
		var property = MaterialEditor.GetMaterialProperty(editor.targets, propertyName);
		if(property.floatValue == value)
		{
			editor.DefaultShaderProperty(prop, label);
		}
	}

	public override float GetPropertyHeight(MaterialProperty prop, string label, MaterialEditor editor)
	{
		return 0;
	}
}