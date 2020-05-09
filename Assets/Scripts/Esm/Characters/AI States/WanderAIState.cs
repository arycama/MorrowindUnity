#pragma warning disable 0108

using UnityEngine;
using Esm;

public class WanderAIState : NpcState
{
	[SerializeField]
	private float time;

	[SerializeField]
	private Vector3 targetPos;

	[SerializeField]
	private Pathgrid pathgrid;

	[SerializeField]
	private PathgridPoint movementTarget;

	public override void OnStateEnter(NpcInput input)
	{
		pathgrid = Pathgrid.LoadPathGrid(input.transform.position);
		if (pathgrid != null)
		{
			movementTarget = pathgrid.GetNearestPoint(input.transform.position);
			var offset = new Vector3(Mathf.FloorToInt(input.transform.position.x / 8192) * 8192, 0, Mathf.FloorToInt(input.transform.position.z / 8192) * 8192);
			targetPos = movementTarget.Position + offset; ;
		}
		else
		{
			var pos = Random.insideUnitCircle * input.WanderData.Distance;
			targetPos = input.transform.position + new Vector3(pos.x, 0, pos.y);
		}
	}

	public override void OnStateExit(NpcInput input)
	{
	}

	public override void OnStateUpdate(NpcInput input)
	{
		// Return if npc has been wandering for long enough
		if(time > input.WanderData.Duration)
		{
			input.Forward = false;
			input.Animation.SetParameter("Forward", false);
			input.NextState = new IdleAIState();
			return;
		}

		input.Forward = true;
		input.Animation.SetParameter("Forward", true);

		var targetVector = targetPos - input.transform.position;

		// Get a new point if we have reached the current point
		if (targetVector.magnitude < input.DistanceThreshold)
		{
			if (pathgrid != null)
			{
				movementTarget = movementTarget.GetConnectedPoint();
				var offset = new Vector3(Mathf.FloorToInt(input.transform.position.x / 8192) * 8192, 0, Mathf.FloorToInt(input.transform.position.z / 8192) * 8192);
				targetPos = movementTarget.Position + offset;
			}
			else
			{
				var pos = Random.insideUnitCircle * input.WanderData.Distance;
				targetPos = input.transform.position + new Vector3(pos.x, 0, pos.y);
			}
		}
		else
		{
			targetVector.y = 0; // Remove the y component so the NPC doesn't rotate upwards

			var rotation = Quaternion.LookRotation(targetVector.normalized);
			input.transform.rotation = Quaternion.RotateTowards(input.transform.rotation, rotation, input.RotateSpeed * Time.deltaTime);

			time += Time.deltaTime;
		}
	}
}
