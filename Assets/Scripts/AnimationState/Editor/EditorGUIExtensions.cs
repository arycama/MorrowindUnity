using UnityEngine;
using UnityEditor;

public static class EditorGUIExtensions
{
	public static bool PropertyFieldRelative(this SerializedProperty property, Rect position, string relativePropertyPath)
	{
		var relativeProperty = property.FindPropertyRelative(relativePropertyPath);
		return EditorGUI.PropertyField(position, relativeProperty);
	}
}