using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Unmath;

public class NewPipeline : RenderPipelineBase
{
	private static readonly IndexedString blueNoise1DIds = new("STBN/stbn_vec1_2Dx1D_128x128x64_", 64);
	private static readonly IndexedString blueNoise2DIds = new("STBN/stbn_vec2_2Dx1D_128x128x64_", 64);

	protected override SupportedRenderingFeatures SupportedRenderingFeatures => asset.SupportedRenderingFeatures;
	private readonly NewPipelineAsset asset;
	private readonly Material blitMaterial, deferredMaterial, backgroundMaterial;
	private readonly SetupView setupView;
	private readonly SetupLighting setupLighting;
	private readonly VolumetricLight volumetricLight;
	private readonly LightCulling lightCulling;
	private readonly RayTracingAccelerationStructure rtas;
	private readonly RayTracingShader occlusionRaytracingShader, shadowRaytracingShader, diffuseRaytracingShader, depthOfFieldRaytracingShader;

	public NewPipeline(NewPipelineAsset asset)
	{
		this.asset = asset;
		blitMaterial = new Material(Shader.Find("Hidden/Blit Material")) { hideFlags = HideFlags.HideAndDontSave };
		deferredMaterial = new Material(Shader.Find("Hidden/Morrowind Deferred")) { hideFlags = HideFlags.HideAndDontSave };
		backgroundMaterial = new Material(Shader.Find("Hidden/Background")) { hideFlags = HideFlags.HideAndDontSave };
		setupView = new(renderGraph);
		setupLighting = new(renderGraph, asset.Lighting, asset.LightCulling);
		lightCulling = new(renderGraph, asset.LightCulling);
		volumetricLight = new(renderGraph, asset);

		occlusionRaytracingShader = Resources.Load<RayTracingShader>("Raytracing/MorrowindOcclusion");
		shadowRaytracingShader = Resources.Load<RayTracingShader>("Raytracing/MorrowindShadow");
		diffuseRaytracingShader = Resources.Load<RayTracingShader>("Raytracing/MorrowindDiffuse");
		depthOfFieldRaytracingShader = Resources.Load<RayTracingShader>("Raytracing/MorrowindDepthOfField");

		var rasSettings = new RayTracingAccelerationStructure.Settings(RayTracingAccelerationStructure.ManagementMode.Automatic, RayTracingAccelerationStructure.RayTracingModeMask.Everything, asset.RayTracingLayerMask);
		rtas = new RayTracingAccelerationStructure(rasSettings);
		SupportedRenderingFeatures.active = SupportedRenderingFeatures;
	}

