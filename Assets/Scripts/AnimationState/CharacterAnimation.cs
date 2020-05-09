#pragma warning disable 0108

using UnityEngine;

/// <summary>
/// Controls playing animations according to user input or AI logic
/// </summary>
[RequireComponent(typeof(Animation))]
public class CharacterAnimation : MonoBehaviour
{
	[SerializeField]
	private AnimStateBase currentAnimState;

	private Vector3 previousRootPosition, rootOffset;
    private Animation animation;
    private Transform rootBone;

	public float LastUpdateTime { get; private set; }

    public AnimationParameters Parameters { get; private set; } = new AnimationParameters();
	public AnimationState CurrentState { get; private set; }
    
    public T GetParameter<T>(string name) => Parameters.GetParameter<T>(name);

    public void SetParameter<T>(string name, T value) => Parameters.SetParameter(name, value);

    public void Loop(AnimationEvent animationEvent)
	{
		animationEvent.animationState.time = animationEvent.floatParameter;
	}

	private void Awake()
	{
		animation = GetComponent<Animation>();
	}

	private void Start()
	{
        currentAnimState = AnimationManager.Instance.IdleState;
        currentAnimState.OnStateEnter(this);

        Debug.Assert(!string.IsNullOrEmpty(currentAnimState.AnimationName), "Animation name can not be null or empty.");
        animation.Play(currentAnimState.AnimationName);

        rootBone = transform.Find("Bip01");
		if (rootBone == null)
		{
			rootBone = transform.Find("Root Bone");
		}

		// Set some values
		previousRootPosition = rootBone.position;
		rootOffset = rootBone.localPosition;
		CalculateMovementSpeed();
	}

	private void Update()
	{
        // Ensure current state is not null
        Debug.Assert(currentAnimState != null, "Current animation state is null", gameObject);

		CurrentState = animation[currentAnimState.AnimationName];

		// Update current state and check if the current state  needs to transition to a new state
		var result = currentAnimState?.OnStateUpdate(this, CurrentState == null ? false : CurrentState.enabled);
		if(result != null)
		{
            currentAnimState = result.Target;
            currentAnimState.OnStateEnter(this);

            Debug.Assert(!string.IsNullOrEmpty(currentAnimState.AnimationName), "Animation name can not be null or empty.");

			CurrentState = animation[currentAnimState.AnimationName];

			// Set the time of the state to the EnterTime if specified
			if(animation[currentAnimState.AnimationName] != null)
			{
				animation.Play(currentAnimState.AnimationName);
				CurrentState.normalizedTime = result.HasExitTime ? result.EnterTime : CurrentState.normalizedTime;
			}
		}

		if(CurrentState != null)
		{
			LastUpdateTime = CurrentState.normalizedTime;
		}
	}

	private void LateUpdate()
	{
		rootBone.position = transform.position + transform.rotation * rootOffset; 
	}
	
	/// <summary>
	/// Calculates movement speed by the average distance between the Loop points of an animation. If no loop points exist, the start and stop positions are used instead.
	/// </summary>
	public float CalculateMovementSpeed()
	{
		// Get the state and clip from the relevant animation
		var state = animation["WalkForward"];

		if (state == null)
		{
			return 0;
		}

		var clip = state.clip;

		// Set start and end to the clip length, incase loop start and stop are not found
		var start = 0f;
		var stop = clip.length;

		// Get the start and end times
		foreach (var animationEvent in clip.events)
		{
			if (animationEvent.functionName != "Loop")
			{
				continue;
			}

			start = animationEvent.floatParameter;
			stop = animationEvent.time;
		}

		// Sample the animation at the start and end frames, and get the distance
		clip.SampleAnimation(gameObject, start);
		var startPosition = rootBone.position;

		clip.SampleAnimation(gameObject, stop);
		var endPosition = rootBone.position;

		var distance = (endPosition - startPosition).magnitude;
		var duration = stop - start;
		var averageSpeed = distance / duration;
		// animation scale = characterInput.moveSpeed / averageSpeed
		// animation scale = min(animationScale, 10);

		return averageSpeed;
	}
}