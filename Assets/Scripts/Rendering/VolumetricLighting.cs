using UnityEngine;
using UnityEngine.Rendering;

public class VolumetricLighting
{
    private static readonly int volumetricLightingId = Shader.PropertyToID("_VolumetricLighting");
    private CameraTextureCache volumetricLightingTextureCache = new();

    public void Release()
    {
        volumetricLightingTextureCache.Dispose();
    }

    public void Render(Camera camera, CommandBuffer command, int tileSize, int depthSlices, int frameCount, float blurSigma, bool nonLinearDepth)
    {
        using var profilerScope = command.BeginScopedSample("Volumetric Lighting");

        var width = Mathf.CeilToInt(camera.pixelWidth / (float)tileSize);
        var height = Mathf.CeilToInt(camera.pixelHeight / (float)tileSize);
        var depth = depthSlices;
        var volumetricLightingDescriptor = new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGBHalf)
        {
            dimension = TextureDimension.Tex3D,
            enableRandomWrite = true,
            volumeDepth = depth,
        };

        volumetricLightingTextureCache.GetTexture(camera, volumetricLightingDescriptor, out var volumetricLightingCurrent, out var volumetricLightingHistory, frameCount);

        var computeShader = Resources.Load<ComputeShader>("VolumetricLighting");
        command.SetGlobalFloat("_VolumeWidth", width);
        command.SetGlobalFloat("_VolumeHeight", height);
        command.SetGlobalFloat("_VolumeSlices", depth);
        command.SetGlobalFloat("_VolumeDepth", camera.farClipPlane);
        command.SetGlobalFloat("_NonLinearDepth", nonLinearDepth ? 1.0f : 0.0f);
        command.SetComputeFloatParam(computeShader, "_BlurSigma", blurSigma);
        command.SetComputeIntParam(computeShader, "_VolumeTileSize", tileSize);

        command.SetComputeTextureParam(computeShader, 0, "_Input", volumetricLightingHistory);
        command.SetComputeTextureParam(computeShader, 0, "_Result", volumetricLightingCurrent);
        command.DispatchNormalized(computeShader, 0, width, height, depth);
        command.GetTemporaryRT(volumetricLightingId, volumetricLightingDescriptor);

        // Filter X
        command.SetComputeTextureParam(computeShader, 1, "_Input", volumetricLightingCurrent);
        command.SetComputeTextureParam(computeShader, 1, "_Result", volumetricLightingId);
        command.DispatchNormalized(computeShader, 1, width, height, depth);

        // Filter Y
        command.SetComputeTextureParam(computeShader, 2, "_Input", volumetricLightingId);
        command.SetComputeTextureParam(computeShader, 2, "_Result", volumetricLightingHistory);
        command.DispatchNormalized(computeShader, 2, width, height, depth);

        command.SetComputeTextureParam(computeShader, 3, "_Input", volumetricLightingHistory);
        command.SetComputeTextureParam(computeShader, 3, "_Result", volumetricLightingId);
        command.DispatchNormalized(computeShader, 3, width, height, 1);
        command.SetGlobalTexture("_VolumetricLighting", volumetricLightingId);
    }

    public void CameraRenderComplete(CommandBuffer command)
    {
        command.ReleaseTemporaryRT(volumetricLightingId);
    }
}