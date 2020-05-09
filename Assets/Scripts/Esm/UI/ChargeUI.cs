using UnityEngine;
using UnityEngine.UI;

public class ChargeUI : MonoBehaviour
{
	[SerializeField]
	private Text text;

	[SerializeField]
	private Image image;

	public void Initialize(float currentCharge, float maxCharge)
	{
		text.text = string.Format("{0}/{1}", currentCharge, maxCharge);
		image.fillAmount = currentCharge / maxCharge;
	}
}