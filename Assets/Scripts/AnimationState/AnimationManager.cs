using UnityEngine;

public class AnimationManager : Singleton<AnimationManager>
{
	[SerializeField]
	private AnimStateBase idleState;

	public AnimStateBase IdleState => idleState;
}
