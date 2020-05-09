using UnityEngine;
using UnityEditor;

public class GaussianBlurShaderGUI : ShaderGUI
{
	public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
	{
		base.OnGUI(materialEditor, properties);

		var size = (int)FindProperty("_Size", properties).floatValue;

		// Blur constants
		var blur = size * 2;
		var sqrBlur = blur * blur;
		var twoSqrBlur = 2 * sqrBlur;
		var twoSqrBlurPi = Mathf.PI * twoSqrBlur;

		// These ones are actually used
		var twoSqrBlurRecip = -1.0f / Mathf.Sqrt(twoSqrBlur);
		var twoSqrBlurPiRecip = 1.0f / twoSqrBlurPi;
		var length = blur * 3;
		var offset = -length / 2 + 0.5f;

		// Calculate sum
		var sum = 0f;
		for(var x = 0; x < length; x++)
		{
			sum += Mathf.Exp(Mathf.Pow(x + offset, 2) * twoSqrBlurRecip) * twoSqrBlurPiRecip;
		}

		// Make into a recip to save pixel divide
		sum = 1 / sum;

		FindProperty("_TwoSqrBlurRecip", properties).floatValue = twoSqrBlurRecip;
		FindProperty("_TwoSqrBlurPiRecip", properties).floatValue = twoSqrBlurPiRecip;
		FindProperty("_Length", properties).floatValue = length;
		FindProperty("_Offset", properties).floatValue = offset;
		FindProperty("_Sum", properties).floatValue = sum;
	}
}