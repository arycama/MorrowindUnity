using System;
using UnityEngine;

[Serializable]
public class ClusteredLightingSettings
{
    [SerializeField] private int tileSize = 16;
    [SerializeField] private int clusterDepth = 32;
    [SerializeField] private int maxLightsPerTile = 32;

    public int TileSize => tileSize;
    public int ClusterDepth => clusterDepth;
    public int MaxLightsPerTile => maxLightsPerTile;
}