using System;
using UnityEngine;
using UnityEngine.Rendering;

public class Bloom
{
    [Serializable]
    public class Settings
    {
        [SerializeField, Range(0f, 1f)] private float strength = 0.125f;
        [SerializeField, Range(2, 8)] private int maxMips = 6;

        public float Strength => strength;
        public int MaxMips => maxMips;
    }

    private static readonly IndexedShaderPropertyId bloomIds = new("Bloom");

    private Settings settings;
    private Material material;

    public Bloom(Settings settings)
    {
        this.settings = settings;
        material = new(Shader.Find("Hidden/Bloom")) { hideFlags = HideFlags.HideAndDontSave };
    }

    public RenderTargetIdentifier Render(Camera camera, CommandBuffer command, RenderTargetIdentifier input)
    {
        using var profilerScope = command.BeginScopedSample("Bloom");

        var mipCount = Mathf.Min(settings.MaxMips, (int)Mathf.Log(Mathf.Max(camera.pixelWidth, camera.pixelHeight), 2));

        // Downsample
        for (var i = 0; i < mipCount; i++)
        {
            if (i == 0)
            {
                command.SetGlobalTexture("_MainTex", input);
            }
            else
            {
                var inputId = bloomIds.GetProperty(i - 1);
                command.SetGlobalTexture("_MainTex", inputId);
            }

            var width = Mathf.Max(1, camera.pixelWidth >> (i + 1));
            var height = Mathf.Max(1, camera.pixelHeight >> (i + 1));
            var desc = new RenderTextureDescriptor(width, height, RenderTextureFormat.RGB111110Float) { enableRandomWrite = true };

            var resultId = bloomIds.GetProperty(i);
            command.GetTemporaryRT(resultId, desc);

            command.SetRenderTarget(resultId);
            command.SetGlobalVector("_RcpResolution", new Vector2(1.0f / width, 1.0f / height));
            command.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3);
        }

        // Upsample
        for (var i = mipCount - 1; i > 0; i--)
        {
            var inputId = bloomIds.GetProperty(i);
            command.SetGlobalFloat("_Strength", settings.Strength);
            command.SetGlobalTexture("_MainTex", inputId);

            if (i > 0)
            {
                var resultId = bloomIds.GetProperty(i - 1);
                command.SetRenderTarget(resultId);
            }
            else
            {
                command.SetRenderTarget(input);
            }

            var width = Mathf.Max(1, camera.pixelWidth >> i);
            var height = Mathf.Max(1, camera.pixelHeight >> i);
            command.SetGlobalVector("_RcpResolution", new Vector2(1.0f / width, 1.0f / height));

            command.DrawProcedural(Matrix4x4.identity, material, 1, MeshTopology.Triangles, 3);

            // Don't release the final result as we pass it to the next pass
            if (i > 1)
                command.ReleaseTemporaryRT(inputId);
        }

        return bloomIds.GetProperty(1);
    }
}
