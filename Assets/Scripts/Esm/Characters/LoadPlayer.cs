using System.Collections;
using System.Collections.Generic;
using Esm;
using UnityEngine;

class LoadPlayer : MonoBehaviour
{
	private void Awake()
	{
		var record = Record.GetRecord<CreatableRecord>("player");
		var gameObject = record.CreateGameObject(null);

		gameObject.transform.position = transform.position;
		gameObject.transform.rotation = transform.rotation;

		CellManager.Target = gameObject.transform;
		Camera.main.GetComponentInParent<CameraFollow>().Target = gameObject.transform;
		gameObject.AddComponent<Journal>();

		gameObject.GetComponent<IInventory>().AddItem(EconomyManager.Gold, 1000);
	}
}