using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomPropertyDrawer(typeof(ToggleVisibilityAttribute))]
public class ToggleVisibilityAttributeDrawer : PropertyDrawer
{
	public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
	{
		var toggleVisibilityAttribute = attribute as ToggleVisibilityAttribute;
		var propertyName = toggleVisibilityAttribute.fieldName;
		var toggleProperty = property.serializedObject.FindProperty(propertyName);

		// Control the visibility based on the value of the togglePropertty
		if (toggleProperty.boolValue)
		{
			EditorGUI.indentLevel++;
			EditorGUI.PropertyField(position ,property);
			EditorGUI.indentLevel--;
		}
	}

	public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
	{
		var toggleVisibilityAttribute = attribute as ToggleVisibilityAttribute;
		var propertyName = toggleVisibilityAttribute.fieldName;
		var toggleProperty = property.serializedObject.FindProperty(propertyName);

		// Control the visibility based on the value of the togglePropertty
		if (toggleProperty.boolValue)
		{
			return EditorGUIUtility.singleLineHeight;
		}

		return 0;
	}
}