using UnityEngine;

public interface IActivatable
{
	void DisplayInfo();
	void Activate(GameObject target);
	void CloseInfo();
}