using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(RangeVariableAttribute))]
public class RangeVariableAttributeDrawer : PropertyDrawer
{
	public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
	{
		var rangeAttribute = attribute as RangeVariableAttribute;
		var maxProperty = property.serializedObject.FindProperty(rangeAttribute.Name);

		EditorGUI.IntSlider(position, property, 0, maxProperty.intValue);
	}
}