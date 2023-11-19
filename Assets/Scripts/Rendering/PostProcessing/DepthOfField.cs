using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class DepthOfField
{
    [Serializable]
    public class Settings
    {
        public float focalPlaneDistance = 1f;
        public float focalTransitionRange = 1f;
        [Range(0f, 10f)] public float blend = 0.11f;
        [Range(0f, 10f)] public float filterRadius;
    }

    private Settings settings;

    public DepthOfField(Settings settings)
    {
        this.settings = settings;
    }

    public void Render(Camera camera, CommandBuffer command, RenderTargetIdentifier depth, RenderTargetIdentifier scene)
    {
        var gNear = camera.nearClipPlane;

        var nb = gNear;
        var ne = settings.focalPlaneDistance - settings.focalTransitionRange;
        if (ne < gNear)
            ne = gNear;
        var fb = settings.focalPlaneDistance + settings.focalTransitionRange;
        var fe = camera.farClipPlane;

        var projMat = camera.projectionMatrix;
        var projParams = new Vector2(projMat[2, 2], projMat[3, 2]);

        var coc = Shader.PropertyToID("_Coc");
        var cocDesc = new RenderTextureDescriptor(camera.pixelWidth, camera.pixelHeight, RenderTextureFormat.RGFloat) { enableRandomWrite = true };

        {
            var genCoc = Resources.Load<ComputeShader>("DepthOfField/GenCoC");
            command.SetComputeFloatParam(genCoc, "maxRadius", settings.filterRadius);
            command.SetComputeFloatParam(genCoc, "blend", settings.blend);
            command.SetComputeFloatParam(genCoc, "nb", nb);
            command.SetComputeFloatParam(genCoc, "ne", ne);
            command.SetComputeFloatParam(genCoc, "fb", fb);
            command.SetComputeFloatParam(genCoc, "fe", fe);
            command.SetComputeFloatParam(genCoc, "zNear", camera.nearClipPlane);
            command.SetComputeFloatParam(genCoc, "zFar", camera.farClipPlane);
            command.SetComputeVectorParam(genCoc, "projParams", projParams);

            command.GetTemporaryRT(coc, cocDesc);

            command.SetComputeTextureParam(genCoc, 0, "_CameraDepth", depth);
            command.SetComputeTextureParam(genCoc, 0, "_Result", coc);
            command.DispatchNormalized(genCoc, 0, camera.pixelWidth, camera.pixelHeight, 1);
        }

        var cocHalf = Shader.PropertyToID("_CocHalf");
        var cocHalf1 = Shader.PropertyToID("_CocHalf1");
        var cocHalfDesc = new RenderTextureDescriptor(camera.pixelWidth, camera.pixelHeight, RenderTextureFormat.RFloat) { enableRandomWrite = true };

        {
            var filterNearCoc = Resources.Load<ComputeShader>("DepthOfField/MaxfilterNearCoC");
            command.GetTemporaryRT(cocHalf, cocHalfDesc);
            command.SetComputeVectorParam(filterNearCoc, "_Resolution", new Vector2(camera.pixelWidth >> 1, camera.pixelHeight >> 1));
            command.SetComputeTextureParam(filterNearCoc, 0, "NearCoCTexture", coc);
            command.SetComputeTextureParam(filterNearCoc, 0, "FilteredNearCoC", cocHalf);
            command.DispatchNormalized(filterNearCoc, 0, camera.pixelWidth >> 1, camera.pixelHeight >> 1, 1);

            command.GetTemporaryRT(cocHalf1, cocHalfDesc);
            command.SetComputeVectorParam(filterNearCoc, "_Resolution", new Vector2(camera.pixelWidth >> 1, camera.pixelHeight >> 1));
            command.SetComputeTextureParam(filterNearCoc, 1, "NearCoCTexture", coc);
            command.SetComputeTextureParam(filterNearCoc, 1, "FilteredNearCoC", cocHalf1);
            command.DispatchNormalized(filterNearCoc, 1, camera.pixelWidth >> 1, camera.pixelHeight >> 1, 1);
        }



        var downsample = Resources.Load<ComputeShader>("DepthOfField/Downsample");
        var horizontalDof = Resources.Load<ComputeShader>("DepthOfField/HorizontalDof");
        var composite = Resources.Load<ComputeShader>("DepthOfField/Composite");
    }
}
