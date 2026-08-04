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
using Quaternion = Unmath.Quaternion;


#if UNITY_EDITOR
using UnityEditor;
#endif

public partial class MorrowindRenderPipeline : CustomRenderPipelineBase<MorrowindRenderPipelineAsset>
{
	protected override bool RenderUiOverlay => false;
	protected override bool RenderWireframe => false;

	private readonly Dictionary<int, (Float3, Quaternion, Float4x4)> previousCameraTransform = new();

	private readonly Material tonemap;
	private readonly Material pointLightMaterial;
	private readonly NativeList<LightShadowCasterCullingInfo> perLightInfos = new(1, Allocator.Persistent);
	private readonly NativeList<ShadowSplitData> splitBuffer = new(1, Allocator.Persistent);

	private LightData[] pointLights = new LightData[8];
	private float[] pointLightDepths = new float[8];
	private int[] lightDepthMinMax;

	public MorrowindRenderPipeline(MorrowindRenderPipelineAsset renderPipelineAsset) : base(renderPipelineAsset)
	{
		tonemap = new Material(Shader.Find("Hidden/Morrowind Tonemap")) { hideFlags = HideFlags.HideAndDontSave };
		pointLightMaterial = new Material(Shader.Find("Hidden/Point Light")) { hideFlags = HideFlags.HideAndDontSave };
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
				fogStart,
				fogEnd, 
				0f
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
			var clipToView = Float4x4.PerspectiveReverseZInverse(viewPassData.tanHalfFov, viewPassData.near, viewPassData.far);

			var viewToScreen = clipToScreen.Mul(viewToClip);
			var screenToView = clipToView.Mul(screenToClip);

			var viewToPixel = screenToPixel.Mul(viewToScreen);
			var pixelToView = clipToView.Mul(pixelToClip);

			var viewToWorld = Float4x4.TRS(viewPassData.position, viewPassData.rotation, 1.0f);
			var worldToView = Float4x4.WorldToLocal(viewPassData.position, viewPassData.rotation);

            // World
            var worldToClip = viewToClip.Mul(worldToView);
			var clipToWorld = viewToWorld.Mul(clipToView);

			var worldToScreen = clipToScreen.Mul(worldToClip);
			var screenToWorld = viewToWorld.Mul(screenToView);

			var worldToPixel = screenToPixel.Mul(worldToScreen);
			var pixelToWorld = viewToWorld.Mul(pixelToView);

			// Previous frame matrices
            var viewToNonJitteredScreen = clipToScreen.Mul(viewToClip);
			if (!previousCameraTransform.TryGetValue(viewPassData.viewId, out var previousTransform))
				previousTransform = (viewPassData.position, viewPassData.rotation, viewToNonJitteredScreen);

			previousCameraTransform[viewPassData.viewId] = (viewPassData.position, viewPassData.rotation, viewToNonJitteredScreen);

			  var worldToPreviousView = Float4x4.WorldToLocal(previousTransform.Item1 - viewPassData.position, previousTransform.Item2);
			var worldToPreviousScreen = previousTransform.Item3.Mul(worldToPreviousView);
			var pixelToWorldDir = Float4x4.PixelToWorldViewDirectionMatrix(viewPassData.viewSize, 0f, viewPassData.tanHalfFov, viewToWorld, true, false);

			renderGraph.SetResource(new ViewData(renderGraph.SetConstantBuffer
			((
				worldToClip,
				viewToClip,
				worldToView,
				pixelToClip,
				screenToWorld,
				worldToPreviousScreen,
				(viewPassData.far - viewPassData.near) * Rcp(viewPassData.near * viewPassData.far), Rcp(viewPassData.far), viewPassData.near, viewPassData.far,
				(Float2)viewPassData.viewSize,
				1.0f / (Float2)viewPassData.viewSize,
				viewPassData.position,
				0f
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

						var viewBounds = Geometry.GetFrustumBounds(viewPassData.tanHalfFov, viewPassData.near, asset.ShadowDistance, cameraToView);
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

					// Calcualte center of spot light cone
					var cullingSphere = new Float4(position, radius);
					if(visibleLight.lightType == LightType.Spot)
					{
						if(outerCosHalfAngle < Sqrt(0.5f))
						{
							cullingSphere.xyz += outerCosHalfAngle * radius * forward;
							cullingSphere.w *= Sin(halfAngle);
						}
						else
						{
							cullingSphere.xyz += radius / (2.0f * outerCosHalfAngle) * forward;
							cullingSphere.w /= 2.0f * outerCosHalfAngle;
						}
					}

					// Convert to view space
					cullingSphere.xyz = viewPassData.rotation.InverseRotate(cullingSphere.xyz - viewPassData.position);

					pointLights[pointLightCount] = new(position, distanceScale, forward, angleScale, visibleLight.finalColor.Float3(), angleOffset, cullingSphere);
					pointLightDepths[pointLightCount] = cullingSphere.z;
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

			if (pointLightCount == 0)
				return;

			// Sort lights by view depth
			Array.Sort(pointLightDepths, pointLights);

			Array.Resize(ref lightDepthMinMax, asset.LightCullDepthSlices);
			for(var i = 0; i < lightDepthMinMax.Length; i++)
				lightDepthMinMax[i] = BitPack(ushort.MaxValue, 16, 0) | BitPack(0, 16, 16);

			var numSlices = asset.LightCullDepthSlices;

			// Add sorted lights to list
			var binWidth = viewPassData.far / asset.LightCullDepthSlices;
			for(var i = 0; i < pointLightCount; i++)
			{
				var light = pointLights[i];
				
				// Calculate view min and max depth
				var minZ = light.cullingSphere.z - light.cullingSphere.w;
				var maxZ = light.cullingSphere.z + light.cullingSphere.w;

				// BitOr with covered Z bins
				var minBin = Max(0, (int)(minZ / binWidth));
				var maxBin = Min(asset.LightCullDepthSlices - 1, (int)(maxZ / binWidth));

				for(var j = minBin; j <= maxBin; j++)
				{
					var currentMinMax = lightDepthMinMax[j];

					var currentMin = BitUnpack(currentMinMax, 16, 0);
					var currentMax = BitUnpack(currentMinMax, 16, 16);

					currentMin = Min(currentMin, i);
					currentMax = Max(currentMax, i);

					lightDepthMinMax[j] = BitPack(currentMin, 16, 0) | BitPack(currentMax, 16, 16);
				}
			}

			var tileCountX = DivRoundUp(viewPassData.viewSize.x, asset.TileSize);
			var tileCountY = DivRoundUp(viewPassData.viewSize.y, asset.TileSize);
			var lightIndexCount = DivRoundUp(pointLightCount, 32);

			var pointLightBuffer = pointLightCount == 0 ? renderGraph.EmptyBuffer : renderGraph.GetBuffer(pointLightCount, UnsafeUtility.SizeOf<LightData>());
			var lightDepthMinMaxBuffer = renderGraph.GetBuffer(asset.LightCullDepthSlices);
			var visibleLightBits = renderGraph.GetTexture(new(tileCountX, tileCountY), GraphicsFormat.R32_UInt, lightIndexCount, TextureDimension.Tex2DArray, isRandomWrite: true);

			using (var pass = renderGraph.AddGenericRenderPass("Set Light Data", (pointLights, pointLightCount, pointLightBuffer, lightDepthMinMaxBuffer, lightDepthMinMax, visibleLightBits)))
			{
				pass.WriteBuffer("", pointLightBuffer);
				pass.WriteBuffer("", lightDepthMinMaxBuffer);
				pass.WriteTexture(visibleLightBits);

				pass.SetRenderFunction(static (command, pass, data) =>
				{
					command.SetBufferData(pass.GetBuffer(data.pointLightBuffer), data.pointLights, 0, 0, data.pointLightCount);
					command.SetBufferData(pass.GetBuffer(data.lightDepthMinMaxBuffer), data.lightDepthMinMax);

					// Clear the light bitmask texture
					command.SetRenderTarget(pass.GetRenderTexture(data.visibleLightBits), 0, CubemapFace.Unknown, -1);
					command.ClearRenderTarget(false, true, default);
				});
			}

			var pointLightData = renderGraph.SetConstantBuffer
			((
				(float)asset.TileSize,
				pointLightCount,
				DivRoundUp(viewPassData.viewSize.x, asset.TileSize),
				lightIndexCount,
				asset.LightCullDepthSlices,
				binWidth,
				Rcp(asset.TileSize),
				Rcp(binWidth)
			));

			renderGraph.SetResource(new PointLightData(pointLightData, pointLightBuffer, pointLightCount, lightDepthMinMaxBuffer, visibleLightBits));
		}),

		new GenericViewRenderFeature(renderGraph, (in ReadOnlySpan<ViewParameter> viewParameters, in ViewPassData viewPassData, in DisplayData displayOutputData, ScriptableRenderContext context) =>
		{
			var cameraDepth = renderGraph.GetTexture(viewPassData.viewSize, GraphicsFormat.D32_SFloat_S8_UInt, clear: true, isCcw: viewPassData.isFlipped, isScreenTexture: true);
			var cameraTarget = renderGraph.GetTexture(viewPassData.viewSize, GraphicsFormat.B10G11R11_UFloatPack32, clear: true, clearColor: viewPassData.camera.backgroundColor.linear, isCcw: viewPassData.isFlipped, isScreenTexture: true);

			renderGraph.SetRTHandle<CameraDepth>(cameraDepth);
			renderGraph.SetRTHandle<CameraTarget>(cameraTarget);
		}),

		new GenericViewRenderFeature(renderGraph, (in ReadOnlySpan<ViewParameter> viewParameters, in ViewPassData viewPassData, in DisplayData displayOutputData, ScriptableRenderContext context) =>
		{
			if(asset.PointLightMesh == null || !renderGraph.TryGetResource<PointLightData>(out var pointLightData))
				return;

			using var pass = renderGraph.AddDrawInstancedProceduralRenderPass("Light Culling", pointLightData);

			var lightCount = pointLightData.lightCount;

			pass.Initialize(asset.PointLightMesh, 0, pointLightMaterial, lightCount, viewPassData.viewSize, viewPassData.viewCount);
			pass.WriteRtHandleDepth<CameraDepth>();

			pass.ReadResource<ViewData>();
			pass.ReadResource<PointLightData>();

			pass.SetRenderFunction(static (command, pass, data) =>
			{
				command.SetRandomWriteTarget(0, pass.GetRenderTexture(data.visibleLightBits));
				pass.SetTexture(Shader.PropertyToID("VisibleLightBitsWrite"), pass.GetRenderTexture(data.visibleLightBits));
			});
		}),

		//new LightCulling(asset.LightCulling, renderGraph),
		new VolumetricLighting(asset.VolumetricLighting, renderGraph),

		new GenericViewRenderFeature(renderGraph, (in ReadOnlySpan<ViewParameter> viewParameters, in ViewPassData viewPassData, in DisplayData displayOutputData, ScriptableRenderContext context) =>
		{
			// Opaque
			var cullingResults = renderGraph.GetResource<CullingResultsData>().cullingResults;
			using (var pass = renderGraph.AddObjectRenderPass("Opaque"))
			{
				pass.Initialize("Forward", context, cullingResults, RenderQueueRange.opaque, viewPassData.viewSize, viewPassData.position, viewPassData.rotation, viewPassData.sortAxis, viewPassData.distanceMetric, SortingCriteria.QuantizedFrontToBack | SortingCriteria.OptimizeStateChanges);

				pass.WriteRtHandleDepth<CameraDepth>();
				pass.WriteRtHandle<CameraTarget>();

				pass.ReadResource<ViewData>();
				pass.ReadResource<EnvironmentData>();
				pass.ReadResource<LightingData>();
				pass.ReadResource<VolumetricLighting.Result>();

				if (pass.TryReadResource<PointLightData>())
					pass.AddKeyword("POINT_LIGHTS_ON");
			}

			// Opaque copy
			var cameraCopy = renderGraph.GetTexture(viewPassData.viewSize, GraphicsFormat.B10G11R11_UFloatPack32, isScreenTexture: true);
			using (var pass = renderGraph.AddGenericRenderPass("Opaque"))
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

			// Transparent
			using (var pass = renderGraph.AddObjectRenderPass("Transparent"))
			{
				pass.Initialize("Forward", context, cullingResults, RenderQueueRange.transparent, viewPassData.viewSize, viewPassData.position, viewPassData.rotation, viewPassData.sortAxis, viewPassData.distanceMetric, SortingCriteria.BackToFront | SortingCriteria.OptimizeStateChanges);

				pass.PreventNewSubPass = true;

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
			}

			// Tonemap
			using(var pass = renderGraph.AddBlitToScreenPass("Tonemap"))
			{
				pass.Initialize(tonemap, viewPassData.viewSize, 1, 0, 1, viewPassData.target, viewPassData.format);
				pass.PreventNewSubPass = true;
				pass.ReadRtHandle<CameraTarget>();
			}
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
