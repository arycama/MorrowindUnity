using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class FeatureDrawer : MaterialPropertyDrawer
{
	private string keyword;

	public override void OnGUI(Rect position, MaterialProperty prop, string label, MaterialEditor editor)
	{
		if(keyword == null)
		{
			keyword = prop.name.ToUpper().TrimStart('_') + "_ON";
		}

		if (editor.TextureProperty(prop, label))
		{
			(editor.target as Material).EnableKeyword(keyword);
		}
		else
		{
			(editor.target as Material).DisableKeyword(keyword);
		}
	}
}