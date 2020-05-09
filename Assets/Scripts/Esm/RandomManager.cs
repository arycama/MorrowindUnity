using System.Collections;
using System.Collections.Generic;
using Esm;
using UnityEngine;

// Temporary placeholder for the Random100 section of the Main script. Sets the global named "Random 100" to a random number between 0 and 100 each frame.
public class RandomManager : MonoBehaviour
{
	[SerializeField]
	private Global global;

	private void Start()
	{
		global = Record.GetRecord<Global>("Random100");
	}

	private void Update()
	{
		global.Value = Random.Range(0, 101);
	}
}