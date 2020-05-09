using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

public class IniManager : MonoBehaviour
{
	private static Dictionary<string, Dictionary<string, string>> Settings = new Dictionary<string, Dictionary<string, string>>();

	[SerializeField]
	private string path = "C:/Program Files (x86)/Steam/SteamApps/common/Morrowind/Morrowind.ini";

	public static float GetFloat(string section, string setting)
	{
		var value = GetString(section, setting);
		return float.Parse(value);
	}

	public static int GetInt(string section, string setting)
	{
		var value = GetString(section, setting);
		return int.Parse(value);
	}

	public static Color32 GetColor(string section, string setting)
	{
		var value = GetString(section, setting);

		// Split the value by commas.
		var components = value.Split(',');
		byte r = 0, g = 0, b = 0, a = 255;

		switch (components.Length)
		{
			case 0:
				Debug.LogWarningFormat("Setting {0} did not contain any color values", setting);
				break;
			case 1:
				Debug.LogWarningFormat("Setting {0} only contained 1 value", setting);
				r = byte.Parse(components[0]);
				break;
			case 2:
				Debug.LogWarningFormat("Setting {0} only contained 2 values", setting);
				r = byte.Parse(components[0]);
				g = byte.Parse(components[1]);
				break;
			case 3:
				r = byte.Parse(components[0]);
				g = byte.Parse(components[1]);
				b = byte.Parse(components[2]);
				break;
			case 4:
				r = byte.Parse(components[0]);
				g = byte.Parse(components[1]);
				b = byte.Parse(components[2]);
				a = byte.Parse(components[3]);
				break;
			default:
				Debug.LogWarningFormat("Setting {0} only contained more than 4 values", setting);
				r = byte.Parse(components[0]);
				g = byte.Parse(components[1]);
				b = byte.Parse(components[2]);
				a = byte.Parse(components[3]);
				break;
		}

		return new Color32(r, g, b, a);
	}

	public static string GetString(string section, string setting)
	{
		// Get the section, or throw exception if it does not exist
		Dictionary<string, string> sectionSettings;
		if (!Settings.TryGetValue(section, out sectionSettings))
		{
			throw new KeyNotFoundException(section);
		}

		// Get the value, or throw exception if it does not exist
		string value;
		if (!sectionSettings.TryGetValue(setting, out value))
		{
			throw new KeyNotFoundException($"[{section}] {setting}");
		}

		return value;
	}

	private void Awake()
	{
		using (var file = new StreamReader(path))
		{
			string section = string.Empty;

			int lineNumber = -1;
			string line;
			while ((line = file.ReadLine()) != null)
			{
				lineNumber++;

				// Remove whitespace and skip if the line is empty
				line = line.Trim();
				if (line.Length == 0)
				{
					continue;
				}

				// Skip lines that start with a semi-colon, as these are comments
				if (line.StartsWith(";"))
				{
					continue;
				}

				// Each section is a string enclosed in square brackets "[Section]"
				if (line.StartsWith("[") && line.EndsWith("]"))
				{
					section = line.Substring(1, line.Length - 2);
					Settings.Add(section, new Dictionary<string, string>());
					continue;
				}

				// Split the line according to equals signs
				var strings = line.Split('=');

				// Some settings may have empty values
				if (strings.Length == 1)
				{
					Settings[section].Add(strings[0], null);
					continue;
				}

				// Log a warning if more than one equals sign is detected
				if(strings.Length > 2)
				{
					Debug.LogWarningFormat("Line {0} has more than one '=' sign. Any subsequent values will be ignored. ({1})", lineNumber, line);
				}

				Settings[section].Add(strings[0], strings[1]);
			}
		}
	}
}