using UnityEngine;
using UnityEditor;

[CustomPropertyDrawer(typeof(BlendState2D))]
public class BlendState2DDrawer : PropertyDrawer
{
	public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
	{
		position.width /= 3;

		var nameProperty = property.FindPropertyRelative("animationName");
		EditorGUI.PropertyField(position, nameProperty, GUIContent.none);

		position.x += position.width;
		position.width *= 2;
		var positionProperty = property.FindPropertyRelative("position");
		EditorGUI.PropertyField(position, positionProperty, GUIContent.none);
	}
}
