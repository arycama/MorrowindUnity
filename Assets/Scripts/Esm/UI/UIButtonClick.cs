using Esm;
using UnityEngine;

class UIButtonClick : MonoBehaviour
{
	public void PlayClickSound()
	{
		var sound = Record.GetRecord<SoundRecord>("Menu Click");
		sound.PlaySoundAtPoint(transform.position);
	}
}