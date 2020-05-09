using System;
using UnityEngine;

[Serializable]
public class AnimSubStateTransition
{
	[SerializeField]
	private string animationName;

	[SerializeField]
	private TransitionCondition[] conditions;

	public string AnimationName => animationName;

	public bool CheckConditions(CharacterAnimation animation, bool currentStateEnabled)
	{
		foreach(var condition in conditions)
		{
			if (!condition.CheckTransition(animation, currentStateEnabled))
			{
				return false;
			}
		}

		return true;
	}
}
