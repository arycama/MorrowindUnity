using UnityEngine;
using System;

[Serializable]
public class BlendState2D
{
	[SerializeField]
	private string animationName;

	[SerializeField]
	private Vector2 position;

	public AnimationState state;

	public string AnimatioName => animationName;
	public Vector2 Position => position;
}

[CreateAssetMenu]
public class FourWayBlendTree : AnimStateBase
{
	[SerializeField]
	private string horizontalParameter;

	[SerializeField]
	private string verticalParameter;

	[SerializeField]
	private string upAnimationName;

	[SerializeField]
	private string downAnimationName;

	[SerializeField]
	private string leftAnimationName;

	[SerializeField]
	private string rightAnimationName;

	[SerializeField]
	private BlendState2D[] blendStates;

	private readonly AnimationState upState, downState, leftState, rightState;

    public override string AnimationName
    {
        get
        {
            throw new NotImplementedException();
        }
    }

    //public override AnimationState OnStateEnter(CharacterAnimation animation)
    //{
    //	upState = animation.animation[upAnimationName];
    //	upState.enabled = true;

    //	downState = animation.animation[downAnimationName];
    //	downState.enabled = true;

    //	leftState = animation.animation[leftAnimationName];
    //	leftState.enabled = true;

    //	rightState = animation.animation[rightAnimationName];
    //	rightState.enabled = true;

    //	foreach(var blendState in blendStates)
    //	{
    //		blendState.state = animation.animation[blendState.AnimatioName];
    //		blendState.state.enabled = true;
    //	}

    //       throw new NotImplementedException();
    //}

    //public override AnimStateBase OnStateUpdate(CharacterAnimation animation)
    //{
    //       var result = base.OnStateUpdate(animation);

    //	var horizontal = animation.GetParameter<float>(horizontalParameter);
    //	var vertical = animation.GetParameter<float>(verticalParameter);

    //	upState.weight = Mathf.Clamp01(vertical);
    //	downState.weight = Mathf.Clamp01(-vertical);
    //	leftState.weight = Mathf.Clamp01(-horizontal);
    //	rightState.weight = Mathf.Clamp01(horizontal);

    //       return result;
    //}

    public override void OnStateExit(CharacterAnimation animation)
	{
		upState.enabled = false;
		downState.enabled = false;
		leftState.enabled = false;
		rightState.enabled = false;
	}
}