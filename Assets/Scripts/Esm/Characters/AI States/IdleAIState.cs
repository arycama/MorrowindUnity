#pragma warning disable 0108

using UnityEngine;

public class IdleAIState : NpcState
{
	[SerializeField]
	private int idle;

	public override void OnStateEnter(NpcInput input)
	{
		if(input.WanderData == null)
		{
			input.NextState = null;
			return;
		}

		idle = input.WanderData.GetIdle();
		if (idle > 1)
		{
			input.Animation.SetParameter("Idle", idle);
		}
		else
		{
			input.NextState = new WanderAIState();
			input.NextState.OnStateEnter(input);
		}
	}

	public override void OnStateExit(NpcInput input)
	{
		// Set idle back to 0 to clean it up
		input.Animation.SetParameter("Idle", 0);
	}

	public override void OnStateUpdate(NpcInput input)
	{
		// Idle is set to -1 while playing, or > 1 when that idle should be played
		if(input.Animation.GetParameter<int>("Idle") == 0)
		{
			input.NextState = new IdleAIState();
		}
	}
}