using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class BoxBlurShaderGUI : ShaderGUI
{
	public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
	{
		base.OnGUI(materialEditor, properties);

		var size = FindProperty("_Size", properties).floatValue;

		// Blur is 2^size
		var sizePow2 = Mathf.Pow(2, size);
		FindProperty("_Blur", properties).floatValue = sizePow2;

		// Recip is 1f / blur^2
		FindProperty("_Recip", properties).floatValue = 1f / sizePow2;

		// Offset is blur/2 - 0.5
		FindProperty("_Offset", properties).floatValue = -(sizePow2 / 2 - 0.5f);
	}
}