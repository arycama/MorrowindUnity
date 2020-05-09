using UnityEngine;

/// <summary>
/// Contains a persistent Singleton of type T which is automatically created if it doesn't exist. 
/// </summary>
/// <typeparam name="T"> The type of MonoBehaviour to Create or Get a singleton of. </typeparam>
public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
	public static T Instance { get; private set; }

	private void Awake()
	{
		Instance = this as T;
	}
}