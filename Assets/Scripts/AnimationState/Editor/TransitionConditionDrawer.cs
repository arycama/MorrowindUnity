using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomPropertyDrawer(typeof(TransitionCondition))]
public class TransitionConditionDrawer : PropertyDrawer
{
	public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
	{
		// Find the type property first so it can be used to show/hide the compare op
		var typeProperty = property.FindPropertyRelative("type");
		var type = (TransitionType)typeProperty.enumValueIndex;

		var showCompareOp = true;
		SerializedProperty valueProperty = null;
		switch (type)
		{
			case TransitionType.None:
				showCompareOp = false;
				break;
			case TransitionType.Bool:
				valueProperty = property.FindPropertyRelative("boolValue");
				showCompareOp = false;
				break;
			case TransitionType.Int:
				valueProperty = property.FindPropertyRelative("intValue");
				break;
			case TransitionType.Float:
			case TransitionType.Time:
				valueProperty = property.FindPropertyRelative("floatValue");
				break;
			case TransitionType.String:
				valueProperty = property.FindPropertyRelative("stringValue");
				showCompareOp = false;
				break;
		}

		// If not showing the compare op, don't allocate space for it
		position.width /= showCompareOp ? 4 : 3;

		var nameProperty = property.FindPropertyRelative("name");
        if (!nameProperty.hasMultipleDifferentValues)
        {
            EditorGUI.PropertyField(position, nameProperty, GUIContent.none);
            position.x += position.width;
        }

		EditorGUI.PropertyField(position, typeProperty, GUIContent.none);

		if (showCompareOp)
		{
			position.x += position.width;
			var compareOpProperty = property.FindPropertyRelative("compareOp");
			EditorGUI.PropertyField(position, compareOpProperty, GUIContent.none);
		}

		position.x += position.width;
		if (valueProperty != null)
		{
			EditorGUI.PropertyField(position, valueProperty, GUIContent.none);
		}
	}
}