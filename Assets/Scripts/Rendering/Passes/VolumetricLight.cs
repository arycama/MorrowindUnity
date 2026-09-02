using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Unmath;
using static Unmath.Math;

public class VolumetricLight : IDisposable
{
	private readonly RenderGraph renderGraph;
	private readonly NewPipelineAsset asset;
	private readonly ComputeShader volumetricLightShader;
	private readonly Dictionary<Camera, RenderTexture> volumetricHistory = new();

	public VolumetricLight(RenderGraph renderGraph, NewPipelineAsset asset)
	{
		this.renderGraph = renderGraph;
		this.asset = asset;
		volumetricLightShader = Resources.Load<ComputeShader>("VolumetricLight");
	}

	public void Dispose()
	{
		foreach (var history in volumetricHistory)
			RenderTexture.ReleaseTemporary(history.Value);
	}

	public void Render(Camera camera, Texture blueNoise1D, BufferHandle pointLightData, BufferHandle pointLights, BufferHandle lightDepthMinMaxBuffer, TextureHandle visibleLightBits, TextureHandle pointShadows)
	{
		var viewSize = new Int2(camera.pixelWidth, camera.pixelHeight);
		var tanHalfFovY = Geometry.TanHalfFovDegrees(camera.fieldOfView);
		var tanHalfFov = new Float2(tanHalfFovY * camera.aspect, tanHalfFovY);

		var volumeWidth = DivRoundUp(viewSize.x, asset.VolumetricTileSize);
		var volumeHeight = DivRoundUp(viewSize.y, asset.VolumetricTileSize);
		var volumetricViewHandle = renderGraph.AddViewInfo(new(volumeWidth, volumeHeight), 1, asset.VolumetricSlices);
		var volumetricLight = renderGraph.GetTexture(new(volumetricViewHandle, GraphicsFormat.R16G16B16A16_SFloat, dimension: TextureDimension.Tex3D), Shader.PropertyToID("VolumetricLight"));

		var dataBuffer = renderGraph.GetBuffer(new(1, 16, GraphicsBuffer.Target.Constant), Shader.PropertyToID("VolumetricLightData"));
		using (var pass = renderGraph.AddRenderPass("Volumetric Light Set Data"))
		{
			var data = stackalloc[] { asset.VolumetricDistance, 0f, 0f, 0f }.ToNativeArray();
			pass.AddUavOutput(dataBuffer);
			pass.SetRenderFunction((dataBuffer, data, renderGraph), static (command, data) =>
			{
				var buffer = data.renderGraph.GetBufferResource(data.dataBuffer);
				command.SetBufferData(buffer, data.data);
			});
		}

		if (asset.VolumetricsEnabled)
		{
			var volumetricDescriptor = new RenderTargetDescriptor(volumetricViewHandle, GraphicsFormat.R16G16B16A16_SFloat, dimension: TextureDimension.Tex3D);
			var volumetricLightTemp = renderGraph.GetTexture(volumetricDescriptor, Shader.PropertyToID("VolumetricLightTemp"));

			using (var pass = renderGraph.AddRenderPass("Volumetric Light Compute"))
			{
				pass.ViewHandle = volumetricViewHandle;
				var pixelToViewDir = Float4x4.PixelToNearClip(new(volumeWidth, volumeHeight), 0f, tanHalfFov, true, false);
				pass.AddUavOutput(volumetricLightTemp);
				pass.AddResources(stackalloc ResourceHandle[] { pointLightData, pointLights, lightDepthMinMaxBuffer, visibleLightBits, pointShadows, dataBuffer });
				pass.AddResources<EnvironmentData, ViewData>();

				var hasHistory = volumetricHistory.TryGetValue(camera, out var history);
				if (hasHistory)
					pass.AddKeyword("HISTORY");

				if (renderGraph.IsResourceWritten(visibleLightBits))
				{
					pass.AddKeyword("POINT_LIGHTS_ON");
				}

				var viewInfo = renderGraph.GetViewInfo(volumetricViewHandle);
				var target = RenderTexture.GetTemporary(volumetricDescriptor.GetRenderTextureDescriptor(viewInfo, 1, true));
				_ = target.Create();
				volumetricHistory[camera] = target;

				var volumeSize = new Int3(volumeWidth, volumeHeight, asset.VolumetricSlices);
				pass.SetRenderFunction((pixelToViewDir, volumetricLightShader, volumeSize, blueNoise1D, history), static (command, data) =>
				{
					command.SetComputeVectorParam(data.volumetricLightShader, "VolumeSize", new Float3(data.volumeSize.x, data.volumeSize.y, data.volumeSize.z));
					command.SetComputeTextureParam(data.volumetricLightShader, 0, "BlueNoise1D", data.blueNoise1D);
					command.SetComputeTextureParam(data.volumetricLightShader, 0, "VolumetricLight", data.history);
					command.SetComputeMatrixParam(data.volumetricLightShader, "PixelToViewDir", data.pixelToViewDir);
					command.DispatchCompute(data.volumetricLightShader, 0, DivRoundUp(data.volumeSize.x, 8), DivRoundUp(data.volumeSize.y, 8), data.volumeSize.z);

					if (data.history != null)
						RenderTexture.ReleaseTemporary(data.history);
				});

				renderGraph.ExportTexture(volumetricLightTemp, target);
			}

			using (var pass = renderGraph.AddRenderPass("Volumetric Light Accumulate"))
			{
				pass.ViewHandle = volumetricViewHandle;
				var pixelToViewDir = Float4x4.PixelToNearClip(new(volumeWidth, volumeHeight), 0f, tanHalfFov, true, false);
				pass.AddResources(stackalloc ResourceHandle[] { volumetricLightTemp, dataBuffer });
				pass.AddResources<EnvironmentData, ViewData>();
				pass.AddUavOutput(volumetricLight);

				var volumeSize = new Int3(volumeWidth, volumeHeight, asset.VolumetricSlices);
				pass.SetRenderFunction((pixelToViewDir, volumetricLightShader, volumeSize), static (command, data) =>
				{
					command.SetComputeVectorParam(data.volumetricLightShader, "VolumeSize", new Float3(data.volumeSize.x, data.volumeSize.y, data.volumeSize.z));
					command.SetComputeMatrixParam(data.volumetricLightShader, "PixelToViewDir", data.pixelToViewDir);
					command.DispatchCompute(data.volumetricLightShader, 1, DivRoundUp(data.volumeSize.x, 8), DivRoundUp(data.volumeSize.y, 8), 1);
				});
			}
		}

		renderGraph.SetResource(new VolumetricLightData(volumetricLight, dataBuffer));
	}
}
