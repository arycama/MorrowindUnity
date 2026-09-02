using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(Pow2Attribute))]
public class Pow2Drawer : PropertyDrawer
{
	public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
	{
		var pow2Attribute = attribute as Pow2Attribute;
		var minvalue = pow2Attribute.MinValue;
		var maxValue = pow2Attribute.MaxValue;

		var valueStart = (int)Mathf.Log(minvalue, 2);
		var valueCount = (int)Mathf.Log(maxValue, 2) + 1 - valueStart;
		var values = new int[valueCount];
		var valueNames = new GUIContent[valueCount];

		for (var i = 0; i < valueCount; i++)
		{
			var value = 1 << (valueStart + i);
			values[i] = value;

			string valueName;
			switch (property.propertyType)
			{
				case SerializedPropertyType.Integer:
				case SerializedPropertyType.Float:
					valueName = value.ToString();
					break;
				case SerializedPropertyType.Vector2:
				case SerializedPropertyType.Vector2Int:
					valueName = $"{value}x{value}";
					break;
				case SerializedPropertyType.Vector3:
				case SerializedPropertyType.Vector3Int:
					valueName = $"{value}x{value}x{value}";
					break;
				default:
					Debug.LogError("Pow2 drawer used on unsupported type");
					return;
			}

			valueNames[i] = new GUIContent(valueName);
		}

		property.intValue = EditorGUI.IntPopup(position, label, property.intValue, valueNames, values);
	}
}
