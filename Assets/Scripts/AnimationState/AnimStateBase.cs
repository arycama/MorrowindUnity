using UnityEngine;

public abstract class AnimStateBase : ScriptableObject
{
	[SerializeField]
	protected bool hasSpeedParameter;

	[SerializeField, ToggleVisibility("hasSpeedParameter")]
	protected string speedParameter;

	[SerializeField]
	protected AnimationTransition[] transitions;

    public abstract string AnimationName { get; }

	public virtual void OnStateEnter(CharacterAnimation animation) { }
	public virtual void OnStateExit(CharacterAnimation animation) { }

	public virtual AnimationTransition OnStateUpdate(CharacterAnimation animation, bool currentStateEnabled)
	{
		foreach (var transition in transitions)
		{
			if (transition.CheckTransition(animation, currentStateEnabled))
			{
				OnStateExit(animation);
				return transition;
			}
		}

		return null;
	}
}