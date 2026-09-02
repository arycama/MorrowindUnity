using System;
using UnityEngine;

public class LightCulling
{
	[Serializable]
	public class Settings
	{
		[field: SerializeField, Pow2(128)] public int TileSize { get; private set; } = 16;
		[field: SerializeField, Pow2(8192)] public int DepthSlices { get; private set; } = 8192;
		[field: SerializeField] public Mesh PointLightMesh { get; private set; }
		[field: SerializeField] public Mesh SpotLightMesh { get; private set; }
	}

	private readonly RenderGraph renderGraph;
	private readonly Material pointLightMaterial;
	private readonly Settings settings;

	public LightCulling(RenderGraph renderGraph, Settings settings)
	{
		this.renderGraph = renderGraph;
		this.settings = settings;
		pointLightMaterial = new Material(Shader.Find("Hidden/Morrowind Point Light")) { hideFlags = HideFlags.HideAndDontSave };
	}

	public void Render(ViewHandle viewHandle, TextureHandle cameraDepth)
	{
		using (var pass = renderGraph.AddRenderPass("Light Culling"))
		{
			pass.ViewHandle = viewHandle;
			pass.DepthStencil = cameraDepth;

			var pointLightData = renderGraph.GetResource<PointLightData>();

			pass.AddUavOutput(pointLightData.visibleLightBits);
			pass.AddResources<ViewData, PointLightData>();

			pass.SetRenderFunction((pointLightData.visibleLightBits, renderGraph, settings, pointLightMaterial, pointLightData.pointLightCount, pointLightData.intersectingLightCount), static (command, data) =>
			{
				command.SetRandomWriteTarget(0, data.renderGraph.GetTextureResource(data.visibleLightBits));

				if (data.intersectingLightCount > 0)
				{
					command.SetGlobalFloat("IndexOffset", 0);
					command.DrawMeshInstancedProcedural(data.settings.PointLightMesh, 0, data.pointLightMaterial, 0, data.intersectingLightCount);
				}

				var remainingPointLightCount = data.pointLightCount - data.intersectingLightCount;
				if (remainingPointLightCount > 0)
				{
					command.SetGlobalFloat("IndexOffset", data.intersectingLightCount);
					command.DrawMeshInstancedProcedural(data.settings.PointLightMesh, 0, data.pointLightMaterial, 1, remainingPointLightCount);
				}
			});
		}
	}
}