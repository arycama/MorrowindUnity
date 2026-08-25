using CustomRenderPipeline;
using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Pool;
using UnityEngine.Rendering;
using Unmath;
using static Unmath.Math;
using Quaternion = Unmath.Quaternion;

public class SetupLightingOld : ViewRenderFeature
{
	private readonly NativeList<LightShadowCasterCullingInfo> perLightInfos = new(1, Allocator.Persistent);
	private readonly NativeList<ShadowSplitData> splitBuffer = new(1, Allocator.Persistent);

	private static IndexedString directionalCascadeIds = new("Directional Cascade "),
		pointLightIds = new("Point Light "),
		SpotLightIds = new("Spot Light ");

	private LightData[] pointLights = new LightData[8];
	private float[] pointLightDepths = new float[8];
	private int[] lightDepthMinMax;

	private readonly LightingSettings lighting;
	private readonly LightCulling.Settings lightCulling;

	public SetupLightingOld(CustomRenderPipeline.RenderGraph renderGraph, LightingSettings lighting, LightCulling.Settings lightCulling) : base(renderGraph)
	{
		this.lighting = lighting;
		this.lightCulling = lightCulling;
	}

	public override void Render(in ReadOnlySpan<ViewParameter> viewParameters, in ViewPassData viewPassData, in DisplayData displayOutputData, ScriptableRenderContext context)
	{
		var cullingResults = renderGraph.GetResource<CullingResultsData>().cullingResults;

		var sunDirection = viewPassData.rotation.InverseRotate(Float3.Up);
		var sunColor = Float3.One * Pi;
		var mainLightIndex = -1;
		var sunShadows = renderGraph.EmptyTexture;
		var viewToSunShadow = Float4x4.Identity;
		var sunShadowsEnabled = false;

		var lightCount = cullingResults.visibleLights.Length;

		Array.Resize(ref pointLights, Max(pointLights.Length, lightCount));
		Array.Resize(ref pointLightDepths, Max(pointLightDepths.Length, lightCount));
		var pointLightCount = 0;

		var pointShadowRequests = ListPool<ShadowRequest>.Get();
		var spotShadowRequests = ListPool<ShadowRequest>.Get();

		for (var i = 0; i < lightCount; i++)
		{
			var visibleLight = cullingResults.visibleLights[i];
			var lightToWorld = (Float4x4)visibleLight.localToWorldMatrix;
			var lightColor = visibleLight.finalColor.Float3();
			var lightDirection = -lightToWorld.Forward;
			var splitRange = new RangeInt(0, 0);
			var lightRotation = lightToWorld.Rotation;
			var viewSpaceLightRotation = viewPassData.rotation.InverseRotate(lightRotation);
			var hasShadows = visibleLight.light.shadows != LightShadows.None;

			if (visibleLight.lightType == LightType.Directional && mainLightIndex == -1)
			{
				mainLightIndex = i;
				sunDirection = -viewSpaceLightRotation.Forward;
				sunColor = lightColor;

				if (hasShadows && cullingResults.GetShadowCasterBounds(mainLightIndex, out _))
				{
					// Transform from view space to light space
					var viewToLight = Float4x4.Rotate(viewSpaceLightRotation.Inverse);
					var viewSpaceLightBounds = Geometry.GetFrustumBounds(viewPassData.tanHalfFov, viewPassData.near, lighting.DirectionalShadowDistance, viewToLight);

					// Matrix that goes from world space to light space
					var worldToLight = Float4x4.Rotate(lightRotation.Inverse);
					var worldToLightClip = Float4x4.OrthoReverseZ(viewSpaceLightBounds).Mul(worldToLight);

					var shadowSplitData = CalculateShadowSplitData(worldToLightClip, lightDirection, true);
					shadowSplitData.shadowCascadeBlendCullingFactor = 1;
					splitBuffer.Add(shadowSplitData);

					splitRange = new RangeInt(perLightInfos.Length, 1);
					sunShadowsEnabled = true;

					var perCascadeData = renderGraph.SetConstantBuffer(worldToLightClip);

					// Matrix that converts from view space to shadow-sampling space
					viewToSunShadow = Float4x4.OrthoReverseZSample(viewSpaceLightBounds).Mul(viewToLight);

					using var pass = renderGraph.AddShadowRenderPass("Directional Shadows");
					pass.PreventNewSubPass = true;

					sunShadows = renderGraph.GetTexture(lighting.DirectionalShadowResolution, GraphicsFormat.D16_UNorm, isExactSize: true, clear: true);
					pass.Initialize(context, cullingResults, mainLightIndex, lighting.DirectionalShadowBias, lighting.DirectionalShadowSlopeBias, false, false, lighting.DirectionalShadowResolution, 1);
					pass.ReadBuffer("CascadeData", perCascadeData);
					pass.WriteDepth(sunShadows);
				}
			}

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
				if (visibleLight.lightType == LightType.Spot)
				{
					if (outerCosHalfAngle < Sqrt(0.5f))
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

				// Reject lights that are fully behind the near plane since Unity doesn't do it automatically..
				if (cullingSphere.z + cullingSphere.w <= viewPassData.near)
					continue;

				// Shadows
				var shadowIndex = uint.MaxValue;
				var nearPlane = visibleLight.light.shadowNearPlane;
				if (hasShadows && cullingResults.GetShadowCasterBounds(i, out _) && visibleLight.lightType == LightType.Point)
				{
					shadowIndex = (uint)pointShadowRequests.Count;
					splitRange = new RangeInt(splitBuffer.Length, 6);

					for (var j = 0; j < 6; j++)
					{
						var faceForward = Float4x4.lookAtList[j];
						var rotation = Quaternion.LookRotation(faceForward, Float4x4.upVectorList[j]);
						var worldToView = Float4x4.WorldToLocal(position, rotation);
						var viewToClip = Float4x4.PerspectiveReverseZ(1, nearPlane, radius);
						var worldToClip = viewToClip.Mul(worldToView);
						var shadowSplitData = CalculateShadowSplitData(worldToClip, faceForward, false);

						// Convert to camera relative
						var cameraInverseTranslation = Float4x4.Translate(viewPassData.position);
						worldToView = worldToView.Mul(cameraInverseTranslation);

						pointShadowRequests.Add(new(i, worldToView, viewToClip, shadowSplitData, j, position, true, nearPlane, radius, position, lightRotation, 1, 1, lighting.PointShadowResolution));
						splitBuffer.Add(shadowSplitData);
					}
				}

				position = viewPassData.rotation.InverseRotate(position - viewPassData.position);

				var shadowProjectionX = 1.0f + radius / (nearPlane - radius);
				var shadowProjectionY = nearPlane * radius / (radius - nearPlane);

				pointLights[pointLightCount] = new(position, distanceScale, forward, angleScale, visibleLight.finalColor.Float3(), angleOffset, cullingSphere, shadowIndex, shadowProjectionX, shadowProjectionY);
				pointLightDepths[pointLightCount] = cullingSphere.z - cullingSphere.w * 1.075f;
				pointLightCount++;
			}

			perLightInfos.Add(new LightShadowCasterCullingInfo
			{
				projectionType = visibleLight.lightType == LightType.Directional ? BatchCullingProjectionType.Orthographic : BatchCullingProjectionType.Perspective,
				splitExclusionMask = 0,
				splitRange = splitRange
			});
		}

		context.CullShadowCasters(cullingResults, new ShadowCastersCullingInfos
		{
			perLightInfos = perLightInfos.AsArray(),
			splitBuffer = splitBuffer.AsArray()
		});

		perLightInfos.Clear();
		splitBuffer.Clear();

		var pointShadowCount = Max(1, pointShadowRequests.Count);
		var pointShadows = renderGraph.GetTexture(lighting.PointShadowResolution, GraphicsFormat.D16_UNorm, pointShadowCount, TextureDimension.Tex2DArray, isExactSize: true);
		using (var pass = renderGraph.AddGenericRenderPass("Render Shadows Setup", pointShadows))
		{
			pass.PreventNewSubPass = true;
			pass.WriteTexture(pointShadows);

			if (pointShadowRequests.Count > 0)
			{
				pass.SetRenderFunction(static (command, pass, pointShadows) =>
				{
					command.SetRenderTarget(pass.GetRenderTexture(pointShadows), 0, CubemapFace.Unknown, -1);
					command.ClearRenderTarget(true, false, default);
				});
			}
		}

		for (var i = 0; i < pointShadowRequests.Count; i++)
		{
			using (renderGraph.AddProfileScope(pointLightIds[i]))
			{
				var request = pointShadowRequests[i];
				var perCascadeData = renderGraph.SetConstantBuffer(request.ProjectionMatrix.Mul(request.ViewMatrix));
				using var pass = renderGraph.AddShadowRenderPass("Render Shadow");
				pass.ReadBuffer("CascadeData", perCascadeData);
				pass.Initialize(context, cullingResults, request.LightIndex, lighting.PointShadowBias, lighting.PointShadowSlopeBias, true, true, lighting.PointShadowResolution, pointShadowCount);
				pass.DepthSlice = i;
				pass.WriteDepth(pointShadows);
			}
		}

		ListPool<ShadowRequest>.Release(pointShadowRequests);

		var sunShadowFadeScale = -1.0f / lighting.DirectionalFadeLength;
		var sunShadowFadeOffset = lighting.DirectionalShadowDistance / lighting.DirectionalFadeLength;

		var viewSunDirection = sunDirection;

		var lightingDataBuffer = renderGraph.SetConstantBuffer
		((
			viewSunDirection,
			sunShadowFadeScale,

			sunColor,
			sunShadowFadeOffset,

			viewToSunShadow.r0,
			viewToSunShadow.r1,
			viewToSunShadow.r2,

			Rcp(lighting.DirectionalShadowResolution),
			(float)lighting.DirectionalShadowResolution,
			0f, 0f
		));

		renderGraph.SetResource(new LightingData(sunShadows, lightingDataBuffer, sunShadowsEnabled));

		if (pointLightCount == 0)
			return;

		// Sort lights by view depth
		Array.Sort(pointLightDepths, pointLights);

		Array.Resize(ref lightDepthMinMax, lightCulling.DepthSlices);
		for (var i = 0; i < lightDepthMinMax.Length; i++)
			lightDepthMinMax[i] = BitPack(ushort.MaxValue, 16, 0) | BitPack(0, 16, 16);

		// Add sorted lights to list
		var binWidth = viewPassData.far / lightCulling.DepthSlices;
		var intersectingLightCount = 0;

		for (var i = 0; i < pointLightCount; i++)
		{
			var light = pointLights[i];

			// Calculate view min and max depth
			var minZ = light.cullingSphere.z - light.cullingSphere.w;
			var maxZ = light.cullingSphere.z + light.cullingSphere.w;

			// BitOr with covered Z bins
			var minBin = Max(0, (int)(minZ / binWidth));
			var maxBin = Min(lightCulling.DepthSlices - 1, (int)(maxZ / binWidth));

			for (var j = minBin; j <= maxBin; j++)
			{
				var currentMinMax = lightDepthMinMax[j];

				var currentMin = BitUnpack(currentMinMax, 16, 0);
				var currentMax = BitUnpack(currentMinMax, 16, 16);

				currentMin = Min(currentMin, i);
				currentMax = Max(currentMax, i);

				lightDepthMinMax[j] = BitPack(currentMin, 16, 0) | BitPack(currentMax, 16, 16);
			}

			// Check if the light intersects the near plane
			if(pointLightDepths[i] < viewPassData.near)
				intersectingLightCount = i + 1;
		}

		var tileCountX = DivRoundUp(viewPassData.viewSize.x, lightCulling.TileSize);
		var tileCountY = DivRoundUp(viewPassData.viewSize.y, lightCulling.TileSize);
		var lightIndexCount = DivRoundUp(pointLightCount, 32);

		var pointLightBuffer = pointLightCount == 0 ? renderGraph.EmptyBuffer : renderGraph.GetBuffer(pointLightCount, UnsafeUtility.SizeOf<LightData>());
		var lightDepthMinMaxBuffer = renderGraph.GetBuffer(lightCulling.DepthSlices);
		var visibleLightBits = renderGraph.GetTexture(new(tileCountX, tileCountY), GraphicsFormat.R32_UInt, lightIndexCount, TextureDimension.Tex2DArray, isRandomWrite: true);

		using (var pass = renderGraph.AddGenericRenderPass("Set Light Data", (pointLights, pointLightCount, pointLightBuffer, lightDepthMinMaxBuffer, lightDepthMinMax, visibleLightBits)))
		{
			pass.PreventNewSubPass = true;
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
			(float)lightCulling.TileSize,
			pointLightCount,
			DivRoundUp(viewPassData.viewSize.x, lightCulling.TileSize),
			lightIndexCount,
			lightCulling.DepthSlices,
			binWidth,
			Rcp(lightCulling.TileSize),
			Rcp(binWidth)
		));

		renderGraph.SetResource(new PointLightData(pointLightData, pointLightBuffer, pointLightCount, lightDepthMinMaxBuffer, visibleLightBits, intersectingLightCount, pointShadows));
	}

	private static ShadowSplitData CalculateShadowSplitData(Float4x4 matrix, Float3 lightDirection, bool skipNearPlane)
	{
		var shadowSplitData = new ShadowSplitData() { shadowCascadeBlendCullingFactor = 1 };
		for (var i = FrustumPlane.Left; i < FrustumPlane.Count; i++)
		{
			if (!skipNearPlane || i != FrustumPlane.Near)
			{
				var plane = matrix.GetFrustumPlane(i);
				shadowSplitData.SetCullingPlane(shadowSplitData.cullingPlaneCount++, plane);
			}
		}

		for (var i = FrustumPlane.Left; i < FrustumPlane.Count; i++)
		{
			var plane = matrix.GetFrustumPlane(i);
			if (plane.normal.Dot(lightDirection) > 0.0f)
			{
				shadowSplitData.SetCullingPlane(shadowSplitData.cullingPlaneCount++, plane);

				if (shadowSplitData.cullingPlaneCount == 10)
					break;
			}
		}

		return shadowSplitData;
	}
}
