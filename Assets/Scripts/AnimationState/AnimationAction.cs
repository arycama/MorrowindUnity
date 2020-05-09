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
				animation.SetParameter(name, boolValue);
				break;
			case TransitionType.Int:
				animation.SetParameter(name, intValue);
				break;
			case TransitionType.Float:
				animation.SetParameter(name, floatValue);
				break;
		}
	}
}