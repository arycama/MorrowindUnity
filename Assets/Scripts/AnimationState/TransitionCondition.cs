using System;
using UnityEngine;

[Serializable]
public class TransitionCondition
{
	[SerializeField]
	private string name;

	[SerializeField]
	private TransitionType type = TransitionType.Bool;

	[SerializeField]
	private CompareOp compareOp = CompareOp.Equal;

	[SerializeField]
	private bool boolValue;

	[SerializeField]
	private float floatValue;

	[SerializeField]
	private int intValue;

	public bool CheckTransition(CharacterAnimation animation, bool currentStateEnabled)
	{
		switch (type)
		{
			case TransitionType.None:
				return true;
			case TransitionType.Bool:
				return animation.Parameters.GetBoolParameter(name) == boolValue;
			case TransitionType.Float:
				{
					var parameter = animation.Parameters.GetFloatParameter(name);
					switch (compareOp)
					{
						case CompareOp.Equal:
							return parameter == floatValue;
						case CompareOp.NotEqual:
							return parameter != floatValue;
						case CompareOp.GreaterThan:
							return parameter > floatValue;
						case CompareOp.GreaterThanOrEqual:
							return parameter >= floatValue;
						case CompareOp.LessThan:
							return parameter < floatValue;
						case CompareOp.LessThanOrEqual:
							return parameter <= floatValue;
						default:
							throw new NotImplementedException(compareOp.ToString());
					}
				}
			case TransitionType.Int:
				{
					var parameter = animation.Parameters.GetIntParameter(name);
					switch (compareOp)
					{
						case CompareOp.Equal:
							return parameter == intValue;
						case CompareOp.NotEqual:
							return parameter != intValue;
						case CompareOp.GreaterThan:
							return parameter > intValue;
						case CompareOp.GreaterThanOrEqual:
							return parameter >= intValue;
						case CompareOp.LessThan:
							return parameter < intValue;
						case CompareOp.LessThanOrEqual:
							return parameter <= intValue;
						default:
							throw new NotImplementedException(compareOp.ToString());
					}
				}
			case TransitionType.String:
				//parameter.stringValue = animation.GetParameter<string>(name);
				break;
			case TransitionType.Time:
				return !currentStateEnabled;
		}

		return false;
	}
}