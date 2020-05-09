#pragma warning disable 0108

using System;
using System.Collections;
using Esm;
using UnityEngine;

[SelectionBase]
public class Door : RecordBehaviour<Door, DoorRecord>, IActivatable, ILockable
{
	[SerializeField]
	private DoorExitData doorData;

	[SerializeField]
	private string loadCell;

	[SerializeField]
	private LockData lockData;

	private bool isOpen, isMoving;
	private Quaternion closedRotation, openRotation;

	private InfoPanel infoPanel;

	protected override void Initialize(DoorRecord record, ReferenceData referenceData)
	{
		base.Initialize(record, referenceData);

		doorData = referenceData.DoorExitData;
		loadCell = referenceData.LoadCell;
		lockData = new LockData(referenceData.LockLevel, referenceData.Trap, referenceData.Key);
	}

	private void Start()
	{
		// Can't do this in initialize as the objects transform doesn't get set until after the method returns
		closedRotation = transform.rotation;
		openRotation = closedRotation * Quaternion.Euler(Vector3.up * 90f);
	}

	private IEnumerator Open()
	{
		isMoving = true;

		while (Quaternion.Angle(transform.rotation, openRotation) > 1f)
		{
			transform.rotation = Quaternion.Slerp(transform.rotation, openRotation, Time.deltaTime * 5f);
			yield return new WaitForEndOfFrame();
		}

		isOpen = true;
		isMoving = false;
	}

	private IEnumerator Close()
	{
		isMoving = true;

		while (Quaternion.Angle(transform.rotation, closedRotation) > 1f)
		{
			transform.rotation = Quaternion.Slerp(transform.rotation, closedRotation, Time.deltaTime * 5f);
			yield return new WaitForEndOfFrame();
		}

		isOpen = false;
		isMoving = false;
	}

	void IActivatable.Activate(GameObject target)
	{
		// Ignroe doors that are already moving
		// Maybe later change this
		if (isMoving)
		{
			return;
		}

		// Check if the door is locked
		if (lockData.CheckLock(target, "LockedDoor"))
		{
			return;
		}

		if (isOpen)
		{
			record.CloseSound?.PlaySoundAtPoint(transform.position);
			StartCoroutine(Close());
		}
		else
		{
			record.OpenSound?.PlaySoundAtPoint(transform.position);
			StartCoroutine(Open());
		}

		OpenDoor(target.transform);
	}

	public void DisplayInfo()
	{
		infoPanel = InfoPanel.Create(new Vector2(0.5f, 0.5f));
		infoPanel.AddTitle(record.FullName);
		DisplayDoorInfo();
		lockData.DisplayLockInfo(infoPanel);
	}

	public void CloseInfo()
	{
		if (infoPanel == null)
		{
			return;
		}

		Destroy(infoPanel.gameObject);
		infoPanel = null;
	}

	bool ILockable.Unlock(float chance)
	{
		if(lockData.Unlock(transform.position, chance))
		{
			CloseInfo();
			DisplayInfo();
			return true;
		}

		return false;
	}

	private void OpenDoor(Transform target)
	{
		if (doorData == null)
		{
			return;
		}

		doorData.OpenDoor(target);

		CellManager.LoadCell(loadCell);
	}

	private void DisplayDoorInfo()
	{
		if (doorData == null)
		{
			return;
		}

		infoPanel.AddText("to");

		if (string.IsNullOrEmpty(loadCell))
		{
			var loadCell = CellManager.GetCellName(doorData.Position);
			infoPanel.AddText(loadCell);
		}
		else
		{
			infoPanel.AddText(loadCell);
		}
	}
}