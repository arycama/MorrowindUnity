using System;
using System.Collections.Generic;

public class IndexedString
{
    private List<string> strings = new();
    private string id;

    public IndexedString(string id)
    {
        this.id = id;
    }

    public string GetString(int index)
    {
        if (index < 0)
            throw new ArgumentOutOfRangeException(index.ToString());

        while (strings.Count <= index)
            strings.Add($"{id}{strings.Count}");

        return strings[index];
    }
}
