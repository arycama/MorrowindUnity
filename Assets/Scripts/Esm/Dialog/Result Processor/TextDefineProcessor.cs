using System.Text;
using System.Linq;

public static class TextDefineProcessor
{
	private static readonly StringBuilder resultBuilder = new StringBuilder(), defineBuilder = new StringBuilder();

	public static string ProcessText(string text, Character player, Character npc)
	{
		// Loop through all characters of the original string
		var isReadingDefine = false;
		for(var i = 0; i < text.Length; i++)
		{
			if (isReadingDefine)
			{
				defineBuilder.Append(text[i]);

				if (!char.IsLetterOrDigit(text[i]))
				{
					var currentDefine = defineBuilder.ToString();
					defineBuilder.Clear();
					resultBuilder.Append(ProcessDefineInternal(currentDefine, player, npc));
					isReadingDefine = false;
				}
			}
			else
			{
				if (text[i] == '%')
					isReadingDefine = true;
				else
					resultBuilder.Append(text[i]);
			}
		}

		var result = resultBuilder.ToString();
		resultBuilder.Clear();
		return result;
	}

	private static string ProcessDefineInternal(string currentDefine, Character player, Character npc)
	{
		switch (currentDefine)
		{
			// The speaker's name.
			case "name":
			case "Name":
				return npc.FullName;

			// The player's name.
			case "pcname":
			case "PCName":
				return player.FullName;

			// The speaker's race.
			case "race":
			case "Race":
				return npc.NpcRecord.Race.Name;

			// The player's race.
			case "pcrace":
			case "PCRace":
				return player.NpcRecord.Race.Name;

			// The speaker's class.
			case "class":
			case "Class":
				return npc.NpcRecord.Class.Name;

			// The player's class.
			case "pcclass":
			case "PCClass":
				return player.NpcRecord.Class.Name;

			// The speaker's faction. If they have no faction, it will be blank.
			case "faction":
			case "Faction":
				return npc.Factions.FirstOrDefault().Key.Name;

			// The speaker's rank.
			case "rank":
			case "Rank":
				return npc.RankName;

			// The player's rank in the spaker's faction.
			//case "PCRank":
			//return listener.CharacterFaction.RankName;
			//break;

			// The player's next rank in the speaker's faction.
			//case "NextPCRank":
			//return text.Insert(index, speaker.Name);
			//break;

			// cell the player is currently in.
			case "cell":
			case "Cell":
				return player.gameObject.scene.name;

			// Any global variable value. Floats display as 1.1, such as %Gamehour
			//case "Global":
			//Debug.Log("Contains:" + word);
			//break;
			default:
				return currentDefine;
		}
	}
}