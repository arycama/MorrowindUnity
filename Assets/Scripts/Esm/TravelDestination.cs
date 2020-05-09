using System;
using UnityEngine;


[Serializable]
public class TravelDestination
{
	[SerializeField]
	private string cell;

	[SerializeField]
	private DoorExitData doorExitData;

	public TravelDestination(DoorExitData doorExitData, string cell = null)
	{
		this.doorExitData = doorExitData;
		this.cell = cell;
	}

	public string Cell => cell;
	public DoorExitData DoorExitData => doorExitData;
}