using UnityEditor;

[CustomEditor(typeof(AnimState))]
public class AnimStateEditor : Editor
{
	private GenericReorderableList transitionsList;

	private void OnEnable()
	{
		transitionsList = new GenericReorderableList(serializedObject, serializedObject.FindProperty("transitions"), true, true, true, true);
	}

	public override void OnInspectorGUI()
	{
		serializedObject.Update();

		var animationNameProperty = serializedObject.FindProperty("animationName");
		EditorGUILayout.PropertyField(animationNameProperty);

		var hasSpeedParameterProperty = serializedObject.FindProperty("hasSpeedParameter");
		EditorGUILayout.PropertyField(hasSpeedParameterProperty);
		if (hasSpeedParameterProperty.boolValue)
		{
			EditorGUI.indentLevel++;
			var speedProperty = serializedObject.FindProperty("speedParameter");
			EditorGUILayout.PropertyField(speedProperty);
			EditorGUI.indentLevel--;
		}

		transitionsList.DoLayoutList();
		serializedObject.ApplyModifiedProperties();
	}
}
