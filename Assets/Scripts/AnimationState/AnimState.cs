using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu]
public class AnimState : AnimStateBase
{
	[SerializeField]
	private string animationName;

	private readonly AnimationState animationState;

    public override string AnimationName => animationName;

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(animationName))
        {
            animationName = name;
        }
    }
}