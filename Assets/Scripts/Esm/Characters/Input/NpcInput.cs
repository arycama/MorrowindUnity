#pragma warning disable 0108

using System.Collections;
using UnityEngine;
using Esm;

public class NpcInput : CharacterInput
{
	[SerializeField]
	private float distanceThreshold = 100;

	[SerializeField]
	private float rotateSpeed = 360;

	[SerializeField]
	private NpcState currentState, nextState;
	
	public NpcState NextState
	{
		get { return nextState; }
		set { nextState = value; }
	}

	public CharacterAnimation Animation => animation;
	public GameObject Target { get; protected set; }
	public WanderData WanderData => wanderData;

	public bool Attack { get { return base.Attack; } set { base.Attack = value; } }
	public bool Equip { set { base.Equip = value; } }
	public bool Forward { set { base.Forward = value; } }
	public bool Run { set { base.Run = value; } }

	public float DistanceThreshold => distanceThreshold;
	public float RotateSpeed => rotateSpeed;

	private void Start()
	{
		combat.OnHitEvent += OnHit;
		NextState = new IdleAIState();
	}

	private void Update()
	{
		if(currentState != nextState)
		{
			currentState?.OnStateExit(this);
			nextState?.OnStateEnter(this);
			currentState = nextState;
		}

		nextState = currentState;

		if (currentState != null)
		{
			currentState.OnStateUpdate(this);
		}
	}

	protected virtual void OnHit(GameObject attacker)
	{
		Target = attacker;
		NextState = new AttackAIState();
	}
}