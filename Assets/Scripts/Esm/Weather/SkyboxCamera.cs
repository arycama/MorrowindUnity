using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SkyboxCamera : MonoBehaviour
{
	[SerializeField]
	private Transform target;
	
	private void Update ()
	{
		transform.rotation = target.rotation;
	}
}