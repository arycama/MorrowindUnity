#pragma warning disable 0108

using UnityEngine;

public static class AnimationExtensions
{
    public static void Play(this Animation animation, AnimationState state)
    {
        state.weight = 1;
        state.enabled = true;
    }
}