#pragma warning disable 0108

using System;
using System.Buffers;
using System.Collections.Generic;
using Esm;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Common data and functionality for characters
/// </summary>
public class CharacterMovement : MonoBehaviour
{
    [SerializeField] private float movementSpeed;
    [SerializeField] private float strafeSpeed = 0.75f;
    [SerializeField] private float rotateSpeed = 0.5f;
    [SerializeField] private float jumpHeight = 250;
    [SerializeField] private LayerMask layerMask = ~0;
    [SerializeField] private float groundedThreshold = 10f;

    private Rigidbody rb;
    private bool isGrounded;
    private readonly float currentHealth;
    private CharacterInput input;
    private CharacterAnimation animation;
    private Vector3 movement;

    private Collider[] colliders = new Collider[2];

    public void Initialize(CharacterAnimation animation, CharacterInput input)
    {
        this.animation = animation;
        this.input = input;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
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
        animation.Parameters.SetIntParameter("MovementDirection", (int)movementDirection);

        var moveSpeed = GetMovementSpeed();
        animation.Parameters.SetIntParameter("MovementSpeed", (int)moveSpeed);

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
            //GetComponent<Rigidbody>().velocity += new Vector3(0, jumpHeight, 0);
            //isGrounded = false;
            animation.Parameters.SetBoolParameter("IsGrounded", isGrounded);
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
            default:
                Move(Vector3.zero, 0.0f);
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
                    animation.Parameters.SetIntParameter("AttackType", (int)AttackType.Chop);
                    break;
                case MovementDirection.Forward:
                case MovementDirection.Back:
                case MovementDirection.ForwardLeftRight:
                case MovementDirection.BackLeftRight:
                    animation.Parameters.SetIntParameter("AttackType", (int)AttackType.Thrust);
                    break;
                case MovementDirection.Left:
                case MovementDirection.Right:
                    animation.Parameters.SetIntParameter("AttackType", (int)AttackType.Slash);
                    break;
            }
        }
        else
        {
            animation.Parameters.SetIntParameter("AttackType", (int)AttackType.None);
        }
    }

    private void Move(Vector3 direction, float movementSpeed)
    {
        movement = transform.TransformDirection(direction) * movementSpeed;
    }

    private void FixedUpdate()
    {
        var box = GetComponentInChildren<BoxCollider>();

        // First, resolve any hits overlapping at the start of the frame
        // Now apply movement and gravity
        var finalMovement = movement * Time.fixedDeltaTime;

        if(!isGrounded)
            finalMovement += Physics.gravity * Time.fixedDeltaTime;

        var center = box.transform.position + finalMovement;
        var rotation = box.transform.rotation;

        var length = Physics.OverlapBoxNonAlloc(center, box.size * 0.5f + Vector3.one * groundedThreshold, colliders, rotation, layerMask);
        while (length == colliders.Length)
        {
            colliders = new Collider[length * 2];
            length = Physics.OverlapBoxNonAlloc(center, box.size * 0.5f + Vector3.one * groundedThreshold, colliders, rotation, layerMask);
        }

        isGrounded = false;
        for(var i = 0; i < length; i++)
        {
            var boxHit = colliders[i];
            if (boxHit == box)
                continue;

            if (Physics.ComputePenetration(box, center, rotation, boxHit, boxHit.transform.position, boxHit.transform.rotation, out var normal, out var depenetrationDistance))
            {
                finalMovement += normal * depenetrationDistance;
                var direction = Vector3.Dot(Vector3.up, normal);
                if (direction > 0)
                    isGrounded = true;
            }
            else if(boxHit.Raycast(new Ray(center, Vector3.down), out var hit, box.size.y * 0.5f + groundedThreshold))
                isGrounded = true;
        }

        transform.position += finalMovement;
        animation.Parameters.SetBoolParameter("IsGrounded", isGrounded);
    }

    private void OnCollisionEnter(Collision collision)
    {
        foreach (var contact in collision.contacts)
        {
            var direction = Vector3.Dot(Vector3.up, contact.normal);
            if (direction > 0)
            {
                isGrounded = true;
                animation.Parameters.SetBoolParameter("IsGrounded", isGrounded);
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