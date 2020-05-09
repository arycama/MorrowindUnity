using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomPropertyDrawer(typeof(RepeatAttribute))]
public class RepeatAttributeDrawer : PropertyDrawer
{
	public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
	{
		var range = attribute as RepeatAttribute;

		// Now draw the property as a Slider or an IntSlider based on whether it's a float or integer.
		if (property.propertyType == SerializedPropertyType.Float)
			property.floatValue = EditorGUI.FloatField(position, label, Mathf.Repeat(property.floatValue, range.Range));
		else if (property.propertyType == SerializedPropertyType.Integer)
			property.intValue = EditorGUI.IntField(position, label, (int)Mathf.Repeat(property.intValue, range.Range));
		else
			EditorGUI.LabelField(position, label.text, "Use Repeat with float or int.");
	}
}