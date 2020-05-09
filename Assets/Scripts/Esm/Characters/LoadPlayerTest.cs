using UnityEngine;

public class LoadPlayerTest : MonoBehaviour
{
	private void Start()
	{
		var record = Record.GetRecord<CreatableRecord>("player");
		var gameObject = record.CreateGameObject(null);

		gameObject.transform.position = transform.position;
		gameObject.transform.rotation = transform.rotation;

		Camera.main.GetComponent<CameraFollow>().Target = gameObject.transform;
	}
}