#pragma warning disable 0108

using UnityEngine;

public abstract class NpcState
{
	public abstract void OnStateEnter(NpcInput input);

	public abstract void OnStateUpdate(NpcInput input);

	public abstract void OnStateExit(NpcInput input);
}
