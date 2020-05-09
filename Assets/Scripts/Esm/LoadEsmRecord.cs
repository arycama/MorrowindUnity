using Esm;
using UnityEngine;

class LoadEsmRecord : MonoBehaviour
{
	[SerializeField]
	private string path;

	private void OnValidate()
	{
		name = path;
	}

	private void Start()
	{
		var record = Record.GetRecord<CreatableRecord>(path);
		var gameObject = record.CreateGameObject(null);
		gameObject.transform.position = transform.position;
		gameObject.transform.rotation = transform.rotation;
		Destroy(this.gameObject);
	}
}