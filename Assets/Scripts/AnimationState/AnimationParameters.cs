#pragma warning disable 0108

using System.Collections.Generic;

public class AnimationParameters
{
    private readonly Dictionary<string, bool> boolParameters = new Dictionary<string, bool>();
    private readonly Dictionary<string, int> intParameters = new Dictionary<string, int>();
    private readonly Dictionary<string, float> floatParameters = new Dictionary<string, float>();

    public bool GetBoolParameter(string name)
    {
        return boolParameters.TryGetValue(name, out var result) ? result : default;
    }

    public int GetIntParameter(string name)
    {
        return intParameters.TryGetValue(name, out var result) ? result : default;
    }

    public float GetFloatParameter(string name)
    {
        return floatParameters.TryGetValue(name, out var result) ? result : default;
    }

    public void SetBoolParameter(string name, bool value)
    {
        boolParameters[name] = value;
    }

    public void SetIntParameter(string name, int value)
    {
        intParameters[name] = value;
    }

    public void SetFloatParameter(string name, float value)
    {
        floatParameters[name] = value;
    }
}
