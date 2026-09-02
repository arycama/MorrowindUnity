using System;
using UnityEngine;

[AttributeUsage(AttributeTargets.Field)]
public class Pow2Attribute : PropertyAttribute
{
	public int MinValue { get; } = 1;
	public int MaxValue { get; }

	public Pow2Attribute(int maxValue)
	{
		MaxValue = maxValue;
	}

	public Pow2Attribute(int minValue, int maxValue)
	{
		MinValue = minValue;
		MaxValue = maxValue;
	}
}
