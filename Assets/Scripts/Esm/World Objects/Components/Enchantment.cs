using Esm;
using UnityEngine;
using System.Collections;

public class Enchantment : MonoBehaviour
{
	[SerializeField]
	private float interval = 0.09375f;

	[SerializeField]
	private EnchantmentData enchantment;

	public void Initialize(EnchantmentData enchantment)
	{
		this.enchantment = enchantment;
	}

	private IEnumerator Start()
	{
		var textures = Resources.LoadAll<Texture2D>("Textures/magicitem");
		var renderers = GetComponentsInChildren<Renderer>();

		foreach(var effect in enchantment.Effects)
		{
			var data = effect.MagicEffect.magicEffectData;
			var color = new Color32((byte)data.red, (byte)data.green, (byte)data.blue, 255);

			foreach (var renderer in renderers)
			{
				renderer.material.EnableKeyword("_EMISSION");
				renderer.material.SetColor("_Emissive", color);
			}

			break;
		}

		while (isActiveAndEnabled)
		{
			foreach(var texture in textures)
			{
				foreach (var renderer in renderers)
				{
					renderer.material.SetTexture("_Glow", texture);
				}

				yield return new WaitForSeconds(interval);
			}
		}
	}
}
