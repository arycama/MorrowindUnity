#pragma warning disable 0108

using UnityEngine;

public class AttackCreatureState : NpcState
{
	private bool attacking;

	public override void OnStateEnter(NpcInput input)
	{
	}

	public override void OnStateExit(NpcInput input)
	{
	}

	public override void OnStateUpdate(NpcInput input)
	{
		var targetVector = input.Target.transform.position - input.transform.position;
		var distance = targetVector.magnitude;

		if (distance > input.DistanceThreshold)
		{
			input.Run = true;
			input.Forward = true;
			input.Attack = false;

			targetVector.y = 0; // Remove the y component so the NPC doesn't rotate upwards

			var rotation = Quaternion.LookRotation(targetVector.normalized);
			input.transform.rotation = Quaternion.RotateTowards(input.transform.rotation, rotation, input.RotateSpeed * Time.deltaTime);
		}
		else
		{
			input.Run = false;
			input.Forward = false;

			if (attacking)
			{
				input.Attack = false;
				input.Animation.SetParameter("Attack", input.Attack);
				attacking = false;
			}
			else
			{
				input.Attack = true;
				input.Animation.SetParameter("Attack", input.Attack);

				attacking = true;
			}
		}
	}
}
