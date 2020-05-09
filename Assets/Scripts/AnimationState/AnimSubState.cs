using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu]
public class AnimSubState : AnimStateBase
{
	[SerializeField, Tooltip("Tranistions that are only checked upon entering the state"), FormerlySerializedAs("stateTransitions"), FormerlySerializedAs("enterTransisions")]
	private AnimSubStateTransition[] enterTransitions;

	[SerializeField]
	private AnimSubStateTransition[] stateTransitions;

	private AnimSubStateTransition baseState, activeState;

	private float previousSpeed;

    public override string AnimationName => activeState.AnimationName;

    public override void OnStateEnter(CharacterAnimation animation)
	{
		// Get the base substate upon entering this state. It will be played when no other state transition is met
		foreach (var stateTransition in enterTransitions)
		{
			if (!stateTransition.CheckConditions(animation, true))
			{
				continue;
			}

			// If the conditions match, set this state as the base state
			activeState = baseState = stateTransition;
			break;
		}
	}

	public override void OnStateExit(CharacterAnimation animation)
	{
		if(activeState != null)
		{
			//animation.animation.Stop(activeState.AnimationName);
		}

		activeState = null;
	}

	public override AnimationTransition OnStateUpdate(CharacterAnimation animation, bool currentStateEnabled)
	{
        var result = base.OnStateUpdate(animation, true);

		//if (hasSpeedParameter)
		//{
		//	var speed = animation.GetParameter<float>(speedParameter);
		//	if(speed != previousSpeed && animation.CurrentState != null)
		//	{
  //              animation.CurrentState.speed = speed;
		//		previousSpeed = speed;
		//	}
		//}

		//// Check each transition within this state to see if it should change
		//foreach (var stateTransition in stateTransitions)
		//{
		//	if (!stateTransition.CheckConditions(animation, animation.CurrentState))
		//	{
		//		continue;
		//	}

		//	// if the current state was true, stop checking as it should keep playing. (Otherwise it goes back and forth between states)
		//	if (activeState != stateTransition)
		//	{
		//		if (activeState != null)
		//		{
		//			animation.animation.Stop(stateTransition.AnimationName);
		//		}

		//		activeState = stateTransition;
  //              animation.CurrentState = animation.animation[stateTransition.AnimationName];
		//		animation.animation.Play(stateTransition.AnimationName);
		//	}

		//	return result;
		//}

		//// if none of the sub-states were triggered, play the base state
		//if(activeState != baseState)
		//{
		//	activeState = baseState;
		//	animation.animation.Play(baseState.AnimationName);
		//}

        return result;
	}
}