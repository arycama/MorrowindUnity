using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraDepthTextureMode : MonoBehaviour
{
	[SerializeField]
	private DepthTextureMode textureMode;

	private void Awake()
	{
		GetComponent<Camera>().depthTextureMode = textureMode;
	}
}