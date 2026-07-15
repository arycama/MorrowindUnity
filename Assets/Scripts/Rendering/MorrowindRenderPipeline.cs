using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Rendering;
using CustomRenderPipeline;
using System;
using Unmath;
using UnityEngine.Experimental.Rendering;
using Unity.Collections;

using static Unmath.Math;
using Unity.Collections.LowLevel.Unsafe;

#if UNITY_EDITOR
using UnityEditor;
#endif

public partial class MorrowindRenderPipeline : CustomRenderPipelineBase<MorrowindRenderPipelineAsset>
{
	public const int maxLightsPerTile = 32;

	//protected override bool RenderUiOverlay => false;
	protected override bool RenderWireframe => false;

	private readonly Material tonemap;
	private readonly NativeList<LightShadowCasterCullingInfo> perLightInfos = new(1, Allocator.Persistent);
	private readonly NativeList<ShadowSplitData> splitBuffer = new(1, Allocator.Persistent);

	private LightData[] pointLights = new LightData[8];
	private float[] pointLightDepths = new float[8];
	private int[] lightDepthBins, lightDepthMinMax;

	public MorrowindRenderPipeline(MorrowindRenderPipelineAsset renderPipelineAsset) : base(renderPipelineAsset)
	{
		tonemap = new Material(Shader.Find("Hidden/Morrowind Tonemap")) { hideFlags = HideFlags.HideAndDontSave };
	}

	protected override List<FrameRenderFeature> InitializePerFrameRenderFeatures() => new()
	{
	};

