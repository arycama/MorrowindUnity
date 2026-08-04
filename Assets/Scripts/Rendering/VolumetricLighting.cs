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
		[field: SerializeField] public int TileSize { get; private set; } = 8;
		[field: SerializeField] public int DepthSlices { get; private set; } = 128;
		[field: SerializeField, Range(0.0f, 2.0f)] public float BlurSigma { get; private set; } = 1.0f;
		[field: SerializeField] public float MaxDistance { get; private set; } = 512.0f;
	}

	private readonly Settings settings;
	private readonly PersistentRTHandleCache colorHistory;
	private readonly ComputeShader computeShader;

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

		var linearToVolumetricScale = Rcp(Log2(settings.MaxDistance / viewPassData.near));
		var volumetricLightingData = renderGraph.SetConstantBuffer(
		(
			linearToVolumetricScale,
			-Log2(viewPassData.near) * linearToVolumetricScale,
			(Log2(settings.MaxDistance) - Log2(viewPassData.near)) / settings.DepthSlices,
			Log2(viewPassData.near),
			volumeWidth,
			volumeHeight,
			settings.DepthSlices,
			Rcp(settings.DepthSlices),
			(uint)Log2(settings.TileSize),
			(float)settings.BlurSigma,
			(uint)settings.DepthSlices,
			0f
		));

		var pixelToWorldViewDir = Float4x4.PixelToWorldViewDirectionMatrix(new(volumeWidth, volumeHeight), 0, viewPassData.tanHalfFov, Matrix4x4.Rotate(viewPassData.rotation), true);

		ResourceHandle<RenderTexture> current, history = default;
		bool wasCreated = false;

		using (var pass = renderGraph.AddComputeRenderPass("Volumetric Lighting", (pixelToWorldViewDir, history, wasCreated)))
		{
			(current, history, wasCreated) = colorHistory.GetTextures(new(volumeWidth, volumeHeight), pass.Index, viewPassData.viewId, settings.DepthSlices);
			pass.renderData.history = history;
			pass.renderData.wasCreated = wasCreated;

			pass.Initialize(computeShader, 0, volumeWidth, volumeHeight, settings.DepthSlices);
			pass.WriteTexture("Result", current);

			pass.ReadTexture("Input", history);
			pass.ReadBuffer("VolumetricLightingData", volumetricLightingData);

			pass.ReadResource<ViewData>();
			pass.ReadResource<EnvironmentData>();
			pass.ReadResource<LightingData>();

			if (pass.TryReadResource<PointLightData>())
				pass.AddKeyword("POINT_LIGHTS_ON");

			pass.SetRenderFunction(static (command, pass, data) =>
			{
				pass.SetMatrix("PixelToWorldViewDir", data.pixelToWorldViewDir);
				pass.SetVector("InputScale", pass.RenderGraph.GetScale3D(data.history));
				pass.SetVector("InputMax", pass.RenderGraph.GetLimit3D(data.history));
				pass.SetFloat("IsFirst", data.wasCreated ? 1.0f : 0.0f);
			});
		}

		// Filter X
		var finalInput = current;
		if (settings.BlurSigma > 0)
		{
			var filterX = renderGraph.GetTexture(new(volumeWidth, volumeHeight), GraphicsFormat.R16G16B16A16_SFloat, settings.DepthSlices, TextureDimension.Tex3D);
			using (var pass = renderGraph.AddComputeRenderPass("Filter X"))
			{
				pass.Initialize(computeShader, 1, volumeWidth, volumeHeight, settings.DepthSlices);
				pass.WriteTexture("Result", filterX);
				pass.ReadBuffer("VolumetricLightingData", volumetricLightingData);
				pass.ReadTexture("Input", current);
			}

			// Filter Y
			var filterY = renderGraph.GetTexture(new(volumeWidth, volumeHeight), GraphicsFormat.R16G16B16A16_SFloat, settings.DepthSlices, TextureDimension.Tex3D);
			using (var pass = renderGraph.AddComputeRenderPass("Filter Y"))
			{
				pass.Initialize(computeShader, 2, volumeWidth, volumeHeight, settings.DepthSlices);
				pass.WriteTexture("Result", filterY);
				pass.ReadBuffer("VolumetricLightingData", volumetricLightingData);
				pass.ReadTexture("Input", filterX);
			}

			finalInput = filterY;
		}

		// Accumulate
		var volumetricLight = renderGraph.GetTexture(new(volumeWidth, volumeHeight), GraphicsFormat.R16G16B16A16_SFloat, settings.DepthSlices, TextureDimension.Tex3D);
		using (var pass = renderGraph.AddComputeRenderPass("Accumulate", pixelToWorldViewDir))
		{
			pass.Initialize(computeShader, 3, volumeWidth, volumeHeight, 1);
			pass.WriteTexture("Result", volumetricLight);
			pass.ReadBuffer("VolumetricLightingData", volumetricLightingData);
			pass.ReadTexture("Input", finalInput);

			pass.ReadResource<EnvironmentData>();
			pass.ReadResource<ViewData>();

			pass.SetRenderFunction(static (command, pass, data) =>
			{
				pass.SetMatrix("PixelToWorldViewDir", data);
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
			pass.SetVector("VolumetricLightScale", pass.RenderGraph.GetScale3D(volumetricLighting));
			pass.SetVector("VolumetricLightMax", pass.RenderGraph.GetLimit3D(volumetricLighting));
		}
	}
}
