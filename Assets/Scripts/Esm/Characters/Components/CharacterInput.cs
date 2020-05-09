#pragma warning disable 0108

using System;
using System.Collections;
using System.Collections.Generic;
using Esm;
using UnityEngine;

[RequireComponent(typeof(AudioSource)), SelectionBase]
public abstract class CharacterInput : MonoBehaviour
{
	[SerializeField]
	protected WanderData wanderData;

	[SerializeField]
	protected AiData aiData;

	protected CharacterAnimation animation;
	protected CharacterCombat combat;
	protected Collider targetCollider;

	protected IActivatable currentActivator;

	public bool Attack { get; protected set; }
	public bool Jump { get; protected set; }
	public bool Equip { get; protected set; }

	public bool Left { get; protected set; }
	public bool Right { get; protected set; }
	public bool Forward { get; protected set; }
	public bool Back { get; protected set; }

	public bool Sneak { get; protected set; }
	public bool Run { get; protected set; }

	[SerializeField]
	public float Yaw;

	public void Initialize(AiData aiData, WanderData wanderData, CharacterAnimation animation, CharacterCombat combat)
	{
		this.aiData = aiData;
		this.wanderData = wanderData;

		this.animation = animation;
		this.combat = combat;
	}
}