	protected override List<ViewRenderFeature> InitializePerCameraRenderFeatures() => new()
	{
		new GenericViewRenderFeature(renderGraph, (in ReadOnlySpan<ViewParameter> viewParameters, in ViewPassData viewPassData, in DisplayData displayOutputData, ScriptableRenderContext context) =>
		{
			BeginCameraRendering(context, viewPassData.camera);

			context.SetupCameraProperties(viewPassData.camera);

			var cullingParameters = viewPassData.cullingParameters;
			cullingParameters.shadowDistance = asset.ShadowDistance;
			cullingParameters.cullingOptions = CullingOptions.NeedsLighting | CullingOptions.DisablePerObjectCulling | CullingOptions.ShadowCasters;

			var cullingResults = context.Cull(ref cullingParameters);
			renderGraph.SetResource(new CullingResultsData(cullingResults));

			// Setup globals
			var fogEnabled = RenderSettings.fog;
	#if UNITY_EDITOR
			if (SceneView.currentDrawingSceneView != null)
				fogEnabled &= SceneView.currentDrawingSceneView.sceneViewState.fogEnabled;
	#endif

			var fogStart = RenderSettings.fogStartDistance;
			var fogEnd = RenderSettings.fogEndDistance;
			var fogScale = fogEnabled ? 1 / (fogEnd - fogStart) : 0;
			var fogOffset = fogEnabled ? fogStart / (fogStart - fogEnd) : 0;

			var environmentDataBuffer = renderGraph.SetConstantBuffer(
			(
				RenderSettings.ambientLight.LinearFloat3(),
				fogScale,
				RenderSettings.fogColor.LinearFloat3(),
				fogOffset,
				Time.time,
				Float3.Zero
			));

			renderGraph.SetResource(new EnvironmentData(environmentDataBuffer));

			// Screen
            var screenToPixel = Float4x4.Scale(new Float3((Float2)viewPassData.viewSize, 1));
			var pixelToScreen = Float4x4.Scale(new Float3(1 / (Float2)viewPassData.viewSize, 1));

			// Clip
			var clipToScreen = Float4x4.ScaleOffset(new Float3(0.5f, viewPassData.isFlipped ? -0.5f : 0.5f, 1), new Float2(0.5f, 0).xxy);
			var screenToClip = Float4x4.ScaleOffset(new Float3(2, viewPassData.isFlipped ? -2 : 2, 1), new Float3(-1, viewPassData.isFlipped ? 1 : -1, 0));
			var clipToPixel = screenToPixel.Mul(clipToScreen);
			var pixelToClip = screenToClip.Mul(pixelToScreen);

			// View
			var viewToClip = Float4x4.PerspectiveReverseZ(viewPassData.tanHalfFov, viewPassData.near, viewPassData.far, 0, viewPassData.isFlipped);
			var worldToView = Float4x4.WorldToLocal(viewPassData.position, viewPassData.rotation);

			// World
			var worldToClip = viewToClip.Mul(worldToView);

			renderGraph.SetResource(new ViewData(renderGraph.SetConstantBuffer
			((
				worldToClip,
				viewToClip,
				worldToView,
				pixelToClip,
				(viewPassData.far - viewPassData.near) * Rcp(viewPassData.near * viewPassData.far), Rcp(viewPassData.far), viewPassData.near, viewPassData.far
			))));
		}),

		new GenericViewRenderFeature(renderGraph, (in ReadOnlySpan<ViewParameter> viewParameters, in ViewPassData viewPassData, in DisplayData displayOutputData, ScriptableRenderContext context) =>
		{
			var cullingResults = renderGraph.GetResource<CullingResultsData>().cullingResults;

			var sunDirection = Float3.Up;
			var sunColor = Float3.One * Pi;
			var mainLightIndex = -1;
			var sunShadows = renderGraph.EmptyTexture;
			var worldToSunShadow = Float4x4.Identity;
			var sunShadowsEnabled = false;

			var lightCount = cullingResults.visibleLights.Length;

			Array.Resize(ref pointLights, Max(pointLights.Length, lightCount));
			Array.Resize(ref pointLightDepths, Max(pointLightDepths.Length, lightCount));
			var pointLightCount = 0;

			for (var i = 0; i < lightCount; i++)
			{
				var visibleLight = cullingResults.visibleLights[i];
				var lightToWorld = (Float4x4)visibleLight.localToWorldMatrix;
				var lightColor = visibleLight.finalColor.Float3();
				var lightDirection = -lightToWorld.Forward;
				var splitRange = new RangeInt(0, 0);

				if (visibleLight.lightType == LightType.Directional && mainLightIndex == -1)
				{
					mainLightIndex = i;
					sunDirection = lightDirection;
					sunColor = lightColor;

					if (cullingResults.GetShadowCasterBounds(mainLightIndex, out _))
					{
						var lightRotation = lightToWorld.Rotation;

						var worldToView = Float4x4.Rotate(lightRotation.Inverse);
						var cameraToWorld = Float4x4.TRS(viewPassData.position, viewPassData.rotation, 1);
						var cameraToView = worldToView.Mul(cameraToWorld);

						var near = viewPassData.near;
						var far = asset.ShadowDistance;

						var viewBounds = Geometry.GetFrustumBounds(viewPassData.tanHalfFov, near, far, cameraToView);
						var viewToClip = Float4x4.OrthoReverseZ(-viewBounds.extents.x, viewBounds.extents.x, -viewBounds.extents.y, viewBounds.extents.y, 0, viewBounds.Size.z);

						var worldViewPosition = viewBounds.center;
						worldViewPosition.z = viewBounds.Min.z;
						worldViewPosition = lightRotation.Rotate(worldViewPosition);

						var worldToCascade = Float4x4.WorldToLocal(worldViewPosition, lightRotation);
						worldToSunShadow = Float4x4.OrthoReverseZSample(-viewBounds.extents.x, viewBounds.extents.x, -viewBounds.extents.y, viewBounds.extents.y, 0, viewBounds.Size.z).Mul(worldToCascade);

						sunShadows = renderGraph.GetTexture(asset.ShadowResolution, GraphicsFormat.D16_UNorm, isExactSize: true, clear: true);

						var shadowSplitData = CalculateShadowSplitData(viewToClip.Mul(worldToCascade), lightDirection, true);
						shadowSplitData.shadowCascadeBlendCullingFactor = 1;
						splitBuffer.Add(shadowSplitData);

						splitRange = new RangeInt(perLightInfos.Length, 1);
						sunShadowsEnabled = true;

						var perCascadeData = renderGraph.SetConstantBuffer(viewToClip.Mul(worldToCascade));
						using var pass = renderGraph.AddShadowRenderPass("Directional Shadows");
						pass.Initialize(context, cullingResults, mainLightIndex, asset.ShadowBias, asset.ShadowSlopeBias, false, false, asset.ShadowResolution, 1);
						pass.ReadBuffer("CascadeData", perCascadeData);
						pass.WriteDepth(sunShadows);
					}
				}

				perLightInfos.Add(new LightShadowCasterCullingInfo
				{
					projectionType = visibleLight.lightType == LightType.Directional ? BatchCullingProjectionType.Orthographic : BatchCullingProjectionType.Perspective,
					splitExclusionMask = 0,
					splitRange = splitRange
				});

				// Calculate view depth
				if (visibleLight.lightType == LightType.Spot || visibleLight.lightType == LightType.Point)
				{
					var radius = visibleLight.range;
					var position = lightToWorld.Translation;
					var distanceScale = Flip(Sq(Rcp(radius)), !visibleLight.light.enableSpotReflector);
					var forward = lightToWorld.Forward;
					var halfAngle = 0.5f * Radians(visibleLight.spotAngle);
					var outerCosHalfAngle = Cos(halfAngle);
					var innerCosHalfAngle = 1.0f;
					var isSpot = visibleLight.lightType == LightType.Spot;
					var angleScale = isSpot ? Rcp(outerCosHalfAngle - innerCosHalfAngle) : 0.0f;
					var angleOffset = isSpot ? outerCosHalfAngle * angleScale : 1.0f;
					pointLights[pointLightCount] = new(position, distanceScale, forward, angleScale, visibleLight.finalColor.Float3(), angleOffset);

					// Calcualte center of spot light cone
					if(visibleLight.lightType == LightType.Spot)
					{
						if(outerCosHalfAngle < Sqrt(0.5f))
						{
							position += outerCosHalfAngle * radius * forward;
						}
						else
						{
							position += radius / (2.0f * outerCosHalfAngle) * forward;
						}
					}

					pointLightDepths[pointLightCount] = viewPassData.rotation.InverseRotate(position - viewPassData.position).z;
					pointLightCount++;
				}
			}

			context.CullShadowCasters(cullingResults, new ShadowCastersCullingInfos
			{
				perLightInfos = perLightInfos.AsArray(),
				splitBuffer = splitBuffer.AsArray()
			});

			perLightInfos.Clear();
			splitBuffer.Clear();

			var sunShadowFadeScale = -1.0f / asset.ShadowFadeDistance;
			var sunShadowFadeOffset = asset.ShadowDistance / asset.ShadowFadeDistance;

			var lightingDataBuffer = renderGraph.SetConstantBuffer
			((
				sunDirection,
				sunShadowFadeScale,

				sunColor,
				sunShadowFadeOffset,

				worldToSunShadow,

				Rcp(asset.ShadowResolution),
				(float)asset.ShadowResolution,
				0f, 0f
			));

			renderGraph.SetResource(new LightingData(sunShadows, lightingDataBuffer, sunShadowsEnabled));

			// Sort lights by view depth
			Array.Sort(pointLightDepths, pointLights);

			// Resize Z bins if needed and clear
			Array.Resize(ref lightDepthBins, asset.LightCullDepthSlices);
			Array.Clear(lightDepthBins, 0, lightDepthBins.Length);

			Array.Resize(ref lightDepthMinMax, asset.LightCullDepthSlices);
			for(var i = 0; i < lightDepthMinMax.Length; i++)
				lightDepthMinMax[i] = BitPack(ushort.MaxValue, 16, 0) | BitPack(0, 16, 16);

			// Add sorted lights to list
			var binWidth = viewPassData.far / asset.LightCullDepthSlices;
			for(var i = 0; i < pointLightCount; i++)
			{
				var light = pointLights[i];
				
				// Calculate view min and max depth
				var position = light.position;
				var radius = Rsqrt(light.rcpRangeSq);

				var viewPosition = viewPassData.rotation.InverseRotate(position);

				if(light.angleScale > 0.0f)
				{
					var cosHalfAngle = light.angleOffset / light.angleScale;
					if(cosHalfAngle < Sqrt(0.5f))
					{
						position += cosHalfAngle * radius * light.forward;
						radius *= SinFromCos(cosHalfAngle);
					}
					else
					{
						position += radius / (2.0f * cosHalfAngle) * light.forward;
						radius /= 2.0f * cosHalfAngle;
					}
				}

				var viewZ = pointLightDepths[i];
				var minZ = viewZ - radius;
				var maxZ = viewZ + radius;

				// BitOr with covered Z bins
				var minBin = Max(0, (int)(minZ / binWidth));
				var maxBin = Min(asset.LightCullDepthSlices - 1, (int)(maxZ / binWidth));

				for(var j = minBin; j <= maxBin; j++)
				{
					lightDepthBins[j] |= i;

					var currentMinMax = lightDepthMinMax[j];

					var currentMin = BitUnpack(currentMinMax, 16, 0);
					var currentMax = BitUnpack(currentMinMax, 16, 16);

					currentMin = Min(currentMin, i);
					currentMax = Max(currentMax, i);

					lightDepthMinMax[j] = BitPack(currentMin, 16, 0) | BitPack(currentMax, 16, 16);
				}
			}

			var pointLightBuffer = pointLightCount == 0 ? renderGraph.EmptyBuffer : renderGraph.GetBuffer(pointLightCount, UnsafeUtility.SizeOf<LightData>());
			var lightDepthBinBuffer = renderGraph.GetBuffer(asset.LightCullDepthSlices);
			var lightDepthMinMaxBuffer = renderGraph.GetBuffer(asset.LightCullDepthSlices);

			using (var pass = renderGraph.AddGenericRenderPass("Set Light Data", (pointLights, pointLightCount, pointLightBuffer, lightDepthBinBuffer, lightDepthBins, lightDepthMinMaxBuffer, lightDepthMinMax)))
			{
				pass.WriteBuffer("", pointLightBuffer);
				pass.WriteBuffer("", lightDepthBinBuffer);
				pass.WriteBuffer("", lightDepthMinMaxBuffer);
				pass.SetRenderFunction(static (command, pass, data) =>
				{
					command.SetBufferData(pass.GetBuffer(data.pointLightBuffer), data.pointLights, 0, 0, data.pointLightCount);
					command.SetBufferData(pass.GetBuffer(data.lightDepthBinBuffer), data.lightDepthBins);
					command.SetBufferData(pass.GetBuffer(data.lightDepthMinMaxBuffer), data.lightDepthMinMax);
				});
			}

			var tileCountX = DivRoundUp(viewPassData.viewSize.x, asset.LightCulling.TileSize);
			var tileCountY = DivRoundUp(viewPassData.viewSize.y, asset.LightCulling.TileSize);
			var tileViewOffset = tileCountX * tileCountY * LightCulling.maxLightsPerTile;
			var lightIndexCount = DivRoundUp(pointLightCount, 32);

			var pointLightData = renderGraph.SetConstantBuffer
			((
				(float)asset.LightCulling.TileSize,
				pointLightCount,
				DivRoundUp(viewPassData.viewSize.x, asset.LightCulling.TileSize),
				lightIndexCount,
				asset.LightCullDepthSlices,
				binWidth,
				0,
				0
			));

			renderGraph.SetResource(new PointLightData(pointLightData, pointLightBuffer, pointLightCount, lightDepthBinBuffer, lightDepthMinMaxBuffer));
		}),

		new LightCulling(asset.LightCulling, renderGraph),

		new GenericViewRenderFeature(renderGraph, (in ReadOnlySpan<ViewParameter> viewParameters, in ViewPassData viewPassData, in DisplayData displayOutputData, ScriptableRenderContext context) =>
		{
			var cameraDepth = renderGraph.GetTexture(viewPassData.viewSize, GraphicsFormat.D32_SFloat_S8_UInt, clear: true, isCcw: viewPassData.isFlipped);
			var cameraTarget = renderGraph.GetTexture(viewPassData.viewSize, GraphicsFormat.B10G11R11_UFloatPack32, clear: true, clearColor: viewPassData.camera.backgroundColor.linear, isCcw: viewPassData.isFlipped);

			// Opaque
			var cullingResults = renderGraph.GetResource<CullingResultsData>().cullingResults;
			using (var pass = renderGraph.AddObjectRenderPass("Opaque"))
			{
				pass.Initialize("SRPDefaultUnlit", context, cullingResults, RenderQueueRange.opaque, viewPassData.viewSize, viewPassData.position, viewPassData.rotation, viewPassData.sortAxis, viewPassData.distanceMetric, SortingCriteria.QuantizedFrontToBack | SortingCriteria.OptimizeStateChanges);

				pass.WriteDepth(cameraDepth);
				pass.WriteTexture(cameraTarget);

				pass.ReadResource<ViewData>();
				pass.ReadResource<EnvironmentData>();
				pass.ReadResource<LightingData>();
				pass.ReadResource<PointLightData>();
				pass.ReadResource<LightCulling.Result>();
			}

			// Opaque copy
			var cameraCopy = renderGraph.GetTexture(viewPassData.viewSize, GraphicsFormat.B10G11R11_UFloatPack32);
			using (var pass = renderGraph.AddGenericRenderPass("Opaque"))
			{
				pass.PreventNewSubPass = true;

				pass.ReadTexture("", cameraTarget);
				pass.WriteTexture(cameraCopy);

				pass.SetRenderFunction((command, pass) =>
				{
					command.CopyTexture(pass.GetRenderTexture(cameraTarget), pass.GetRenderTexture(cameraCopy));
				});
			}

			// Transparent
			using (var pass = renderGraph.AddObjectRenderPass("Transparent"))
			{
				pass.Initialize("SRPDefaultUnlit", context, cullingResults, RenderQueueRange.transparent, viewPassData.viewSize, viewPassData.position, viewPassData.rotation, viewPassData.sortAxis, viewPassData.distanceMetric, SortingCriteria.BackToFront | SortingCriteria.OptimizeStateChanges);

				pass.PreventNewSubPass = true;

				pass.WriteDepth(cameraDepth, SubPassFlags.ReadOnlyDepth);
				pass.WriteTexture(cameraTarget);
				pass.ReadTexture("CameraDepth", cameraDepth);
				pass.ReadTexture("CameraColor", cameraCopy);

				pass.ReadResource<ViewData>();
				pass.ReadResource<EnvironmentData>();
				pass.ReadResource<LightingData>();
				pass.ReadResource<PointLightData>();
				pass.ReadResource<LightCulling.Result>();
			}

			// Tonemap
			using(var pass = renderGraph.AddBlitToScreenPass("Tonemap"))
			{
				pass.Initialize(tonemap, viewPassData.viewSize, 1, 0, 1, viewPassData.target, viewPassData.format);
				pass.PreventNewSubPass = true;
				pass.ReadTexture("CameraTarget", cameraTarget);
			}
		}),

		#if UNITY_EDITOR
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
			#endif
	};

