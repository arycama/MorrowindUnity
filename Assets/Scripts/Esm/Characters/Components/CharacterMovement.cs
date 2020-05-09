#pragma warning disable 0108

using System;
using System.Collections.Generic;
using Esm;
using UnityEngine;

/// <summary>
/// Common data and functionality for characters
/// </summary>
public class CharacterMovement : MonoBehaviour
{
	[SerializeField]
	private float movementSpeed;

	[SerializeField]
	private float strafeSpeed = 0.75f;

	[SerializeField]
	private float rotateSpeed = 3;

	[SerializeField]
	private float jumpHeight = 250;


	private bool isGrounded;
	private float currentHealth;
	private CharacterInput input;
	private CharacterAnimation animation;

	public void Initialize(CharacterAnimation animation, CharacterInput input)
	{
		this.animation = animation;
		this.input = input;
	}

	private void Start()
	{
		movementSpeed = animation.CalculateMovementSpeed();
	}

	// Calculate Attack type from movement
	private void Update()
	{
		if (Time.timeScale == 0 || !input) { return; }

		// Calculate movement direciton here from CharacterInput inputs.
		var movementDirection = GetMovementDirection();
		animation.SetParameter("MovementDirection", movementDirection);

		var moveSpeed = GetMovementSpeed();
		animation.SetParameter("MovementSpeed", moveSpeed);

		transform.eulerAngles += new Vector3(0, input.Yaw * rotateSpeed, 0);
		var movementSpeed = this.movementSpeed;
		switch (moveSpeed)
		{
			case MovementSpeed.Run:
				movementSpeed *= GameSetting.Get("fBaseRunMultiplier").FloatValue;
				break;
			case MovementSpeed.Sneak:
				//movementSpeed *= GameSetting.Get("fSneakSpeedMultiplier").FloatValue;
				break;
			case MovementSpeed.Walk:
				break;
			case MovementSpeed.None:
				movementSpeed = 0;
				break;
		}

		// Jumping
		if (input.Jump && isGrounded)
		{
			GetComponent<Rigidbody>().velocity += new Vector3(0, jumpHeight, 0);
			isGrounded = false;
			animation.SetParameter("IsGrounded", isGrounded);
		}

		// Movement
		switch (movementDirection)
		{
			case MovementDirection.Forward:
			case MovementDirection.ForwardLeftRight:
				Move(Vector3.forward, movementSpeed);
				break;
			case MovementDirection.Back:
			case MovementDirection.BackLeftRight:
				Move(Vector3.back, movementSpeed);
				break;
			case MovementDirection.Left:
			case MovementDirection.LeftForwardBack:
				Move(Vector3.left, movementSpeed * strafeSpeed);
				break;
			case MovementDirection.Right:
			case MovementDirection.RightForwardBack:
				Move(Vector3.right, movementSpeed * strafeSpeed);
				break;
			case MovementDirection.ForwardLeft:
				Move(new Vector3(-0.75f, 0, 0.75f), movementSpeed);
				break;
			case MovementDirection.ForwardRight:
				Move(new Vector3(0.75f, 0, 0.75f), movementSpeed);
				break;
			case MovementDirection.BackLeft:
				Move(new Vector3(-0.75f, 0, -0.75f), movementSpeed);
				break;
			case MovementDirection.BackRight:
				Move(new Vector3(0.75f, 0, -0.75f), movementSpeed);
				break;
		}

		// Attacking
		// If attack is not being pressed, don't perform an attack
		if (input.Attack)
		{
			switch (movementDirection)
			{
				case MovementDirection.None:
				case MovementDirection.ForwardLeft:
				case MovementDirection.ForwardRight:
				case MovementDirection.BackLeft:
				case MovementDirection.BackRight:
					animation.SetParameter("AttackType", AttackType.Chop);
					break;
				case MovementDirection.Forward:
				case MovementDirection.Back:
				case MovementDirection.ForwardLeftRight:
				case MovementDirection.BackLeftRight:
					animation.SetParameter("AttackType", AttackType.Thrust);
					break;
				case MovementDirection.Left:
				case MovementDirection.Right:
					animation.SetParameter("AttackType", AttackType.Slash);
					break;
			}
		}
		else
		{
			animation.SetParameter("AttackType", AttackType.None);
		}
	}

	private void Move(Vector3 direction, float movementSpeed)
	{
		direction = transform.TransformDirection(direction);
		transform.position += direction * movementSpeed * Time.deltaTime;
	}

	private void OnCollisionEnter(Collision collision)
	{
		foreach (var contact in collision.contacts)
		{
			var direction = Vector3.Dot(Vector3.up, contact.normal);
			if (direction > 0)
			{
				isGrounded = true;
				animation.SetParameter("IsGrounded", isGrounded);
				break;
			}
		}
	}

	// Calculates left, right, forward and backward movement from inputs
	private MovementDirection GetMovementDirection()
	{
		var movement = MovementDirection.None;
		if (input.Forward)
		{
			movement |= MovementDirection.Forward;
		}

		if (input.Back)
		{
			movement |= MovementDirection.Back;
		}

		if (input.Left)
		{
			movement |= MovementDirection.Left;
		}

		if (input.Right)
		{
			movement |= MovementDirection.Right;
		}

		return movement;
	}

	// Returns whether the character is running, sneaking or walking
	private MovementSpeed GetMovementSpeed()
	{
		if (input.Run)
		{
			return MovementSpeed.Run;
		}
		else if (input.Sneak)
		{
			return MovementSpeed.Sneak;
		}
		else
		{
			return MovementSpeed.Walk;
		}
	}
}