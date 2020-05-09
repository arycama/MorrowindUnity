using System;
using UnityEngine;

[Serializable]
public class SoundName
{
	[SerializeField]
	private SoundRecord sound;

	[SerializeField]
	private int chance;

	public SoundName(System.IO.BinaryReader reader, int size)
	{
		var bytes = reader.ReadBytes(32);

		// Has lots of junk after string, so find the first null character
		int index = 32;
		for(var i = 0; i < bytes.Length; i++)
		{
			if (bytes[i] == 0)
			{
				index = i;
				break;
			}
		}

		var sound = System.Text.Encoding.ASCII.GetString(bytes, 0, index );
		this.sound = Record.GetRecord<SoundRecord>(sound);
		chance = reader.ReadByte();
	}

	public SoundRecord Sound => sound;
	public int Chance => chance;
}