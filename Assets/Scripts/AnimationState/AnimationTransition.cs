using System;
using UnityEngine;

[Serializable]
public class AnimationTransition
{
	[SerializeField, UnityEngine.Serialization.FormerlySerializedAs("target")]
	private AnimStateBase targetState;

	[SerializeField]
	private bool hasExitTime;

	[SerializeField, Range(0, 1)]
	private float exitTime;

	[SerializeField, Range(0, 1)]
	private float enterTime;

	[SerializeField]
	private TransitionCondition[] conditions;

	public bool HasExitTime => hasExitTime;
	public float EnterTime => enterTime;
	public float ExitTime => exitTime;
	public AnimStateBase Target => targetState;

	/// <summary>
	/// Checks all animation conditions, and returns true if they are all successful
	/// </summary>
	/// <param name="animation"></param>
	/// <returns></returns>
	public bool CheckTransition(CharacterAnimation animation, bool currentStateEnabled)
	{
		if (hasExitTime)
		{
			var time = animation.CurrentState?.normalizedTime;
			if (time != null)
			{
				// Check if animation has restarted
				if(time < animation.LastUpdateTime)
				{
					if(exitTime > animation.LastUpdateTime)
					{

					}
					else
					{
						return false;
					}
				}
				else
				{
					// Ensure the exit time was in the last update
					if (animation.LastUpdateTime <= exitTime && time >= exitTime)
					{

					}
					else
					{
						return false;
					}
				}
			}
		}

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