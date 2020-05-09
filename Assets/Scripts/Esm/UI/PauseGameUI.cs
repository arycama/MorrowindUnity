using UnityEngine;

/// <summary>
/// Abstract class for UI that pauses the game, shows the mouse and confines it
/// </summary>
public abstract class PauseGameUI : MonoBehaviour
{
	public void CloseUI()
	{
		Destroy(gameObject);
	}

	protected virtual void OnEnable()
	{
		Time.timeScale = 0;
		Cursor.visible = true;
		Cursor.lockState = CursorLockMode.None;
	}

	protected virtual void OnDisable()
	{
		Time.timeScale = 1;
		Cursor.lockState = CursorLockMode.Confined;
		Cursor.visible = false;
	}
}