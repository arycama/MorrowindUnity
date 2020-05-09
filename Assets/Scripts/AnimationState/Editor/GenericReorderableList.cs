using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System;
using System.Collections;

/// <summary>
/// Represents a Reorderable List with property fields for the specific types they are drawing
/// </summary>
public class GenericReorderableList : ReorderableList
{
    public GenericReorderableList(IList elements, Type elementType) : base(elements, elementType)
    {
        drawElementCallback = new ElementCallbackDelegate(DrawElement);
        drawHeaderCallback = new HeaderCallbackDelegate(DrawHeader);
        elementHeightCallback = new ElementHeightCallbackDelegate(ElementHeight);
    }

    public GenericReorderableList(SerializedObject serializedObject, SerializedProperty elements) : base(serializedObject, elements)
    {
        drawElementCallback = new ElementCallbackDelegate(DrawElement);
        drawHeaderCallback = new HeaderCallbackDelegate(DrawHeader);
        elementHeightCallback = new ElementHeightCallbackDelegate(ElementHeight);
    }

    public GenericReorderableList(IList elements, Type elementType, bool draggable, bool displayHeader, bool displayAddButton, bool displayRemoveButton) : base(elements, elementType, draggable, displayHeader, displayAddButton, displayRemoveButton)
    {
        drawElementCallback = new ElementCallbackDelegate(DrawElement);
        drawHeaderCallback = new HeaderCallbackDelegate(DrawHeader);
        elementHeightCallback = new ElementHeightCallbackDelegate(ElementHeight);
    }

    public GenericReorderableList(SerializedObject serializedObject, SerializedProperty elements, bool draggable, bool displayHeader, bool displayAddButton, bool displayRemoveButton) : base(serializedObject, elements, draggable, displayHeader, displayAddButton, displayRemoveButton)
    {
        drawElementCallback = new ElementCallbackDelegate(DrawElement);
        drawHeaderCallback = new HeaderCallbackDelegate(DrawHeader);
        elementHeightCallback = new ElementHeightCallbackDelegate(ElementHeight);
    }

    private void DrawElement(Rect rect, int index, bool isActive, bool isFocused)
	{
		// Don't draw if the list is collapsed
		if (!serializedProperty.isExpanded)
		{
			return;
		}

		EditorGUI.PropertyField(rect, serializedProperty.GetArrayElementAtIndex(index));
	}

	private void DrawHeader(Rect rect)
	{
		var newRect = new Rect(rect.x + 10, rect.y, rect.width - 10, rect.height);
		serializedProperty.isExpanded = EditorGUI.Foldout(newRect, serializedProperty.isExpanded, serializedProperty.displayName);
	}

	private float ElementHeight(int index)
	{
		if (!serializedProperty.isExpanded)
		{
			return 0;
		}

		return EditorGUI.GetPropertyHeight(serializedProperty.GetArrayElementAtIndex(index));
	}
}