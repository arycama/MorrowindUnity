using System;

public class ListUIOption
{
	public string Description { get; }
	public Action Action { get; }
	public bool IsEnabled { get; set; }

	public ListUIOption(string description, Action action, bool isEnabled = true)
	{
		Description = description;
		Action = action;
		IsEnabled = isEnabled;
	}
}