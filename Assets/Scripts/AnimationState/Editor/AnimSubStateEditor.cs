using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(AnimSubState))]
public class AnimSubStateEditor : Editor
{
	private GenericReorderableList transitionsList, enterTransitionslist, stateTransitionsList;

	private void OnEnable()
	{
		transitionsList = new GenericReorderableList(serializedObject, serializedObject.FindProperty("transitions"), true, true, true, true);
		enterTransitionslist = new GenericReorderableList(serializedObject, serializedObject.FindProperty("enterTransitions"), true, true, true, true);
		stateTransitionsList = new GenericReorderableList(serializedObject, serializedObject.FindProperty("stateTransitions"), true, true, true, true);
	}

	public override void OnInspectorGUI()
	{
		serializedObject.Update();

		var hasSpeedParameterProperty = serializedObject.FindProperty("hasSpeedParameter");
		EditorGUILayout.PropertyField(hasSpeedParameterProperty);
		if(hasSpeedParameterProperty.boolValue)
		{
			EditorGUI.indentLevel++;
			var speedProperty = serializedObject.FindProperty("speedParameter");
			EditorGUILayout.PropertyField(speedProperty);
			EditorGUI.indentLevel--;
		}

		transitionsList.DoLayoutList();
		enterTransitionslist.DoLayoutList();
		stateTransitionsList.DoLayoutList();

		serializedObject.ApplyModifiedProperties();
	}
}
