using UnityEngine;
using UnityEditor;

[CustomPropertyDrawer(typeof(AnimationTransition))]
public class AnimationTransitionDrawer : PropertyDrawer
{
	public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
	{
		position.height = EditorGUIUtility.singleLineHeight;

		property.PropertyFieldRelative(position, "targetState");
		EditorGUI.indentLevel++;

		// Dispaly exit time and additional values if needed
		position.y += position.height;
		var hasExitTimeProperty = property.FindPropertyRelative("hasExitTime");
		EditorGUI.PropertyField(position, hasExitTimeProperty);

		if (hasExitTimeProperty.boolValue)
		{
			position.y += position.height;
			property.PropertyFieldRelative(position, "exitTime");

			position.y += position.height;
			property.PropertyFieldRelative(position, "enterTime");
		}

		// Set the condition field to take up 2/3rds of the line, and the size to take up the rest
		var initialX = position.x;
		var initialWidth = position.width;

		position.width /= 3;
		position.y += position.height + EditorGUIUtility.standardVerticalSpacing;

		var conditionsProperty = property.FindPropertyRelative("conditions");
		EditorGUI.LabelField(position, conditionsProperty.displayName);

		// Halve the remaining width, draw the size label, then the size field at twice the width of the label
		position.x += position.width;
		position.width /= 2;
		EditorGUI.LabelField(position, new GUIContent("Size"));
		position.x += position.width;
		position.width *= 3;
		conditionsProperty.arraySize = EditorGUI.DelayedIntField(position, GUIContent.none, conditionsProperty.arraySize);

		// Set the width back to normal
		position.width = initialWidth;
		position.x = initialX;

		EditorGUI.indentLevel++;
		var length = conditionsProperty.arraySize;
		for (var i = 0; i < length; i++)
		{
			position.y += position.height + EditorGUIUtility.standardVerticalSpacing;
			var arrayElement = conditionsProperty.GetArrayElementAtIndex(i);
			EditorGUI.PropertyField(position, arrayElement);
		}

		EditorGUI.indentLevel--;
		EditorGUI.indentLevel--;
	}

	public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
	{
		var conditionsProperty = property.FindPropertyRelative("conditions");
		var elementCount = 3 + conditionsProperty.arraySize;

		var hasExitTimeProperty = property.FindPropertyRelative("hasExitTime");
		if (hasExitTimeProperty.boolValue)
		{
			elementCount += 2;
		}

		return EditorGUIUtility.singleLineHeight * elementCount + EditorGUIUtility.standardVerticalSpacing * (elementCount - 1);
	}
}
