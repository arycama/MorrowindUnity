using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using CustomRenderPipeline;
using System;
using UnityEngine.Experimental.Rendering;
using System.Reflection;


#if UNITY_EDITOR
using UnityEditor;
#endif

public partial class MorrowindRenderPipeline : CustomRenderPipelineBase<MorrowindRenderPipelineAsset>
{
	private static readonly IndexedString blueNoise1DIds = new("STBN/stbn_vec1_2Dx1D_128x128x64_", 64);
	private static readonly IndexedString blueNoise2DIds = new("STBN/stbn_vec2_2Dx1D_128x128x64_", 64);
	private static readonly IndexedString blueNoise3DIds = new("STBN/stbn_vec3_2Dx1D_128x128x64_", 64);

	private static readonly IndexedString blueNoise2DUnitIds = new("STBN/stbn_unitvec2_2Dx1D_128x128x64_", 64);
	private static readonly IndexedString blueNoise3DUnitIds = new("STBN/stbn_unitvec3_2Dx1D_128x128x64_", 64);
	private static readonly IndexedString blueNoise3DCosineIds = new("STBN/stbn_unitvec3_cosine_2Dx1D_128x128x64_", 64);

	private static readonly int BlueNoise1DId = Shader.PropertyToID("BlueNoise1D");
	private static readonly int BlueNoise2DId = Shader.PropertyToID("BlueNoise2D");
	private static readonly int BlueNoise3DId = Shader.PropertyToID("BlueNoise3D");
	private static readonly int BlueNoise2DUnitId = Shader.PropertyToID("BlueNoise2DUnit");
	private static readonly int BlueNoise3DUnitId = Shader.PropertyToID("BlueNoise3DUnit");
	private static readonly int BlueNoise3DCosineId = Shader.PropertyToID("BlueNoise3DCosine");

	protected override bool RenderUiOverlay => false;
	protected override bool RenderWireframe => false;

	private readonly Material tonemap, deferredLightingMaterial;
	private readonly RayTracingShader shadowRaytracingShader, diffuseRaytracingShader, occlusionRaytracingShader;

	public MorrowindRenderPipeline(MorrowindRenderPipelineAsset renderPipelineAsset) : base(renderPipelineAsset)
	{
		tonemap = new Material(Shader.Find("Hidden/Morrowind Tonemap")) { hideFlags = HideFlags.HideAndDontSave };
		deferredLightingMaterial = new Material(Shader.Find("Hidden/Morrowind Deferred")) { hideFlags = HideFlags.HideAndDontSave };
        shadowRaytracingShader = Resources.Load<RayTracingShader>("Raytracing/MorrowindShadow");
		diffuseRaytracingShader = Resources.Load<RayTracingShader>("Raytracing/MorrowindDiffuse");
		occlusionRaytracingShader = Resources.Load<RayTracingShader>("Raytracing/MorrowindOcclusion");
	}

	protected override List<FrameRenderFeature> InitializePerFrameRenderFeatures() => new()
	{
		new RaytracingSystem(renderGraph, asset.RayTracingSettings),
	};

