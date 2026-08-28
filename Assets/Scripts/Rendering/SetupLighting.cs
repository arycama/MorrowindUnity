using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Unmath;
using static Unmath.Math;

public class SetupLighting : IDisposable
{
	private readonly RenderGraph renderGraph;
	private readonly LightingSettings lighting;
	private readonly GraphicsBuffer environmentData;

	public SetupLighting(RenderGraph renderGraph, LightingSettings lighting)
	{
		this.renderGraph = renderGraph;
		this.lighting = lighting;
		environmentData = new GraphicsBuffer(GraphicsBuffer.Target.Constant, 1, UnsafeUtility.SizeOf<EnvironmentDataStruct>());
	}

	public void Dispose()
	{
		environmentData.Dispose();
	}

	public (GraphicsBuffer environmentData, TextureHandle sunShadow) Render(Camera camera, CullingResults cullingResults, ScriptableRenderContext context, GraphicsBuffer viewDataBuffer)
	{
		var tanHalfFovY = Tan(0.5f * Radians(camera.fieldOfView));
		var tanHalfFov = new Float2(tanHalfFovY * camera.aspect, tanHalfFovY);

		var shadowView = renderGraph.AddViewInfo(lighting.DirectionalShadowResolution);
		var sunDirection = camera.transform.WorldRotation().InverseRotate(Float3.Up);
		var sunColor = Float3.One;
		var mainLightIndex = -1;
		var sunShadow = renderGraph.GetTexture(new(shadowView, GraphicsFormat.D16_UNorm, true), Shader.PropertyToID("SunShadow"));
		var viewToSunShadow = Float4x4.Identity;
		var lightCount = cullingResults.visibleLights.Length;

		var perLightInfos = new NativeArray<LightShadowCasterCullingInfo>(lightCount, Allocator.Temp);
		var splitBuffer = new NativeList<ShadowSplitData>(Allocator.Temp);

		for (var i = 0; i < lightCount; i++)
		{
			var visibleLight = cullingResults.visibleLights[i];
			var lightToWorld = (Float4x4)visibleLight.localToWorldMatrix;
			var lightColor = visibleLight.finalColor.Float3();
			var lightDirection = -lightToWorld.Forward;
			var splitRange = new RangeInt(0, 0);
			var lightRotation = lightToWorld.Rotation;
			var viewSpaceLightRotation = camera.transform.WorldRotation().InverseRotate(lightRotation);
			var hasShadows = visibleLight.light.shadows != LightShadows.None;

			if (visibleLight.lightType == LightType.Directional && mainLightIndex == -1)
			{
				mainLightIndex = i;
				sunDirection = -viewSpaceLightRotation.Forward;
				sunColor = lightColor;

				// TODO: Constrain cascade bounds to shadow caster bounds
				if (hasShadows && cullingResults.GetShadowCasterBounds(mainLightIndex, out _))
				{
					// Transform from view space to light space
					var viewToLight = Float4x4.Rotate(viewSpaceLightRotation.Inverse);
					var viewSpaceLightBounds = Geometry.GetFrustumBounds(tanHalfFov, camera.nearClipPlane, lighting.DirectionalShadowDistance, viewToLight);

					// Matrix that goes from world space to light space
					var worldToLight = Float4x4.Rotate(lightRotation.Inverse);
					var worldToLightClip = Float4x4.OrthoReverseZ(viewSpaceLightBounds).Mul(worldToLight);

					var shadowSplitData = CalculateShadowSplitData(worldToLightClip, lightDirection, true);
					shadowSplitData.shadowCascadeBlendCullingFactor = 1;
					splitRange = new RangeInt(splitBuffer.Length, 1);
					splitBuffer.Add(in shadowSplitData);

					// Matrix that converts from view space to shadow-sampling space
					viewToSunShadow = Float4x4.OrthoReverseZSample(viewSpaceLightBounds).Mul(viewToLight);

					var shadowDrawingSettings = new ShadowDrawingSettings(cullingResults, i);
					var rendererList = context.CreateShadowRendererList(ref shadowDrawingSettings);

					using var pass = renderGraph.AddRenderPass("Directional Shadows");
					pass.ViewHandle = shadowView;
					pass.AddOutput(sunShadow);
					pass.SetRenderFunction((rendererList, worldToLightClip, lighting, viewDataBuffer), (command, data) =>
					{
						command.SetGlobalDepthBias(data.lighting.DirectionalShadowBias, data.lighting.DirectionalShadowSlopeBias);
						command.SetGlobalInt("ZClip", 0);
						command.SetGlobalMatrix("WorldToShadowClip", data.worldToLightClip);
						command.SetGlobalConstantBuffer(data.viewDataBuffer, Shader.PropertyToID("ViewData"), 0, data.viewDataBuffer.stride);
						command.DrawRendererList(rendererList);
						command.SetGlobalDepthBias(0.0f, 0.0f);
						command.SetGlobalInt("ZClip", 1);
					});
				}
			}

			perLightInfos[i] = new LightShadowCasterCullingInfo
			{
				projectionType = visibleLight.lightType == LightType.Directional ? BatchCullingProjectionType.Orthographic : BatchCullingProjectionType.Perspective,
				splitExclusionMask = 0,
				splitRange = splitRange
			};
		}

		context.CullShadowCasters(cullingResults, new ShadowCastersCullingInfos
		{
			perLightInfos = perLightInfos,
			splitBuffer = splitBuffer.AsArray()
		});

		var fogEnabled = RenderSettings.fog;
#if UNITY_EDITOR
		if (SceneView.currentDrawingSceneView != null)
			fogEnabled &= SceneView.currentDrawingSceneView.sceneViewState.fogEnabled;
#endif

		using (var pass = renderGraph.AddRenderPass("Set EnvironmentData"))
		{
			pass.SetRenderFunction((sunDirection, sunColor, fogEnabled, environmentData, lighting, viewToSunShadow), static (command, data) =>
			{
				var fogStart = data.fogEnabled ? RenderSettings.fogStartDistance : 0;
				var fogEnd = data.fogEnabled ? RenderSettings.fogEndDistance : 0;
				var fogScale = data.fogEnabled ? 1 / (fogEnd - fogStart) : 0;
				var fogOffset = data.fogEnabled ? fogStart / (fogStart - fogEnd) : 0;
				var sunShadowFadeScale = -1.0f / data.lighting.DirectionalFadeLength;
				var sunShadowFadeOffset = data.lighting.DirectionalShadowDistance / data.lighting.DirectionalFadeLength;

				command.SetBufferData(data.environmentData, stackalloc[]
				{(
					RenderSettings.ambientLight.LinearFloat3(), fogScale,
					RenderSettings.fogColor.LinearFloat3(), fogOffset,
					Time.time, fogStart, fogEnd, 0,
					data.sunDirection, sunShadowFadeScale,
					data.sunColor, sunShadowFadeOffset,
					data.viewToSunShadow
				)}.AsArray());
			});
		}

		return (environmentData, sunShadow);
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

	private struct EnvironmentDataStruct
	{
		public Float3 Item1;
		public float fogScale;
		public Float3 Item3;
		public float fogOffset;
		public float time;
		public float fogStart;
		public float fogEnd;
		public int Item8;
		public Float3 sunDirection;
		public int Item10;
		public Float3 sunColor;
		public int Item12;
		public Float4x4 item13;
	}
}
