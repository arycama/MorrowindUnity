using UnityEngine;

[System.Serializable]
public class AnimationAction
{
	[SerializeField]
	private string name;

	[SerializeField]
	private TransitionType type;

	[SerializeField]
	private bool boolValue;

	[SerializeField]
	private float floatValue;

	[SerializeField]
	private int intValue;

	public void Invoke(CharacterAnimation animation)
	{
		switch (type)
		{
			case TransitionType.Bool:
				animation.Parameters.SetBoolParameter(name, boolValue);
				break;
			case TransitionType.Int:
				animation.Parameters.SetIntParameter(name, intValue);
				break;
			case TransitionType.Float:
				animation.Parameters.SetFloatParameter(name, floatValue);
				break;
		}
	}
}