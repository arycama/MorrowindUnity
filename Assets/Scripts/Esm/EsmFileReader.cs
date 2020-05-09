using System.IO;
using UnityEngine;

public class EsmFileReader : MonoBehaviour
{
	// Can change to an array later to read multiple files
	[SerializeField]
	private string path = "C:/Program Files (x86)/Steam/SteamApps/common/Morrowind/Data Files/Morrowind.esm";

	public static System.IO.BinaryReader reader;

	private void Awake()
	{
		var fileStream = File.Open(path, FileMode.Open);
		reader = new System.IO.BinaryReader(fileStream);

		while (reader.BaseStream.Position < reader.BaseStream.Length)
			Record.Create(reader);
	}

	private void OnDestroy() => reader?.Dispose();
}