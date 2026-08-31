using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Unmath;
using static Unmath.Math;

public class NewPipeline : RenderPipelineBase
{
	private static readonly IndexedString blueNoise1DIds = new("STBN/stbn_vec1_2Dx1D_128x128x64_", 64);
	private static readonly IndexedString blueNoise2DIds = new("STBN/stbn_vec2_2Dx1D_128x128x64_", 64);

	private static readonly int
		viewDataId = Shader.PropertyToID("ViewData"),
		environmentDataId = Shader.PropertyToID("EnvironmentData");

	protected override SupportedRenderingFeatures SupportedRenderingFeatures => asset.SupportedRenderingFeatures;
	private readonly NewPipelineAsset asset;
	private readonly Material blitMaterial, deferredMaterial;
	private readonly MaterialPropertyBlock propertyBlock;
	private readonly SetupView setupView;
	private readonly SetupLighting setupLighting;
	private readonly ComputeShader volumetricLightShader;
	private readonly RayTracingAccelerationStructure rtas;
	private readonly RayTracingShader occlusionRaytracingShader, shadowRaytracingShader, diffuseRaytracingShader, depthOfFieldRaytracingShader;
	private readonly Dictionary<Camera, RenderTexture> volumetricHistory = new();

	public NewPipeline(NewPipelineAsset asset)
	{
		this.asset = asset;
		blitMaterial = new Material(Shader.Find("Hidden/Blit Material")) { hideFlags = HideFlags.HideAndDontSave };
		deferredMaterial = new Material(Shader.Find("Hidden/Morrowind Deferred")) { hideFlags = HideFlags.HideAndDontSave };
		propertyBlock = new();
		setupView = new(renderGraph);
		setupLighting = new(renderGraph, asset.Lighting);

		volumetricLightShader = Resources.Load<ComputeShader>("VolumetricLight");

		occlusionRaytracingShader = Resources.Load<RayTracingShader>("Raytracing/MorrowindOcclusion");
		shadowRaytracingShader = Resources.Load<RayTracingShader>("Raytracing/MorrowindShadow");
		diffuseRaytracingShader = Resources.Load<RayTracingShader>("Raytracing/MorrowindDiffuse");
		depthOfFieldRaytracingShader = Resources.Load<RayTracingShader>("Raytracing/MorrowindDepthOfField");

		var rasSettings = new RayTracingAccelerationStructure.Settings(RayTracingAccelerationStructure.ManagementMode.Automatic, RayTracingAccelerationStructure.RayTracingModeMask.Everything, asset.RayTracingLayerMask);
		rtas = new RayTracingAccelerationStructure(rasSettings);
	}

	protected override void Dispose(bool disposing)
	{
		base.Dispose(disposing);
		setupLighting.Dispose();
		rtas.Release();
	}

	protected override void RenderFrame(ScriptableRenderContext context, List<Camera> cameras)
	{
		using (var pass = renderGraph.AddRenderPass("Raytracing Update"))
		{
			pass.SetRenderFunction(rtas, static (command, data) =>
			{
				command.BuildRayTracingAccelerationStructure(data);
			});
		}
	}

