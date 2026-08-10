using CustomRenderPipeline;
using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Unmath;
using static Unmath.Math;

public class SetupLighting : ViewRenderFeature
{
	private readonly NativeList<LightShadowCasterCullingInfo> perLightInfos = new(1, Allocator.Persistent);
	private readonly NativeList<ShadowSplitData> splitBuffer = new(1, Allocator.Persistent);

	private LightData[] pointLights = new LightData[8];
	private float[] pointLightDepths = new float[8];
	private int[] lightDepthMinMax;

	private readonly LightingSettings lighting;
	private readonly LightCulling.Settings lightCulling;

	public SetupLighting(RenderGraph renderGraph, LightingSettings lighting, LightCulling.Settings lightCulling) : base(renderGraph)
	{
		this.lighting = lighting;
		this.lightCulling = lightCulling;
	}

	public override void Render(in ReadOnlySpan<ViewParameter> viewParameters, in ViewPassData viewPassData, in DisplayData displayOutputData, ScriptableRenderContext context)
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

					var viewBounds = Geometry.GetFrustumBounds(viewPassData.tanHalfFov, viewPassData.near, lighting.DirectionalShadowDistance, cameraToView);
					var viewToClip = Float4x4.OrthoReverseZ(-viewBounds.extents.x, viewBounds.extents.x, -viewBounds.extents.y, viewBounds.extents.y, 0, viewBounds.Size.z);

					var worldViewPosition = viewBounds.center;
					worldViewPosition.z = viewBounds.Min.z;
					worldViewPosition = lightRotation.Rotate(worldViewPosition);

					var worldToCascade = Float4x4.WorldToLocal(worldViewPosition, lightRotation);
					worldToSunShadow = Float4x4.OrthoReverseZSample(-viewBounds.extents.x, viewBounds.extents.x, -viewBounds.extents.y, viewBounds.extents.y, 0, viewBounds.Size.z).Mul(worldToCascade);

					sunShadows = renderGraph.GetTexture(lighting.DirectionalShadowResolution, GraphicsFormat.D16_UNorm, isExactSize: true, clear: true);

					var shadowSplitData = CalculateShadowSplitData(viewToClip.Mul(worldToCascade), lightDirection, true);
					shadowSplitData.shadowCascadeBlendCullingFactor = 1;
					splitBuffer.Add(shadowSplitData);

					splitRange = new RangeInt(perLightInfos.Length, 1);
					sunShadowsEnabled = true;

					var perCascadeData = renderGraph.SetConstantBuffer(viewToClip.Mul(worldToCascade));
					using var pass = renderGraph.AddShadowRenderPass("Directional Shadows");
					pass.Initialize(context, cullingResults, mainLightIndex, lighting.DirectionalShadowBias, lighting.DirectionalShadowSlopeBias, false, false, lighting.DirectionalShadowResolution, 1);
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

				pointLights[pointLightCount] = new(position, distanceScale, forward, angleScale, visibleLight.finalColor.Float3(), angleOffset, cullingSphere);
				pointLightDepths[pointLightCount] = cullingSphere.z - cullingSphere.w * 1.075f;
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

		var sunShadowFadeScale = -1.0f / lighting.DirectionalFadeLength;
		var sunShadowFadeOffset = lighting.DirectionalShadowDistance / lighting.DirectionalFadeLength;

		var lightingDataBuffer = renderGraph.SetConstantBuffer
		((
			sunDirection,
			sunShadowFadeScale,

			sunColor,
			sunShadowFadeOffset,

			worldToSunShadow,

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

		renderGraph.SetResource(new PointLightData(pointLightData, pointLightBuffer, pointLightCount, lightDepthMinMaxBuffer, visibleLightBits, intersectingLightCount));
	}

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
