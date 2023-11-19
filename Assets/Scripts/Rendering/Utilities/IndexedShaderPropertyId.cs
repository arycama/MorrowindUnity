using System;
using System.Collections.Generic;
using UnityEngine;

public class IndexedShaderPropertyId
{
    private List<int> properties = new();
    private string id;

    public IndexedShaderPropertyId(string id)
    {
        this.id = id;
    }

    public int GetProperty(int index)
    {
        if (index < 0)
            throw new ArgumentOutOfRangeException(index.ToString());

        while (properties.Count <= index)
            properties.Add(Shader.PropertyToID($"{id}{properties.Count}"));

        return properties[index];
    }
}
