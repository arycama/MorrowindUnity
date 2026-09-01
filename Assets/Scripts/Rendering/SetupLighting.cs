using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Unmath;
using static Unmath.Math;
using Bounds = Unmath.Bounds;

public class SetupLighting
{
	private readonly RenderGraph renderGraph;
	private readonly LightingSettings lighting;

	public SetupLighting(RenderGraph renderGraph, LightingSettings lighting)
	{
		this.renderGraph = renderGraph;
		this.lighting = lighting;
	}

	public (BufferHandle environmentData, TextureHandle sunShadow) Render(Camera camera, CullingResults cullingResults, ScriptableRenderContext context, BufferHandle viewData)
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
		var viewPosition = camera.transform.WorldPosition();
		var viewRotation = camera.transform.WorldRotation();
		var viewToWorld = Float4x4.Rotate(viewRotation);

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
			var viewSpaceLightRotation = viewRotation.InverseRotate(lightRotation);
			var hasShadows = visibleLight.light.shadows != LightShadows.None;

			if (visibleLight.lightType == LightType.Directional && mainLightIndex == -1)
			{
				mainLightIndex = i;
				sunDirection = -viewSpaceLightRotation.Forward;
				sunColor = lightColor;

				// TODO: Constrain cascade bounds to shadow caster bounds
				if (hasShadows && cullingResults.GetShadowCasterBounds(mainLightIndex, out var sceneBounds))
				{
					var relativeSceneBounds = new Bounds(sceneBounds.center - viewPosition, sceneBounds.extents);
					var faces = ClipFrustumByBounds(tanHalfFov, camera.nearClipPlane, lighting.DirectionalShadowDistance, viewToWorld, relativeSceneBounds);
					var worldToLight = Float4x4.Rotate(lightRotation.Inverse);

					Bounds bounds = default;
					for (var j = 0; j < faces.Count; j++)
					{
						var face = faces[j];
						for (var k = 0; k < face.points.Count; k++)
						{
							var lightPoint = worldToLight.MultiplyPoint3x4(face.points[k]);
							bounds = j == 0 ? new Bounds(lightPoint, Float3.Zero) : bounds.Encapsulate(lightPoint);
						}
					}

					var worldToLightClip = Float4x4.OrthoReverseZ(bounds).Mul(worldToLight);
					var viewToLight = Float4x4.Rotate(lightRotation.InverseRotate(viewRotation));
					viewToSunShadow = Float4x4.OrthoReverseZSample(bounds).Mul(viewToLight);

					var shadowSplitData = CalculateShadowSplitData(worldToLightClip, lightDirection, true);
					shadowSplitData.shadowCascadeBlendCullingFactor = 1;
					splitRange = new RangeInt(splitBuffer.Length, 1);
					splitBuffer.Add(in shadowSplitData);

					var shadowDrawingSettings = new ShadowDrawingSettings(cullingResults, i);
					var rendererList = context.CreateShadowRendererList(ref shadowDrawingSettings);

					using var pass = renderGraph.AddRenderPass("Directional Shadows");
					pass.ViewHandle = shadowView;
					pass.DepthStencil = sunShadow;
					pass.AddResource(viewData);

					pass.SetRenderFunction((rendererList, worldToLightClip, lighting), (command, data) =>
					{
						command.SetGlobalDepthBias(data.lighting.DirectionalShadowBias, data.lighting.DirectionalShadowSlopeBias);
						command.SetGlobalInt("ZClip", 0);
						command.SetGlobalMatrix("WorldToShadowClip", data.worldToLightClip);
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

		var environmentData = renderGraph.GetBuffer(new(1, UnsafeUtility.SizeOf<EnvironmentDataStruct>(), GraphicsBuffer.Target.Constant), Shader.PropertyToID("EnvironmentData"));
		using (var pass = renderGraph.AddRenderPass("Set EnvironmentData"))
		{
			pass.AddUavOutput(environmentData);
			pass.SetRenderFunction((sunDirection, sunColor, fogEnabled, environmentData, lighting, viewToSunShadow, renderGraph), static (command, data) =>
			{
				var fogStart = data.fogEnabled ? RenderSettings.fogStartDistance : 0;
				var fogEnd = data.fogEnabled ? RenderSettings.fogEndDistance : 0;
				var fogScale = data.fogEnabled ? 1 / (fogEnd - fogStart) : 0;
				var fogOffset = data.fogEnabled ? fogStart / (fogStart - fogEnd) : 0;
				var sunShadowFadeScale = -1.0f / data.lighting.DirectionalFadeLength;
				var sunShadowFadeOffset = data.lighting.DirectionalShadowDistance / data.lighting.DirectionalFadeLength;

				var buffer = data.renderGraph.GetBufferResource(data.environmentData);
				command.SetBufferData(buffer, stackalloc[]
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

	// ordered (CCW when viewed from outside) coplanar list of points
	public struct Polygon
	{
		public List<Float3> points;
		public Polygon(List<Float3> pts) { points = pts; }
	}

	// Clip the frustum polygons against a Bounds. Returns the polygons of
	public static List<Polygon> ClipFrustumByBounds(Float2 tanHalfFov, float near, float far, Float4x4 viewToWorld, Bounds bounds, float eps = 1e-4f)
	{
		Float3 c(int i) => viewToWorld.MultiplyPoint3x4(Geometry.GetFrustumCorner(tanHalfFov, near, far, (FrustumCorner)i));

		// c: 0=NBL 1=NTL 2=NTR 3=NBR  4=FBL 5=FTL 6=FTR 7=FBR
		var polygons = new List<Polygon>
		{
			new(new List<Float3> { c(0), c(1), c(5), c(4) }), // left
			new(new List<Float3> { c(3), c(2), c(6), c(7) }), // right
			new(new List<Float3> { c(0), c(3), c(7), c(4) }), // bottom
			new(new List<Float3> { c(1), c(5), c(6), c(2) }), // top
			new(new List<Float3> { c(0), c(1), c(2), c(3) }), // near
			new(new List<Float3> { c(6), c(5), c(4), c(7) }), // far  (reversed)
		};

		polygons = ClipByPlane(polygons, p => p.x - bounds.Min.x, Float3.Right, eps);
		polygons = ClipByPlane(polygons, p => bounds.Max.x - p.x, Float3.Left, eps);
		polygons = ClipByPlane(polygons, p => p.y - bounds.Min.y, Float3.Up, eps);
		polygons = ClipByPlane(polygons, p => bounds.Max.y - p.y, Float3.Down, eps);
		polygons = ClipByPlane(polygons, p => p.z - bounds.Min.z, Float3.Forward, eps);
		polygons = ClipByPlane(polygons, p => bounds.Max.z - p.z, Float3.Back, eps);

		return polygons;
	}

	// Cuts every polygon by one half-space (signedDist(p) >= 0 keeps p), and stitches a new cap polygon out of the intersection points.
	private static List<Polygon> ClipByPlane(List<Polygon> polygons, Func<Float3, float> signedDist, Float3 planeNormal, float eps)
	{
		var result = new List<Polygon>();
		var capPoints = new List<Float3>();

		foreach (var f in polygons)
		{
			var clipped = ClipPolygon(f.points, signedDist, capPoints, eps);
			if (clipped.Count >= 3)
				result.Add(new Polygon(clipped));
		}

		if (capPoints.Count >= 3)
		{
			var cap = SortCoplanarPointsCCW(capPoints, planeNormal, eps);
			if (cap.Count >= 3)
				result.Add(new Polygon(cap));
		}

		return result;
	}

	private static List<Float3> ClipPolygon(List<Float3> poly, Func<Float3, float> signedDist, List<Float3> planePoints, float eps)
	{
		var outPts = new List<Float3>();
		var n = poly.Count;
		for (var i = 0; i < n; i++)
		{
			var cur = poly[i];
			var next = poly[(i + 1) % n];
			var dCur = signedDist(cur);
			var dNext = signedDist(next);

			if (dCur >= -eps)
			{
				outPts.Add(cur);
				if (Mathf.Abs(dCur) < eps) planePoints.Add(cur);
			}

			if (dCur > eps && dNext < -eps || dCur < -eps && dNext > eps)
			{
				var t = dCur / (dCur - dNext);
				var ip = Float3.Lerp(cur, next, t);
				outPts.Add(ip);
				planePoints.Add(ip);
			}
		}

		return outPts;
	}

	private static List<Float3> SortCoplanarPointsCCW(List<Float3> pts, Float3 normal, float eps)
	{
		// Dedupe
		var unique = new List<Float3>();
		foreach (var p in pts)
		{
			var dup = false;
			foreach (var u in unique)
			{
				if ((u - p).SquareMagnitude < eps * eps)
				{
					dup = true;
					break;
				}
			}

			if (!dup)
				unique.Add(p);
		}

		if (unique.Count < 3)
			return unique;

		var centroid = Float3.Zero;
		foreach (var p in unique)
			centroid += p;

		centroid /= unique.Count;

		var tangent = normal.Cross(Float3.Up);
		if (tangent.SquareMagnitude < 1e-6f)
			tangent = normal.Cross(Float3.Right);
		tangent = tangent.Normalized;

		var bitangent = normal.Cross(tangent);

		unique.Sort((a, b) =>
		{
			var angA = Mathf.Atan2((a - centroid).Dot(bitangent), (a - centroid).Dot(tangent));
			var angB = Mathf.Atan2((b - centroid).Dot(bitangent), (b - centroid).Dot(tangent));
			return angA.CompareTo(angB);
		});

		return unique;
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