	protected override void Dispose(bool disposing)
	{
		base.Dispose(disposing);
		rtas.Release();
		volumetricLight.Dispose();
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

		setupView.Render(camera);
		setupView.Render(camera, true, false);
		var (environmentData, sunShadow, pointLightData, pointLights, lightDepthMinMaxBuffer, visibleLightBits, pointLightCount, intersectingLightCount, pointShadows) = setupLighting.Render(camera, cullingResults, context);
		var viewSize = new Int2(camera.pixelWidth, camera.pixelHeight);
		var tanHalfFovY = Geometry.TanHalfFovDegrees(camera.fieldOfView);
		var tanHalfFov = new Float2(tanHalfFovY * camera.aspect, tanHalfFovY);

		var noiseIndex = renderGraph.FrameIndex % 64;
		var blueNoise1D = Resources.Load<Texture2D>(blueNoise1DIds[noiseIndex]);
		var blueNoise2D = Resources.Load<Texture2D>(blueNoise2DIds[noiseIndex]);

		var viewHandle = renderGraph.AddViewInfo(viewSize, asset.Samples);
		var cameraDepth = renderGraph.GetTexture(new(viewHandle, GraphicsFormat.D32_SFloat_S8_UInt, true), Shader.PropertyToID("CameraDepth"));
		var albedoNormal = renderGraph.GetTexture(new(viewHandle, GraphicsFormat.R8G8B8A8_UNorm), Shader.PropertyToID("AlbedoNormal"));
		var cameraColor = renderGraph.GetTexture(new(viewHandle, GraphicsFormat.B10G11R11_UFloatPack32), Shader.PropertyToID("CameraColor"));

		using (var pass = renderGraph.AddRenderPass("Terrain"))
		{
			pass.ViewHandle = viewHandle;
			pass.DepthStencil = cameraDepth;
			pass.AddOutputs(stackalloc[] { albedoNormal, cameraColor });
			pass.AddResources(stackalloc ResourceHandle[] { environmentData });
			pass.AddResource<ViewData>();

			var rendererList = context.CreateRendererList(new(new ShaderTagId("Terrain"), cullingResults, camera) { renderQueueRange = RenderQueueRange.all, sortingCriteria = SortingCriteria.QuantizedFrontToBack });
			pass.SetRenderFunction(rendererList, static (command, rendererList) =>
			{
				command.DrawRendererList(rendererList);
			});
		}

		using (var pass = renderGraph.AddRenderPass("GBuffer"))
		{
			pass.ViewHandle = viewHandle;
			pass.DepthStencil = cameraDepth;
			pass.AddOutputs(stackalloc[] { albedoNormal, cameraColor });
			pass.AddResource(environmentData);
			pass.AddResource<ViewData>();

			var rendererParams = new RendererListParams(cullingResults, new(new("GBuffer"), new(camera) { criteria = SortingCriteria.OptimizeStateChanges }) { enableInstancing = true }, new(RenderQueueRange.opaque));
			var rendererList = context.CreateRendererList(ref rendererParams);
			pass.SetRenderFunction(rendererList, static (command, rendererList) =>
			{
				command.DrawRendererList(rendererList);
			});
		}

		lightCulling.Render(viewHandle, cameraDepth, visibleLightBits, pointLightData, pointLights, pointLightCount, intersectingLightCount);

		var volumetricLight = this.volumetricLight.Render(environmentData, camera, blueNoise1D, sunShadow, pointLightData, pointLights, lightDepthMinMaxBuffer, visibleLightBits, pointShadows);
		var raytracedOcclusion = renderGraph.GetTexture(new(viewHandle, GraphicsFormat.R8_UNorm), Shader.PropertyToID("ScreenSpaceOcclusion"));
		if (asset.RaytracedOcclusion)
		{
			using var pass = renderGraph.AddRenderPass("Raytraced Occlusion");
			pass.ViewHandle = viewHandle;
			pass.AddUavOutput(raytracedOcclusion);
			pass.AddResources(stackalloc ResourceHandle[] { cameraDepth, albedoNormal });

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
			pass.AddResources(stackalloc ResourceHandle[] { cameraDepth, albedoNormal });

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
			pass.AddResources(stackalloc ResourceHandle[] { cameraDepth, albedoNormal });

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
			pass.AddResources(stackalloc ResourceHandle[] { environmentData, pointLightData, pointLights, lightDepthMinMaxBuffer, visibleLightBits, pointShadows });
			pass.AddResource<ViewData>();

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

			if (renderGraph.IsResourceWritten(visibleLightBits))
			{
				pass.AddKeyword("POINT_LIGHTS_ON");
			}

			pass.SetRenderFunction((deferredMaterial, asset.VolumetricDistance), static (command, data) =>
			{
				command.SetGlobalFloat("MaxDepth", data.VolumetricDistance);
				command.DrawProcedural(default, data.deferredMaterial, 0, MeshTopology.Triangles, 3, 1);
			});
		}

		using (var pass = renderGraph.AddRenderPass("Sky"))
		{
			pass.ViewHandle = viewHandle;
			pass.DepthStencil = cameraDepth;
			pass.AddOutput(cameraColor);
			pass.AddResources(stackalloc ResourceHandle[] { environmentData });
			pass.AddResource<ViewData>();

			if (renderGraph.IsResourceWritten(volumetricLight))
			{
				pass.AddResource(volumetricLight);
				pass.AddKeyword("VOLUMETRIC_LIGHT_ON");
			}

			var rendererList = context.CreateRendererList(new(new ShaderTagId("Sky"), cullingResults, camera) { renderQueueRange = RenderQueueRange.all });
			pass.SetRenderFunction((rendererList, asset.VolumetricDistance, backgroundMaterial), static (command, data) =>
			{
				command.SetGlobalFloat("MaxDepth", data.VolumetricDistance);
				command.DrawProcedural(default, data.backgroundMaterial, 0, MeshTopology.Triangles, 3, 1);
				command.DrawRendererList(data.rendererList);
			});
		}

		using (var pass = renderGraph.AddRenderPass("Forward Transparent"))
		{
			pass.ViewHandle = viewHandle;
			pass.DepthStencil = cameraDepth;
			pass.AddOutput(cameraColor);
			pass.AddResources(stackalloc ResourceHandle[] { environmentData, pointLightData, pointLights, lightDepthMinMaxBuffer, visibleLightBits, pointShadows });
			pass.AddResource<ViewData>();

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

			if (renderGraph.IsResourceWritten(visibleLightBits))
			{
				pass.AddKeyword("POINT_LIGHTS_ON");
			}

			var rendererParams = new RendererListParams(cullingResults, new(new("Forward"), new(camera) { criteria = SortingCriteria.BackToFront | SortingCriteria.OptimizeStateChanges }) { enableInstancing = true }, new(RenderQueueRange.transparent));
			var rendererList = context.CreateRendererList(ref rendererParams);
			pass.SetRenderFunction((rendererList, asset.VolumetricDistance), (command, data) =>
			{
				command.SetGlobalFloat("MaxDepth", data.VolumetricDistance);
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

		// Final blit/resolve if needed
		// TODO: This should also account for HDR
		var backbufferViewHandle = renderGraph.AddViewInfo(new(camera.pixelWidth, camera.pixelHeight)); ;
		var backbufferFormat = QualitySettings.activeColorSpace == ColorSpace.Linear ? GraphicsFormat.R8G8B8A8_SRGB : GraphicsFormat.R8G8B8A8_UNorm;
		var targetFormat = camera.targetTexture == null ? backbufferFormat : camera.targetTexture.graphicsFormat;
		var backbufferColor = renderGraph.GetTexture(new(viewHandle, targetFormat), Shader.PropertyToID("BackbufferColor"));
		renderGraph.ExportTexture(backbufferColor, camera.targetTexture == null ? BuiltinRenderTextureType.CameraTarget : camera.targetTexture);

		using (var pass = renderGraph.AddRenderPass("Final Blit"))
		{
			// Can only render directly to backbuffer if there is no msaa samples and there is no target texture
			// TODO: Check for hardware msaa backbuffer resolve support
			pass.ViewHandle = backbufferViewHandle;
			pass.AddOutput(backbufferColor);
			pass.AddResource<ViewData>();

			// For sceneView, take the first depth sample for for gizmos, wireframe, etc.
			var requiresFlip = camera.targetTexture == null;
			var renderToBackbuffer = asset.Samples == 1 && camera.targetTexture == null;
			var passIndex = 0;

#if UNITY_EDITOR
			var requiresSceneDepth = camera.cameraType == CameraType.SceneView || Handles.ShouldRenderGizmos();
			if (requiresSceneDepth)
			{
				passIndex = 1;
				var backbufferDepth = renderGraph.GetTexture(new(viewHandle, GraphicsFormat.D32_SFloat_S8_UInt), Shader.PropertyToID("BackbufferDepth"));
				renderGraph.ExportTexture(backbufferDepth, camera.targetTexture);
				pass.DepthStencil = backbufferDepth;
				pass.AddKeyword("DEPTH");

				if (!renderToBackbuffer)
					pass.AddResource(cameraDepth);
			}
#endif

			if (renderToBackbuffer)
			{
				pass.AddInput(cameraColor);
				pass.AddKeyword("DIRECT");
			}
			else
			{
				pass.AddResource(cameraColor);
			}

			if (requiresFlip)
				pass.AddKeyword("FLIP");

			if (asset.Samples > 1)
				pass.AddKeyword("MSAA");

			if (renderGraph.IsResourceWritten(raytracedDepthOfField))
			{
				pass.AddResource(raytracedDepthOfField);
				pass.AddKeyword("DEPTH_OF_FIELD");
			}

			pass.SetRenderFunction((blitMaterial, passIndex), static (command, data) =>
			{
				command.SetWireframe(false);
				command.DrawProcedural(Matrix4x4.identity, data.blitMaterial, data.passIndex, MeshTopology.Triangles, 3);
			});
		}

		using (var pass = renderGraph.AddRenderPass("Render UI"))
		{
			pass.ViewHandle = backbufferViewHandle;
			pass.AddOutput(backbufferColor);

			if (camera.cameraType == CameraType.SceneView)
				pass.AddResource<ViewDataFlipped>();
			else
				pass.AddResource<ViewData>();

			var rendererList = context.CreateRendererList(new(new ShaderTagId("UI"), cullingResults, camera) { renderQueueRange = RenderQueueRange.all, sortingCriteria = SortingCriteria.CommonTransparent });
			pass.SetRenderFunction(rendererList, static (command, rendererList) =>
			{
				command.DrawRendererList(rendererList);
			});
		}

#if UNITY_EDITOR
		if (Handles.ShouldRenderGizmos())
		{
			using var pass = renderGraph.AddRenderPass("Render Gizmos");
			var preImageEffectsRenderList = context.CreateGizmoRendererList(camera, GizmoSubset.PreImageEffects);
			var postImageEffectsRenderList = context.CreateGizmoRendererList(camera, GizmoSubset.PostImageEffects);

			pass.SetRenderFunction((preImageEffectsRenderList, postImageEffectsRenderList), static (command, data) =>
			{
				// Note that gizmos use their own matrix logic which we can't override
				command.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
				command.DrawRendererList(data.preImageEffectsRenderList);
				command.DrawRendererList(data.postImageEffectsRenderList);
			});
		}

		if (camera.cameraType == CameraType.SceneView)
		{
			using var pass = renderGraph.AddRenderPass("Wireframe");
			pass.AddResource<ViewDataFlipped>();

			var wireframeRendererList = context.CreateWireOverlayRendererList(camera);
			pass.SetRenderFunction((camera, wireframeRendererList, context), static (command, data) =>
			{
				data.context.SetupCameraProperties(data.camera);
				command.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
				command.DrawRendererList(data.wireframeRendererList);
			});
		}
#endif
	}
}