	private static ShadowSplitData CalculateShadowSplitData(Float4x4 viewProjectionMatrix, bool skipNearPlane)
	{
		var shadowSplitData = new ShadowSplitData() { shadowCascadeBlendCullingFactor = 1 };
		for (var i = FrustumPlane.Left; i < FrustumPlane.Count; i++)
		{
			if (!skipNearPlane || i != FrustumPlane.Near)
				shadowSplitData.SetCullingPlane(shadowSplitData.cullingPlaneCount++, viewProjectionMatrix.GetFrustumPlane(i));
		}

		return shadowSplitData;
	}

	/// <summary> Add any planes that face away from the light direction. This avoids rendering shadowcasters that can never cast a visible shadow </summary>
	private static ShadowSplitData CalculateShadowSplitData(Float4x4 viewProjectionMatrix, Float3 lightDirection, bool skipNearPlane)
	{
		var shadowSplitData = CalculateShadowSplitData(viewProjectionMatrix, skipNearPlane);
		for (var i = FrustumPlane.Left; i < FrustumPlane.Count; i++)
		{
			var plane = viewProjectionMatrix.GetFrustumPlane(i);
			if (plane.normal.Dot(lightDirection) > 0.0f)
				shadowSplitData.SetCullingPlane(shadowSplitData.cullingPlaneCount++, plane);
		}

		return shadowSplitData;
	}
}
