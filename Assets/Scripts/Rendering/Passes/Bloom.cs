using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using Unmath;
using static Unmath.Math;

public struct BloomData : IRenderResource
{
	private readonly TextureHandle bloom;

	public BloomData(TextureHandle bloom)
	{
		this.bloom = bloom;
	}

	public void SetData(PassBuilder builder)
	{
		builder.AddResource(bloom);
		builder.AddKeyword("BLOOM");
	}
}

public class Bloom
{
	[Serializable]
	public class Settings
	{
		[field: SerializeField, Range(0f, 1f)] public float Strength { get; private set; } = 0.125f;
		[field: SerializeField, Range(1, 12)] public int MaxMips { get; private set; } = 6;
	}

	private readonly RenderGraph renderGraph;
	private readonly Material material;
	private readonly Settings settings;

	public Bloom(RenderGraph renderGraph, Settings settings)
	{
		this.renderGraph = renderGraph;
		this.settings = settings;
		material = new Material(Shader.Find("Hidden/Morrowind Bloom")) { hideFlags = HideFlags.HideAndDontSave };
	}

	public void Render(Camera camera, TextureHandle cameraTarget)
	{
		var mipCount = Min(settings.MaxMips, (int)Log2(Max(camera.pixelWidth, camera.pixelHeight)));
		Span<TextureHandle> bloomIds = stackalloc TextureHandle[mipCount];
		Span<ViewHandle> viewHandles = stackalloc ViewHandle[mipCount];

		for (var i = 0; i < mipCount; i++)
		{
			var width = Max(1, camera.pixelWidth >> (i + 1));
			var height = Max(1, camera.pixelHeight >> (i + 1));

			viewHandles[i] = renderGraph.AddViewInfo(new(width, height));
			bloomIds[i] = renderGraph.GetTexture(new(viewHandles[i], GraphicsFormat.B10G11R11_UFloatPack32), Shader.PropertyToID("Bloom"));

			using var pass = renderGraph.AddRenderPass("Bloom Down");
			pass.ViewHandle = viewHandles[i];
			pass.AddOutput(bloomIds[i]);
			pass.AddResource(i > 0 ? bloomIds[i - 1] : cameraTarget);
			var passIndex = i > 0 ? 1 : 0;
			pass.SetRenderFunction((1.0f / new Float2(width, height), material, passIndex), static (command, data) =>
			{
				command.SetGlobalVector("RcpResolution", (Vector2)data.Item1);
				command.DrawProcedural(default, data.material, data.passIndex, MeshTopology.Triangles, 3);
			});
		}

		for (var i = mipCount - 1; i > 0; i--)
		{
			using var pass = renderGraph.AddRenderPass("Bloom Up");
			pass.ViewHandle = viewHandles[i - 1];
			pass.AddOutput(bloomIds[i - 1]);
			pass.AddResource(bloomIds[i]);

			var width = Max(1, camera.pixelWidth >> i);
			var height = Max(1, camera.pixelHeight >> i);
			pass.SetRenderFunction((1.0f / new Float2(width, height), settings.Strength, material), static (command, data) =>
			{
				command.SetGlobalFloat("Strength", data.Strength);
				command.SetGlobalVector("RcpResolution", (Vector2)data.Item1);
				command.DrawProcedural(default, data.material, 2, MeshTopology.Triangles, 3);
			});
		}

		renderGraph.SetResource(new BloomData(bloomIds[0]));
	}
}