	protected override List<ViewRenderFeature> InitializePerCameraRenderFeatures() => new()
	{
		new SetupCamera(renderGraph, asset.LightingSettings, asset),
		new SetupLighting(renderGraph, asset.LightingSettings, asset.LightCulling),

		new GenericViewRenderFeature(renderGraph, (in ReadOnlySpan<ViewParameter> viewParameters, in ViewPassData viewPassData, in DisplayData displayOutputData, ScriptableRenderContext context) =>
		{
			var cameraDepth = renderGraph.GetTexture(viewPassData.viewSize, GraphicsFormat.D32_SFloat_S8_UInt, clear: true, isScreenTexture: true);
			var albedoMetallic = renderGraph.GetTexture(viewPassData.viewSize, GraphicsFormat.R8G8B8A8_SRGB, isScreenTexture: true);
			var normalOcclusionRoughness = renderGraph.GetTexture(viewPassData.viewSize, GraphicsFormat.R8G8B8A8_UNorm, isScreenTexture: true);
			var cameraTarget = renderGraph.GetTexture(viewPassData.viewSize, GraphicsFormat.B10G11R11_UFloatPack32, clear: true, clearColor: RenderSettings.fogColor.linear, isScreenTexture: true);

			renderGraph.SetRTHandle<CameraDepth>(cameraDepth);
			renderGraph.SetRTHandle<GBufferAlbedoMetallic>(albedoMetallic);
			renderGraph.SetRTHandle<GBufferNormalOcclusionRoughness>(normalOcclusionRoughness);
			renderGraph.SetRTHandle<CameraTarget>(cameraTarget);

			var noiseIndex = renderGraph.FrameIndex % 64;
			var blueNoise1D = Resources.Load<Texture2D>(blueNoise1DIds[noiseIndex]);
			var blueNoise2D = Resources.Load<Texture2D>(blueNoise2DIds[noiseIndex]);
			var blueNoise3D = Resources.Load<Texture2D>(blueNoise3DIds[noiseIndex]);
			var blueNoise2DUnit = Resources.Load<Texture2D>(blueNoise2DUnitIds[noiseIndex]);
			var blueNoise3DUnit = Resources.Load<Texture2D>(blueNoise3DUnitIds[noiseIndex]);
			var blueNoise3DCosine = Resources.Load<Texture2D>(blueNoise3DCosineIds[noiseIndex]);

			var cullingResults = renderGraph.GetResource<CullingResultsData>().cullingResults;
			using var pass = renderGraph.AddObjectRenderPass("Opaque", (blueNoise1D, blueNoise2D, blueNoise3D, blueNoise2DUnit, blueNoise3DUnit, blueNoise3DCosine));
			pass.PreventNewSubPass = true;

			pass.Initialize("GBuffer", context, cullingResults, RenderQueueRange.opaque, viewPassData.viewSize, viewPassData.position, viewPassData.rotation, viewPassData.sortAxis, viewPassData.distanceMetric, SortingCriteria.QuantizedFrontToBack | SortingCriteria.OptimizeStateChanges, isScreenPass: true);

			pass.WriteRtHandleDepth<CameraDepth>();
			pass.WriteRtHandle<GBufferAlbedoMetallic>();
			pass.WriteRtHandle<GBufferNormalOcclusionRoughness>();
			pass.WriteRtHandle<CameraTarget>();
			pass.ReadResource<ViewData>();

			pass.SetRenderFunction(static (command, pass, data) =>
			{
				pass.SetTexture(BlueNoise1DId, data.blueNoise1D);
				pass.SetTexture(BlueNoise2DId, data.blueNoise2D);
				pass.SetTexture(BlueNoise3DId, data.blueNoise3D);
				pass.SetTexture(BlueNoise2DUnitId, data.blueNoise2DUnit);
				pass.SetTexture(BlueNoise3DUnitId, data.blueNoise3DUnit);
				pass.SetTexture(BlueNoise3DCosineId, data.blueNoise3DCosine);
			});
		}),

		//new LightCulling(asset.LightCulling, renderGraph, "Hidden/Morrowind Point Light"),
		new VolumetricLighting(asset.VolumetricLighting, renderGraph),

		new GenericViewRenderFeature(renderGraph, (in ReadOnlySpan<ViewParameter> viewParameters, in ViewPassData viewPassData, in DisplayData displayOutputData, ScriptableRenderContext context) =>
		{
			return;
			if(!renderGraph.TryGetResource<RaytracingResult>(out var raytracingData) || raytracingData.Rtas == null)
				return;

			var result = renderGraph.GetTexture(viewPassData.viewSize, GraphicsFormat.R8_UNorm, isScreenTexture: true, isRandomWrite: true);
			using var pass = renderGraph.AddRaytracingRenderPass("Raytraced Shadows");
			pass.PreventNewSubPass = true;

			pass.Initialize(occlusionRaytracingShader, "RayGeneration", "RaytracedTransmittance", raytracingData.Rtas, viewPassData.viewSize.x, viewPassData.viewSize.y, 1, raytracingData.Bias, raytracingData.DistantBias, viewPassData.tanHalfFov.y);
			pass.WriteTexture(result, "HitResult");
			pass.ReadRtHandle<GBufferNormalOcclusionRoughness>();
			pass.ReadRtHandle<CameraDepth>();
			pass.ReadResource<ViewData>();
			renderGraph.SetRTHandle<ScreenSpaceOcclusion>(result);
		}),

		new GenericViewRenderFeature(renderGraph, (in ReadOnlySpan<ViewParameter> viewParameters, in ViewPassData viewPassData, in DisplayData displayOutputData, ScriptableRenderContext context) =>
		{
			var field = typeof(RayTracingAccelerationStructure).GetField("m_Ptr", BindingFlags.NonPublic | BindingFlags.Instance);
			if(!renderGraph.TryGetResource<RaytracingResult>(out var raytracingData) || raytracingData.Rtas == null || (IntPtr)field.GetValue(raytracingData.Rtas) == IntPtr.Zero)
				return;

            var result = renderGraph.GetTexture(viewPassData.viewSize, GraphicsFormat.R8_UNorm, isScreenTexture: true, isRandomWrite: true);
			using var pass = renderGraph.AddRaytracingRenderPass("Raytraced Shadows");
			pass.PreventNewSubPass = true;

			pass.Initialize(shadowRaytracingShader, "RayGeneration", "RaytracedTransmittance", raytracingData.Rtas, viewPassData.viewSize.x, viewPassData.viewSize.y, 1, raytracingData.Bias, raytracingData.DistantBias, viewPassData.tanHalfFov.y);
			pass.WriteTexture(result, "HitResult");
			pass.ReadRtHandle<GBufferNormalOcclusionRoughness>();
			pass.ReadRtHandle<CameraDepth>();
			pass.ReadResource<ViewData>();
			renderGraph.SetRTHandle<ScreenShadows>(result);
		}),

		new GenericViewRenderFeature(renderGraph, (in ReadOnlySpan<ViewParameter> viewParameters, in ViewPassData viewPassData, in DisplayData displayOutputData, ScriptableRenderContext context) =>
		{
			return;
			if(!renderGraph.TryGetResource<RaytracingResult>(out var raytracingData) || raytracingData.Rtas == null)
				return;

			var result = renderGraph.GetTexture(viewPassData.viewSize, GraphicsFormat.B10G11R11_UFloatPack32, isScreenTexture: true, isRandomWrite: true);
			using var pass = renderGraph.AddRaytracingRenderPass("Raytraced Shadows");
			pass.PreventNewSubPass = true;

			pass.Initialize(diffuseRaytracingShader, "RayGeneration", "RaytracedDiffuse", raytracingData.Rtas, viewPassData.viewSize.x, viewPassData.viewSize.y, 1, raytracingData.Bias, raytracingData.DistantBias, viewPassData.tanHalfFov.y);
			pass.WriteTexture(result, "HitResult");
			pass.ReadRtHandle<GBufferNormalOcclusionRoughness>();
			pass.ReadRtHandle<CameraDepth>();
			pass.ReadResource<ViewData>();
			renderGraph.SetRTHandle<ScreenSpaceDiffuse>(result);
		}),

		new GenericViewRenderFeature(renderGraph, (in ReadOnlySpan<ViewParameter> viewParameters, in ViewPassData viewPassData, in DisplayData displayOutputData, ScriptableRenderContext context) =>
		{
			using var pass = renderGraph.AddFullscreenRenderPass("Deferred Lighting");
			pass.PreventNewSubPass = true;

			pass.Initialize(deferredLightingMaterial, viewPassData.viewSize, isScreenPass: true);
			pass.WriteRtHandleDepth<CameraDepth>(SubPassFlags.ReadOnlyDepthStencil);
			pass.WriteRtHandle<CameraTarget>();
			pass.ReadRtHandle<CameraDepth>();
			pass.ReadRtHandle<GBufferAlbedoMetallic>();
			pass.ReadRtHandle<GBufferNormalOcclusionRoughness>();

			pass.ReadResource<ViewData>();
			pass.ReadResource<EnvironmentData>();
			pass.ReadResource<LightingData>();
			pass.ReadResource<VolumetricLighting.Result>();

			if(renderGraph.TryGetResource<RaytracingResult>(out var raytracingData))
			{
				pass.ReadRtHandle<ScreenShadows>();
				pass.ReadRtHandle<ScreenSpaceOcclusion>();
				pass.ReadRtHandle<ScreenSpaceDiffuse>();
				pass.AddKeyword("RAYTRACING_ON");
			}

			if (pass.TryReadResource<PointLightData>())
				pass.AddKeyword("POINT_LIGHTS_ON");

			if(asset.VolumetricLighting.Enabled)
				pass.AddKeyword("VOLUMETRIC_LIGHT_ON");
		}),

		new GenericViewRenderFeature(renderGraph, (in ReadOnlySpan<ViewParameter> viewParameters, in ViewPassData viewPassData, in DisplayData displayOutputData, ScriptableRenderContext context) =>
		{
			using var pass = renderGraph.AddObjectRenderPass("Sky");
			pass.PreventNewSubPass = true;

			var cullingResults = renderGraph.GetResource<CullingResultsData>().cullingResults;
			pass.Initialize("Sky", context, cullingResults, RenderQueueRange.all, viewPassData.viewSize, viewPassData.position, viewPassData.rotation, viewPassData.sortAxis, viewPassData.distanceMetric, isScreenPass: true);
			pass.PreventNewSubPass = true;
			pass.WriteRtHandleDepth<CameraDepth>(SubPassFlags.ReadOnlyDepthStencil);
			pass.WriteRtHandle<CameraTarget>();
			pass.ReadResource<ViewData>();
			pass.ReadResource<EnvironmentData>();
			pass.ReadResource<VolumetricLighting.Result>();
		}),

		new GenericViewRenderFeature(renderGraph, (in ReadOnlySpan<ViewParameter> viewParameters, in ViewPassData viewPassData, in DisplayData displayOutputData, ScriptableRenderContext context) =>
		{
			var cullingResults = renderGraph.GetResource<CullingResultsData>().cullingResults;

			// Opaque copy
			var cameraCopy = renderGraph.GetTexture(viewPassData.viewSize, GraphicsFormat.B10G11R11_UFloatPack32, isScreenTexture: true);
			using (var pass = renderGraph.AddGenericRenderPass("Opaque Copy"))
			{
				pass.PreventNewSubPass = true;

				var cameraTarget = renderGraph.GetRtHandleData<CameraTarget>().handle;
				pass.ReadTexture("", cameraTarget);
				pass.WriteTexture(cameraCopy);

				pass.SetRenderFunction((command, pass) =>
				{
					command.CopyTexture(pass.GetRenderTexture(cameraTarget), pass.GetRenderTexture(cameraCopy));
				});
			}

			using (var pass = renderGraph.AddObjectRenderPass("Transparent"))
			{
				pass.PreventNewSubPass = true;
				pass.Initialize("Forward", context, cullingResults, RenderQueueRange.transparent, viewPassData.viewSize, viewPassData.position, viewPassData.rotation, viewPassData.sortAxis, viewPassData.distanceMetric, SortingCriteria.BackToFront | SortingCriteria.OptimizeStateChanges, isScreenPass: true);

				pass.WriteRtHandleDepth<CameraDepth>(SubPassFlags.ReadOnlyDepth);
				pass.WriteRtHandle<CameraTarget>();

				pass.ReadRtHandle<CameraDepth>();
				pass.ReadTexture("CameraColor", cameraCopy);

				pass.ReadResource<ViewData>();
				pass.ReadResource<EnvironmentData>();
				pass.ReadResource<LightingData>();
				pass.ReadResource<VolumetricLighting.Result>();

				if (pass.TryReadResource<PointLightData>())
					pass.AddKeyword("POINT_LIGHTS_ON");

				if(asset.VolumetricLighting.Enabled)
					pass.AddKeyword("VOLUMETRIC_LIGHT_ON");
			}

			// Tonemap
			using(var pass = renderGraph.AddBlitToScreenPass("Tonemap"))
			{
				pass.Initialize(tonemap, viewPassData.viewSize, 1, 0, 1, viewPassData.target, viewPassData.format);
				pass.PreventNewSubPass = true;
				pass.ReadRtHandle<CameraTarget>();
				pass.ReadResource<ViewData>();

				if(viewPassData.isFlipped)
					pass.AddKeyword("FLIP");
			}

			//using (var pass = renderGraph.AddObjectScreenRenderPass("UI"))
			//{
			//	pass.Initialize("UI", context, cullingResults, RenderQueueRange.all, viewPassData.viewSize, viewPassData.position, viewPassData.rotation, viewPassData.sortAxis, viewPassData.distanceMetric, SortingCriteria.CommonTransparent, viewCount: viewPassData.viewCount, stereoMode: viewPassData.stereoMode, frameBufferFormat: viewPassData.format, frameBufferTarget: new RenderTargetIdentifier(viewPassData.target, 0, CubemapFace.Unknown, -1));

			//	pass.PreventNewSubPass = true;
			//	pass.WriteRtHandleDepth<CameraDepth>();
			//	pass.ReadResource<ViewData>();
			//}
		}),

		#if UNITY_EDITOR
			new GenericViewRenderFeature(renderGraph, (in ReadOnlySpan<ViewParameter> viewParameters, in ViewPassData viewPassData, in DisplayData displayOutputData, ScriptableRenderContext context) =>
			{
				if(!Handles.ShouldRenderGizmos())
					return;

				var preImageEffects = context.CreateGizmoRendererList(viewPassData.camera, GizmoSubset.PreImageEffects);
				var postImageEffects = context.CreateGizmoRendererList(viewPassData.camera, GizmoSubset.PostImageEffects);

				using (var pass = renderGraph.AddGenericRenderPass("Render Gizmos", (viewPassData.target, preImageEffects, postImageEffects)))
				{
					pass.SetRenderFunction(static (command, pass, data) =>
					{
						command.SetRenderTarget(data.target);
						command.DrawRendererList(data.preImageEffects);
						command.DrawRendererList(data.postImageEffects);
					});
				}
			}),

			new GenericViewRenderFeature(renderGraph, (in ReadOnlySpan<ViewParameter> viewParameters, in ViewPassData viewPassData, in DisplayData displayOutputData, ScriptableRenderContext context) =>
			{
				var wireOverlay = context.CreateWireOverlayRendererList(viewPassData.camera);

				using var pass = renderGraph.AddGenericRenderPass("Render Wireframe", (wireOverlay, viewPassData.target));
				pass.SetRenderFunction(static (command, pass, data) =>
				{
					command.SetRenderTarget(data.target);
					command.DrawRendererList(data.wireOverlay);
				});
			}),
			#endif
	};
}