	protected override void RenderCamera(Camera camera, ScriptableCullingParameters cullingParameters, ScriptableRenderContext context)
	{
		cullingParameters.cullingOptions = CullingOptions.DisablePerObjectCulling | CullingOptions.NeedsLighting | CullingOptions.ShadowCasters;
		cullingParameters.shadowDistance = asset.Lighting.DirectionalShadowDistance;
		var cullingResults = context.Cull(ref cullingParameters);

		var viewData = setupView.Render(camera);
		var (environmentData, sunShadow) = setupLighting.Render(camera, cullingResults, context, viewData);
		var viewSize = new Int2(camera.pixelWidth, camera.pixelHeight);
		var tanHalfFovY = Geometry.TanHalfFovDegrees(camera.fieldOfView);
		var tanHalfFov = new Float2(tanHalfFovY * camera.aspect, tanHalfFovY);

		var noiseIndex = renderGraph.FrameIndex % 64;
		var blueNoise1D = Resources.Load<Texture2D>(blueNoise1DIds[noiseIndex]);
		var blueNoise2D = Resources.Load<Texture2D>(blueNoise2DIds[noiseIndex]);

		var volumeWidth = DivRoundUp(viewSize.x, asset.VolumetricTileSize);
		var volumeHeight = DivRoundUp(viewSize.y, asset.VolumetricTileSize);
		var volumetricViewHandle = renderGraph.AddViewInfo(new(volumeWidth, volumeHeight), 1, asset.VolumetricSlices);
		var volumetricLight = renderGraph.GetTexture(new(volumetricViewHandle, GraphicsFormat.R16G16B16A16_SFloat, dimension: TextureDimension.Tex3D), Shader.PropertyToID("VolumetricLight"));

		if (asset.VolumetricsEnabled)
		{
			var volumetricDescriptor = new RenderTargetDescriptor(volumetricViewHandle, GraphicsFormat.R16G16B16A16_SFloat, dimension: TextureDimension.Tex3D);
			var volumetricLightTemp = renderGraph.GetTexture(volumetricDescriptor, Shader.PropertyToID("VolumetricLightTemp"));

			using (var pass = renderGraph.AddRenderPass("Volumetric Light Compute"))
			{
				pass.ViewHandle = volumetricViewHandle;
				var pixelToViewDir = Float4x4.PixelToNearClip(new(volumeWidth, volumeHeight), 0f, tanHalfFov, true, false);
				pass.AddUavOutput(volumetricLightTemp);

				if (renderGraph.IsResourceWritten(sunShadow))
				{
					pass.AddResource(sunShadow);
					pass.AddKeyword("SHADOWS_ON");
				}

				var hasHistory = volumetricHistory.TryGetValue(camera, out var history);
				if (hasHistory)
					pass.AddKeyword("HISTORY");

				var viewInfo = renderGraph.GetViewInfo(volumetricViewHandle);
				var target = RenderTexture.GetTemporary(volumetricDescriptor.GetRenderTextureDescriptor(viewInfo, 1, true));
				target.Create();
				volumetricHistory[camera] = target;

				var volumeSize = new Int3(volumeWidth, volumeHeight, asset.VolumetricSlices);
				pass.SetRenderFunction((pixelToViewDir, volumetricLightShader, volumeSize, asset.VolumetricDistance, viewData, environmentData, blueNoise1D, history), static (command, data) =>
				{
					command.SetGlobalConstantBuffer(data.environmentData, environmentDataId, 0, data.environmentData.stride);
					command.SetGlobalConstantBuffer(data.viewData, viewDataId, 0, data.viewData.stride);
					command.SetComputeVectorParam(data.volumetricLightShader, "VolumeSize", new Float3(data.volumeSize.x, data.volumeSize.y, data.volumeSize.z));
					command.SetComputeFloatParam(data.volumetricLightShader, "MaxDepth", data.VolumetricDistance);
					command.SetComputeTextureParam(data.volumetricLightShader, 0, "BlueNoise1D", data.blueNoise1D);
					command.SetComputeTextureParam(data.volumetricLightShader, 0, "VolumetricLight", data.history);
					command.SetComputeMatrixParam(data.volumetricLightShader, "PixelToViewDir", data.pixelToViewDir);
					command.DispatchCompute(data.volumetricLightShader, 0, data.volumeSize.x, data.volumeSize.y, data.volumeSize.z);

					if (data.history != null)
						RenderTexture.ReleaseTemporary(data.history);
				});

				renderGraph.ExportResource(volumetricLightTemp, target);
			}

			using (var pass = renderGraph.AddRenderPass("Volumetric Light Compute"))
			{
				pass.ViewHandle = volumetricViewHandle;
				var pixelToViewDir = Float4x4.PixelToNearClip(new(volumeWidth, volumeHeight), 0f, tanHalfFov, true, false);
				pass.AddResource(volumetricLightTemp);
				pass.AddUavOutput(volumetricLight);

				var volumeSize = new Int3(volumeWidth, volumeHeight, asset.VolumetricSlices);

				pass.SetRenderFunction((pixelToViewDir, volumetricLightShader, volumeSize, asset.VolumetricDistance, viewData, environmentData), static (command, data) =>
				{
					command.SetGlobalConstantBuffer(data.environmentData, environmentDataId, 0, data.environmentData.stride);
					command.SetGlobalConstantBuffer(data.viewData, viewDataId, 0, data.viewData.stride);
					command.SetComputeVectorParam(data.volumetricLightShader, "VolumeSize", new Float3(data.volumeSize.x, data.volumeSize.y, data.volumeSize.z));
					command.SetComputeFloatParam(data.volumetricLightShader, "MaxDepth", data.VolumetricDistance);
					command.SetComputeMatrixParam(data.volumetricLightShader, "PixelToViewDir", data.pixelToViewDir);
					command.DispatchCompute(data.volumetricLightShader, 1, data.volumeSize.x, data.volumeSize.y, 1);
				});
			}
		}

		var viewHandle = renderGraph.AddViewInfo(viewSize, asset.Samples);
		var cameraDepth = renderGraph.GetTexture(new(viewHandle, GraphicsFormat.D32_SFloat_S8_UInt, true), Shader.PropertyToID("CameraDepth"));
		var albedoNormal = renderGraph.GetTexture(new(viewHandle, GraphicsFormat.R8G8B8A8_UNorm), Shader.PropertyToID("AlbedoNormal"));
		var cameraColor = renderGraph.GetTexture(new(viewHandle, GraphicsFormat.B10G11R11_UFloatPack32, true, RenderSettings.fogColor.linear), Shader.PropertyToID("CameraColor"));

		using (var pass = renderGraph.AddRenderPass("Terrain"))
		{
			pass.ViewHandle = viewHandle;
			pass.DepthStencil = cameraDepth;
			pass.AddOutputs(stackalloc[] { albedoNormal, cameraColor });

			var rendererList = context.CreateRendererList(new(new ShaderTagId("Terrain"), cullingResults, camera) { renderQueueRange = RenderQueueRange.all, sortingCriteria = SortingCriteria.QuantizedFrontToBack });
			pass.SetRenderFunction((rendererList, viewData, environmentData), static (command, data) =>
			{
				command.SetGlobalConstantBuffer(data.environmentData, environmentDataId, 0, data.environmentData.stride);
				command.SetGlobalConstantBuffer(data.viewData, viewDataId, 0, data.viewData.stride);
				command.DrawRendererList(data.rendererList);
			});
		}

		using (var pass = renderGraph.AddRenderPass("GBuffer"))
		{
			pass.ViewHandle = viewHandle;
			pass.DepthStencil = cameraDepth;
			pass.AddOutputs(stackalloc[] { albedoNormal, cameraColor });

			var rendererParams = new RendererListParams(cullingResults, new(new("GBuffer"), new(camera) { criteria = SortingCriteria.OptimizeStateChanges }) { enableInstancing = true }, new(RenderQueueRange.opaque));
			var rendererList = context.CreateRendererList(ref rendererParams);
			pass.SetRenderFunction((rendererList, viewData, environmentData), static (command, data) =>
			{
				command.SetGlobalConstantBuffer(data.environmentData, environmentDataId, 0, data.environmentData.stride);
				command.SetGlobalConstantBuffer(data.viewData, viewDataId, 0, data.viewData.stride);
				command.DrawRendererList(data.rendererList);
			});
		}

		var raytracedOcclusion = renderGraph.GetTexture(new(viewHandle, GraphicsFormat.R8_UNorm), Shader.PropertyToID("ScreenSpaceOcclusion"));
		if (asset.RaytracedOcclusion)
		{
			using var pass = renderGraph.AddRenderPass("Raytraced Occlusion");
			pass.ViewHandle = viewHandle;
			pass.AddUavOutput(raytracedOcclusion);
			pass.AddResources(stackalloc[] { cameraDepth, albedoNormal });

			pass.SetRenderFunction((rtas, occlusionRaytracingShader, camera.pixelWidth, camera.pixelHeight, blueNoise2D), static (command, data) =>
			{
				command.SetRayTracingTextureParam(data.occlusionRaytracingShader, "BlueNoise2D", data.blueNoise2D);
				command.SetRayTracingShaderPass(data.occlusionRaytracingShader, "RaytracedTransmittance");
				command.SetGlobalRayTracingAccelerationStructure("SceneRaytracingAccelerationStructure", data.rtas);
				command.DispatchRays(data.occlusionRaytracingShader, "RayGeneration", (uint)data.pixelWidth, (uint)data.pixelHeight, 1);
			});
		}

		var raytracedShadows = renderGraph.GetTexture(new(viewHandle, GraphicsFormat.R8_UNorm), Shader.PropertyToID("ScreenSpaceShadows"));
		if (asset.RaytracedShadows)
		{
			using var pass = renderGraph.AddRenderPass("Raytraced Shadow");
			pass.ViewHandle = viewHandle;
			pass.AddUavOutput(raytracedShadows);
			pass.AddResources(stackalloc[] { cameraDepth, albedoNormal });

			pass.SetRenderFunction((rtas, shadowRaytracingShader, camera.pixelWidth, camera.pixelHeight, blueNoise2D), static (command, data) =>
			{
				command.SetRayTracingTextureParam(data.shadowRaytracingShader, "BlueNoise2D", data.blueNoise2D);
				command.SetRayTracingShaderPass(data.shadowRaytracingShader, "RaytracedTransmittance");
				command.SetGlobalRayTracingAccelerationStructure("SceneRaytracingAccelerationStructure", data.rtas);
				command.DispatchRays(data.shadowRaytracingShader, "RayGeneration", (uint)data.pixelWidth, (uint)data.pixelHeight, 1);
			});
		}

		var raytracedDiffuse = renderGraph.GetTexture(new(viewHandle, GraphicsFormat.B10G11R11_UFloatPack32), Shader.PropertyToID("ScreenSpaceDiffuse"));
		if (asset.RaytracedDiffuse)
		{
			using var pass = renderGraph.AddRenderPass("Raytraced Diffuse");
			pass.ViewHandle = viewHandle;
			pass.AddUavOutput(raytracedDiffuse);
			pass.AddResources(stackalloc[] { cameraDepth, albedoNormal });

			pass.SetRenderFunction((rtas, diffuseRaytracingShader, camera.pixelWidth, camera.pixelHeight, blueNoise2D), static (command, data) =>
			{
				command.SetRayTracingTextureParam(data.diffuseRaytracingShader, "BlueNoise2D", data.blueNoise2D);
				command.SetRayTracingShaderPass(data.diffuseRaytracingShader, "RaytracedLuminance");
				command.SetGlobalRayTracingAccelerationStructure("SceneRaytracingAccelerationStructure", data.rtas);
				command.DispatchRays(data.diffuseRaytracingShader, "RayGeneration", (uint)data.pixelWidth, (uint)data.pixelHeight, 1);
			});
		}

		using (var pass = renderGraph.AddRenderPass("Deferred"))
		{
			pass.ViewHandle = viewHandle;
			pass.DepthStencil = cameraDepth;
			pass.AddOutput(cameraColor);
			pass.AddInputs(stackalloc[] { cameraDepth, albedoNormal });

			if (renderGraph.IsResourceWritten(sunShadow))
			{
				pass.AddResource(sunShadow);
				pass.AddKeyword("SHADOWS_ON");
			}

			if (asset.Samples > 1)
				pass.AddKeyword("MSAA_ON");

			if (renderGraph.IsResourceWritten(raytracedOcclusion))
			{
				pass.AddResource(raytracedOcclusion);
				pass.AddKeyword("RAYTRACED_OCCLUSION");
			}

			if (renderGraph.IsResourceWritten(raytracedShadows))
			{
				pass.AddResource(raytracedShadows);
				pass.AddKeyword("RAYTRACED_SHADOWS");
			}

			if (renderGraph.IsResourceWritten(raytracedDiffuse))
			{
				pass.AddResource(raytracedDiffuse);
				pass.AddKeyword("RAYTRACED_DIFFUSE");
			}

			if (renderGraph.IsResourceWritten(volumetricLight))
			{
				pass.AddResource(volumetricLight);
				pass.AddKeyword("VOLUMETRIC_LIGHT_ON");
			}

			pass.SetRenderFunction((deferredMaterial, viewData, environmentData, propertyBlock, asset.VolumetricDistance), static (command, data) =>
			{
				data.propertyBlock.Clear();
				data.propertyBlock.SetConstantBuffer(environmentDataId, data.environmentData, 0, data.environmentData.stride);
				data.propertyBlock.SetConstantBuffer(viewDataId, data.viewData, 0, data.viewData.stride);
				data.propertyBlock.SetFloat("MaxDepth", data.VolumetricDistance);
				command.DrawProcedural(default, data.deferredMaterial, 0, MeshTopology.Triangles, 3, 1, data.propertyBlock);
			});
		}

		using (var pass = renderGraph.AddRenderPass("Sky"))
		{
			pass.ViewHandle = viewHandle;
			pass.DepthStencil = cameraDepth;
			pass.AddOutput(cameraColor);

			var rendererList = context.CreateRendererList(new(new ShaderTagId("Sky"), cullingResults, camera) { renderQueueRange = RenderQueueRange.all });
			pass.SetRenderFunction((rendererList, viewData, environmentData), static (command, data) =>
			{
				command.SetGlobalConstantBuffer(data.environmentData, environmentDataId, 0, data.environmentData.stride);
				command.SetGlobalConstantBuffer(data.viewData, viewDataId, 0, data.viewData.stride);
				command.DrawRendererList(data.rendererList);
			});
		}

		using (var pass = renderGraph.AddRenderPass("Forward Transparent"))
		{
			pass.ViewHandle = viewHandle;
			pass.DepthStencil = cameraDepth;
			pass.AddOutput(cameraColor);

			if (renderGraph.IsResourceWritten(sunShadow))
			{
				pass.AddResource(sunShadow);
				pass.AddKeyword("SHADOWS_ON");
			}

			if (renderGraph.IsResourceWritten(volumetricLight))
			{
				pass.AddResource(volumetricLight);
				pass.AddKeyword("VOLUMETRIC_LIGHT_ON");
			}

			var rendererParams = new RendererListParams(cullingResults, new(new("Forward"), new(camera) { criteria = SortingCriteria.BackToFront | SortingCriteria.OptimizeStateChanges }) { enableInstancing = true }, new(RenderQueueRange.transparent));
			var rendererList = context.CreateRendererList(ref rendererParams);
			pass.SetRenderFunction((rendererList, viewData, environmentData, asset.VolumetricDistance), (command, data) =>
			{
				command.SetGlobalFloat("MaxDepth", data.VolumetricDistance);
				command.SetGlobalConstantBuffer(data.environmentData, environmentDataId, 0, data.environmentData.stride);
				command.SetGlobalConstantBuffer(data.viewData, viewDataId, 0, data.viewData.stride);
				command.DrawRendererList(data.rendererList);
			});
		}

		var raytracedDepthOfField = renderGraph.GetTexture(new(viewHandle, GraphicsFormat.B10G11R11_UFloatPack32), Shader.PropertyToID("DepthOfField"));
		if (asset.RaytracedDepthOfField)
		{
			using var pass = renderGraph.AddRenderPass("Raytraced Depth of Field");
			pass.ViewHandle = viewHandle;
			pass.AddUavOutput(raytracedDepthOfField);

			var focalLength = 0.5f * (asset.SensorSize / 1000.0f) / tanHalfFovY;
			var apertureRadius = 0.5f * focalLength / asset.Aperture;
			var pixelToViewDir = Float4x4.PixelToNearClip(new(camera.pixelWidth, camera.pixelHeight), 0f, tanHalfFov, true, false);

			pass.SetRenderFunction((rtas, depthOfFieldRaytracingShader, camera.pixelWidth, camera.pixelHeight, blueNoise1D, blueNoise2D, apertureRadius, asset.FocusDistance, pixelToViewDir), static (command, data) =>
			{
				command.SetRayTracingTextureParam(data.depthOfFieldRaytracingShader, "BlueNoise1D", data.blueNoise1D);
				command.SetRayTracingTextureParam(data.depthOfFieldRaytracingShader, "BlueNoise2D", data.blueNoise2D);
				command.SetRayTracingFloatParam(data.depthOfFieldRaytracingShader, "ApertureRadius", data.apertureRadius);
				command.SetRayTracingFloatParam(data.depthOfFieldRaytracingShader, "FocusDistance", data.FocusDistance);
				command.SetRayTracingMatrixParam(data.depthOfFieldRaytracingShader, "PixelToViewDir", data.pixelToViewDir);
				command.SetRayTracingShaderPass(data.depthOfFieldRaytracingShader, "RaytracedLuminance");
				command.SetGlobalRayTracingAccelerationStructure("SceneRaytracingAccelerationStructure", data.rtas);
				command.DispatchRays(data.depthOfFieldRaytracingShader, "RayGeneration", (uint)data.pixelWidth, (uint)data.pixelHeight, 1);
			});
		}

		using (var pass = renderGraph.AddRenderPass("Final Blit"))
		{
			// Can only render directly to backbuffer if there is no msaa samples and there is no target texture
			// TODO: Check for hardware msaa backbuffer resolve support
			pass.ViewHandle = renderGraph.AddViewInfo(new(camera.pixelWidth, camera.pixelHeight));

			// Final blit/resolve if needed
			// TODO: This should also account for HDR
			var backbufferFormat = QualitySettings.activeColorSpace == ColorSpace.Linear ? GraphicsFormat.R8G8B8A8_SRGB : GraphicsFormat.R8G8B8A8_UNorm;
			var targetFormat = camera.targetTexture == null ? backbufferFormat : camera.targetTexture.graphicsFormat;
			var sceneColor = renderGraph.GetTexture(new(viewHandle, targetFormat), Shader.PropertyToID("SceneColor"));
			renderGraph.ExportResource(sceneColor, camera.targetTexture == null ? BuiltinRenderTextureType.CameraTarget : camera.targetTexture);

			// For sceneView, take the first depth sample for for gizmos, wireframe, etc.
			TextureHandle sceneDepth = default;
			var requiresSceneDepth = false;

#if UNITY_EDITOR
			requiresSceneDepth = camera.cameraType == CameraType.SceneView || Handles.ShouldRenderGizmos();
			if (requiresSceneDepth)
			{
				sceneDepth = renderGraph.GetTexture(new(viewHandle, GraphicsFormat.D32_SFloat_S8_UInt), Shader.PropertyToID("SceneDepth"));
				renderGraph.ExportResource(sceneDepth, camera.targetTexture);
			}
#endif

			var renderToBackbuffer = asset.Samples == 1 && camera.targetTexture == null;
			if (renderToBackbuffer)
			{
				pass.AddInput(cameraColor);
				pass.AddKeyword("DIRECT");
			}
			else
			{
				pass.AddResource(cameraColor);

				if (requiresSceneDepth)
					pass.AddResource(cameraDepth);
			}

			// TODO: Currently we need to set depth as the first output if it exists. Once this is replaced with a set depth stencil function, this wont be neccessary
			if (requiresSceneDepth)
			{
				pass.DepthStencil = sceneDepth;
				pass.AddOutput(sceneColor);
				pass.AddKeyword("DEPTH");
			}
			else
				pass.AddOutputs(stackalloc[] { sceneColor });

			var requiresFlip = camera.targetTexture == null;
			if (requiresFlip)
				pass.AddKeyword("FLIP");

			if (asset.Samples > 1)
				pass.AddKeyword("MSAA");

			if (renderGraph.IsResourceWritten(raytracedDepthOfField))
			{
				pass.AddResource(raytracedDepthOfField);
				pass.AddKeyword("DEPTH_OF_FIELD");
			}

			pass.SetRenderFunction((blitMaterial, viewData), static (command, data) =>
			{
				command.SetGlobalConstantBuffer(data.viewData, viewDataId, 0, data.viewData.stride);
				command.SetWireframe(false);
				command.DrawProcedural(Matrix4x4.identity, data.blitMaterial, 0, MeshTopology.Triangles, 3);
			});
		}

#if UNITY_EDITOR
		// Render gizmos
		if (Handles.ShouldRenderGizmos())
		{
			using var pass = renderGraph.AddRenderPass("Render Gizmos");
			var preImageEffectsRenderList = context.CreateGizmoRendererList(camera, GizmoSubset.PreImageEffects);
			var postImageEffectsRenderList = context.CreateGizmoRendererList(camera, GizmoSubset.PostImageEffects);

			pass.SetRenderFunction((preImageEffectsRenderList, postImageEffectsRenderList), static (command, data) =>
			{
				// Note that gizmos use their own matrix logic which we can't override
				command.DrawRendererList(data.preImageEffectsRenderList);
				command.DrawRendererList(data.postImageEffectsRenderList);
			});
		}

		// Render wireframe
		if (camera.cameraType == CameraType.SceneView)
		{
			viewData = setupView.Render(camera, true);

			using var pass = renderGraph.AddRenderPass("Wireframe");
			var wireframeRendererList = context.CreateWireOverlayRendererList(camera);
			pass.SetRenderFunction((camera, wireframeRendererList, context, viewData), static (command, data) =>
			{
				data.context.SetupCameraProperties(data.camera);
				command.SetGlobalConstantBuffer(data.viewData, viewDataId, 0, data.viewData.stride);
				command.DrawRendererList(data.wireframeRendererList);
			});
		}
#endif
	}
}