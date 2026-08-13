using System;
using CustomRenderPipeline;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Unmath;
using static Unmath.Math;

public class VolumetricLighting : ViewRenderFeature
{
	[Serializable]
	public class Settings
	{
		[field: SerializeField] public bool Enabled { get; private set; } = true;
		[field: SerializeField] public int TileSize { get; private set; } = 8;
		[field: SerializeField] public int DepthSlices { get; private set; } = 128;
		[field: SerializeField, Range(0.0f, 2.0f)] public float BlurSigma { get; private set; } = 1.0f;
		[field: SerializeField] public float MaxDistance { get; private set; } = 512.0f;
	}

	private readonly Settings settings;
	private readonly ComputeShader computeShader;
	private readonly PersistentRTHandleCache colorHistory;

	public VolumetricLighting(Settings settings, RenderGraph renderGraph) : base(renderGraph)
	{
		this.settings = settings;
		colorHistory = new(GraphicsFormat.R16G16B16A16_SFloat, renderGraph, "Volumetric Lighting", TextureDimension.Tex3D);
		computeShader = Resources.Load<ComputeShader>("VolumetricLight");
	}

	protected override void Cleanup(bool disposing)
	{
		colorHistory.Dispose();
	}

	public override void Render(in ReadOnlySpan<ViewParameter> viewParameters, in ViewPassData viewPassData, in DisplayData displayOutputData, ScriptableRenderContext context)
	{
		renderGraph.AddProfileBeginPass("Volumetric Lighting");

		var volumeWidth = DivRoundUp(viewPassData.viewSize.x, settings.TileSize);
		var volumeHeight = DivRoundUp(viewPassData.viewSize.y, settings.TileSize);
		var volumeDepth = settings.DepthSlices;
		var pixelToViewDir = Float4x4.PixelToNearClip(new(volumeWidth, volumeHeight), 0f, viewPassData.tanHalfFov, true, false);

		var volumetricLightingData = renderGraph.SetConstantBuffer
		((
			new Float3(volumeWidth, volumeHeight, volumeDepth),
			settings.MaxDistance
		));

		ResourceHandle<RenderTexture> current, history = default;
		bool wasCreated = false;

		using (var pass = renderGraph.AddComputeRenderPass("Volumetric Lighting"))
		{
			(current, history, wasCreated) = colorHistory.GetTextures(new(volumeWidth, volumeHeight), pass.Index, viewPassData.viewId, settings.DepthSlices);

			pass.Initialize(computeShader, 0, volumeWidth, volumeHeight, settings.DepthSlices);
			pass.WriteTexture("Result", current);

			pass.ReadResource<ViewData>();
			pass.ReadResource<EnvironmentData>();
			pass.ReadResource<LightingData>();
			pass.ReadBuffer("VolumetricLightingData", volumetricLightingData);
			pass.ReadTexture("Input", history);

			if (pass.TryReadResource<PointLightData>())
				pass.AddKeyword("POINT_LIGHTS_ON");

			pass.SetRenderFunction((command, pass) =>
			{
				pass.SetMatrix("PixelToViewDir", pixelToViewDir);
				pass.SetVector("VolumeSize", new Float3(volumeWidth, volumeHeight, volumeDepth));
				pass.SetFloat("MaxDepth", settings.MaxDistance);
			});
		}

		// Accumulate
		var volumetricLight = renderGraph.GetTexture(new(volumeWidth, volumeHeight), GraphicsFormat.R16G16B16A16_SFloat, settings.DepthSlices, TextureDimension.Tex3D, isExactSize: true);
		using (var pass = renderGraph.AddComputeRenderPass("Accumulate", pixelToViewDir))
		{
			pass.Initialize(computeShader, 1, volumeWidth, volumeHeight, 1);
			pass.WriteTexture("Result", volumetricLight);
			pass.ReadTexture("Input", current);

			pass.ReadResource<EnvironmentData>();
			pass.ReadResource<ViewData>();
			pass.ReadBuffer("VolumetricLightingData", volumetricLightingData);

			pass.SetRenderFunction((command, pass, data) =>
			{
				pass.SetMatrix("PixelToViewDir", data);
				pass.SetFloat("MaxDepth", settings.MaxDistance);
				pass.SetVector("VolumeSize", new Float3(volumeWidth, volumeHeight, volumeDepth));
			});
		}

		var result = new Result(volumetricLight, volumetricLightingData);
		renderGraph.SetResource(result);

		renderGraph.AddProfileEndPass("Volumetric Lighting");
	}

	public readonly struct Result : IRenderPassData
	{
		private readonly ResourceHandle<RenderTexture> volumetricLighting;
		private readonly ResourceHandle<GraphicsBuffer> volumetricLightingData;

		public Result(ResourceHandle<RenderTexture> volumetricLighting, ResourceHandle<GraphicsBuffer> volumetricLightingData)
		{
			this.volumetricLighting = volumetricLighting;
			this.volumetricLightingData = volumetricLightingData;
		}

		public readonly void SetInputs(RenderPass pass)
		{
			pass.ReadTexture("VolumetricLighting", volumetricLighting);
			pass.ReadBuffer("VolumetricLightingData", volumetricLightingData);
		}

		public readonly void SetProperties(RenderPass pass, CommandBuffer command)
		{
		}
	}
}
