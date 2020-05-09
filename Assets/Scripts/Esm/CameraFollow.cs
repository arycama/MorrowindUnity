using System.Collections;
using System.Collections.Generic;
using UnityEngine;

class CameraFollow : MonoBehaviour
{
	[SerializeField]
	private Vector3 offset;

	[SerializeField]
	private Transform target;

	[SerializeField]
	private float rotateSpeed;

	public Transform Target
	{
		set
		{
			target = value;
			LateUpdate();
		}
	}

	private void LateUpdate()
	{
		if(target == null || Time.timeScale == 0)
		{
			return;
		}

		var position = target.position + target.rotation * offset;
		transform.position = position;

		var rotation = new Vector3(transform.eulerAngles.x + -Input.GetAxis("Mouse Y") * rotateSpeed, target.eulerAngles.y);
		transform.eulerAngles = rotation;
	}
}