using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public class MorrowindParser
{
	private readonly bool hasBegun = false, hasEnded = false;

	private readonly string text;
	private StringBuilder token;
	private StringReader reader;

	public MorrowindParser(string text)
	{
		this.text = text;
	}

	public void Parse()
	{
		// Loop over each character
		token = new StringBuilder();

		int charBytes;
		using (reader = new StringReader(text))
		while ((charBytes = reader.Read()) > -1)
		{
			var character = System.Convert.ToChar(charBytes);

			// Process spaces, commas, brackets etc
			switch (character)
			{
				case ' ':
					ProcessToken();
					break;
				default:
					token.Append(character);
					break;
			}
		}
	}

	private void ProcessToken()
	{
		switch (token.ToString())
		{
			case "begin":
				GetScriptName();
				break;
			default:
				break;
		}

		// Might not always want to do this
		token.Clear();
	}

	// Gets the name of the script by reading until a new line is reached 
	private void GetScriptName()
	{
		token.Clear();
		int charBytes;
		while ((charBytes = reader.Read()) > -1)
		{
			var character = System.Convert.ToChar(charBytes);
			switch (character)
			{
				// Spaces, new lines, tabs etc. mean end of line. This should be an "is letter or underscore" thing.
				case '\n':
				case '\r':
				case '\t':
					break;
				default:
					token.Append(character);
					break;
			}
		}
	}
}