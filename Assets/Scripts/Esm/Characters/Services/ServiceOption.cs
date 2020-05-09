using System;

public class ServiceOption
{
	public string Description { get; }
	public float Price { get; }
	public Func<bool> Action { get; }

	public ServiceOption(string description, Func<bool> action, float price = 0)
	{
		Description = description;
		Action = action;
		Price = price;
	}
}