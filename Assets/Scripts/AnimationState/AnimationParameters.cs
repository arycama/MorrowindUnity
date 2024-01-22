#pragma warning disable 0108

using System.Collections.Generic;

public class AnimationParameters
{
    private readonly Dictionary<string, object> animationParameters = new Dictionary<string, object>();

    public T GetParameter<T>(string name)
    {
        object value;
        if (!animationParameters.TryGetValue(name, out value))
        {
            value = default(T);
            animationParameters.Add(name, value);
        }

        return (T)value;
    }

    public void SetParameter<T>(string name, T value)
    {
        animationParameters[name] = value;
    }
}
