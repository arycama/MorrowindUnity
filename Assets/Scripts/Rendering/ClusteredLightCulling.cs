using System;
using UnityEngine;
using UnityEngine.Rendering;

public class ClusteredLightCulling
{
    [Serializable]
    public class Settings
    {
        [SerializeField] private int tileSize = 16;
        [SerializeField] private int clusterDepth = 32;
        [SerializeField] private int maxLightsPerTile = 32;

        public int TileSize => tileSize;
        public int ClusterDepth => clusterDepth;
        public int MaxLightsPerTile => maxLightsPerTile;
    }

    private static readonly int lightClusterIndicesId = Shader.PropertyToID("_LightClusterIndices");

    private Settings settings;
    private ComputeBuffer counterBuffer;
    private ComputeBuffer lightList;

    private static readonly uint[] zeroArray = new uint[1] { 0 };

    private int DivRoundUp(int x, int y) => (x + y - 1) / y;

    public ClusteredLightCulling(Settings settings)
    {
        this.settings = settings;
        counterBuffer = new ComputeBuffer(1, sizeof(uint)) { name = nameof(counterBuffer) };
    }

    public void Release()
    {
        lightList?.Release();
        counterBuffer.Release();
    }

    public void Render(CommandBuffer command, Camera camera)
    {
        using var profilerScope = command.BeginScopedSample("Clustered Light Culling");

        var clusterWidth = DivRoundUp(camera.pixelWidth, settings.TileSize);
        var clusterHeight = DivRoundUp(camera.pixelHeight, settings.TileSize);
        var clusterCount = clusterWidth * clusterHeight * settings.ClusterDepth;

        GraphicsUtilities.SafeExpand(ref lightList, clusterCount * settings.MaxLightsPerTile, sizeof(int), ComputeBufferType.Default);

        var descriptor = new RenderTextureDescriptor(clusterWidth, clusterHeight, RenderTextureFormat.RGInt)
        {
            dimension = TextureDimension.Tex3D,
            enableRandomWrite = true,
            volumeDepth = settings.ClusterDepth
        };

        var clusterScale = settings.ClusterDepth / Mathf.Log(camera.farClipPlane / camera.nearClipPlane, 2f);
        var clusterBias = -(settings.ClusterDepth * Mathf.Log(camera.nearClipPlane, 2f) / Mathf.Log(camera.farClipPlane / camera.nearClipPlane, 2f));

        var computeShader = Resources.Load<ComputeShader>("ClusteredLightCulling");

        command.GetTemporaryRT(lightClusterIndicesId, descriptor);
        command.SetBufferData(counterBuffer, zeroArray);
        //command.SetComputeBufferParam(computeShader, 0, "_LightData", lightData);
        command.SetComputeBufferParam(computeShader, 0, "_LightCounter", counterBuffer);
        command.SetComputeBufferParam(computeShader, 0, "_LightClusterListWrite", lightList);
        command.SetComputeTextureParam(computeShader, 0, "_LightClusterIndicesWrite", lightClusterIndicesId);
        //command.SetComputeIntParam(computeShader, "_LightCount", lightData.Count);
        command.SetComputeIntParam(computeShader, "_TileSize", settings.TileSize);
        command.SetComputeFloatParam(computeShader, "_RcpClusterDepth", 1f / settings.ClusterDepth);
        command.DispatchNormalized(computeShader, 0, clusterWidth, clusterHeight, settings.ClusterDepth);

        command.SetGlobalTexture(lightClusterIndicesId, lightClusterIndicesId);
        command.SetGlobalBuffer("_LightClusterList", lightList);
        command.SetGlobalFloat("_ClusterScale", clusterScale);
        command.SetGlobalFloat("_ClusterBias", clusterBias);
        command.SetGlobalInt("_TileSize", settings.TileSize);
    }

    public void CameraRenderingComplete(CommandBuffer command)
    {
        command.ReleaseTemporaryRT(lightClusterIndicesId);
    }
}
