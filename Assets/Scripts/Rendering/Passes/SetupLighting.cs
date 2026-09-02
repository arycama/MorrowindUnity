using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Pool;
using UnityEngine.Rendering;
using Unmath;
using static Unmath.Math;
using Bounds = Unmath.Bounds;

public class SetupLighting
{
	private readonly NativeList<LightShadowCasterCullingInfo> perLightInfos = new(1, Allocator.Persistent);
	private readonly NativeList<ShadowSplitData> splitBuffer = new(1, Allocator.Persistent);

	private readonly RenderGraph renderGraph;
	private readonly LightingSettings lighting;
	private readonly LightCulling.Settings lightCulling;
	private LightData[] pointLights = new LightData[8];
	private float[] pointLightDepths = new float[8];
	private int[] lightDepthMinMax;

	public SetupLighting(RenderGraph renderGraph, LightingSettings lighting, LightCulling.Settings lightCulling)
	{
		this.renderGraph = renderGraph;
		this.lighting = lighting;
		this.lightCulling = lightCulling;
	}

	public (BufferHandle environmentData, TextureHandle sunShadow, BufferHandle dataBuffer, BufferHandle lightBuffer, BufferHandle lightDepthMinMaxBuffer, TextureHandle visibleLightBits, int pointLightCount, int intersectingPointLightCount, TextureHandle pointShadows) Render(Camera camera, CullingResults cullingResults, ScriptableRenderContext context)
	{
		var tanHalfFovY = Tan(0.5f * Radians(camera.fieldOfView));
		var tanHalfFov = new Float2(tanHalfFovY * camera.aspect, tanHalfFovY);

		var shadowView = renderGraph.AddViewInfo(lighting.DirectionalShadowResolution);
		var sunDirection = camera.transform.WorldRotation().InverseRotate(Float3.Up);
		var sunColor = Float3.One;
		var mainLightIndex = -1;
		var sunShadow = renderGraph.GetTexture(new(shadowView, GraphicsFormat.D16_UNorm, true), Shader.PropertyToID("SunShadow"));
		var viewToSunShadow = Float4x4.Identity;
		var viewPosition = camera.transform.WorldPosition();
		var viewRotation = camera.transform.WorldRotation();
		var viewToWorld = Float4x4.Rotate(viewRotation);
		var near = camera.nearClipPlane;
		var far = camera.farClipPlane;
		var viewSize = new Int2(camera.pixelWidth, camera.pixelHeight);

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
					var faces = ClipFrustumByBounds(tanHalfFov, near, lighting.DirectionalShadowDistance, viewToWorld, relativeSceneBounds);
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
					pass.AddResource<ViewData>();

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
				cullingSphere.xyz = viewRotation.InverseRotate(cullingSphere.xyz - viewPosition);

				// Reject lights that are fully behind the near plane since Unity doesn't do it automatically..
				if (cullingSphere.z + cullingSphere.w <= near)
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
						var matrix = Float4x4.cubemapRotations[j];
						var worldToView = Float4x4.WorldToLocal(matrix.column0, matrix.column1, matrix.column2, position);
						var viewToClip = Float4x4.PerspectiveReverseZ(1, nearPlane, radius);
						var worldToClip = viewToClip.Mul(worldToView);
						var shadowSplitData = CalculateShadowSplitData(worldToClip, matrix.column2, false);

						// Convert to camera relative
						var cameraInverseTranslation = Float4x4.Translate(viewPosition);
						worldToView = worldToView.Mul(cameraInverseTranslation);

						pointShadowRequests.Add(new(i, worldToView, viewToClip, shadowSplitData, j, position, true, nearPlane, radius, position, lightRotation, 1, 1, lighting.PointShadowResolution));
						splitBuffer.Add(shadowSplitData);
					}
				}

				position = viewRotation.InverseRotate(position - viewPosition);

				var shadowProjectionX = 1.0f + radius / (nearPlane - radius);
				var shadowProjectionY = nearPlane * radius / (radius - nearPlane);

				pointLights[pointLightCount] = new(position, distanceScale, forward, angleScale, visibleLight.finalColor.Float3(), angleOffset, cullingSphere, shadowIndex, shadowProjectionX, shadowProjectionY);
				pointLightDepths[pointLightCount] = cullingSphere.z - cullingSphere.w * 1.075f;
				pointLightCount++;
			}

			perLightInfos.Add(new()
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
		var pointShadowView = renderGraph.AddViewInfo(lighting.PointShadowResolution, 1, pointShadowCount);
		var pointShadows = renderGraph.GetTexture(new(pointShadowView, GraphicsFormat.D16_UNorm, true, dimension: TextureDimension.Tex2DArray), Shader.PropertyToID("PointShadows"));

		// TODO: Can we do this in a render pass friendly way
		using (var pass = renderGraph.AddRenderPass("Render Shadows Setup"))
		{
			pass.ViewHandle = pointShadowView;
			pass.DepthStencil = pointShadows;

			pass.SetRenderFunction((renderGraph, pointShadows), static (command, data) =>
			{
				//command.SetRenderTarget(data.renderGraph.GetTextureResource(data.pointShadows), 0, CubemapFace.Unknown, -1);
				//command.ClearRenderTarget(true, false, default);
			});
		}

		for (var i = 0; i < pointShadowRequests.Count; i++)
		{
			using (var pass = renderGraph.AddRenderPass("Render Shadow"))
			{
				pass.ViewHandle = pointShadowView;
				pass.DepthStencil = pointShadows;
				pass.DepthSlice = i;
				var request = pointShadowRequests[i];
				var shadowDrawingSettings = new ShadowDrawingSettings(cullingResults, request.LightIndex);
				var rendererList = context.CreateShadowRendererList(ref shadowDrawingSettings);

				var worldToShadowClip = request.ProjectionMatrix.Mul(request.ViewMatrix);
				pass.SetRenderFunction((worldToShadowClip, rendererList, lighting.PointShadowBias, lighting.PointShadowSlopeBias), static (command, data) =>
				{
					command.SetGlobalDepthBias(data.PointShadowBias, data.PointShadowSlopeBias);
					command.SetGlobalMatrix("WorldToShadowClip", data.worldToShadowClip);
					command.DrawRendererList(data.rendererList);
					command.SetGlobalDepthBias(0.0f, 0.0f);
				});
			}
		}

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

		// Sort lights by view depth
		Array.Sort(pointLightDepths, pointLights);

		Array.Resize(ref lightDepthMinMax, lightCulling.DepthSlices);
		for (var i = 0; i < lightDepthMinMax.Length; i++)
			lightDepthMinMax[i] = BitPack(ushort.MaxValue, 16, 0) | BitPack(0, 16, 16);

		// Add sorted lights to list
		var binWidth = far / lightCulling.DepthSlices;
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
			if (pointLightDepths[i] < near)
				intersectingLightCount = i + 1;
		}

		pointLightCount = Max(1, pointLightCount); // TO avoid buffer size 0 errors
		var tileCountX = DivRoundUp(viewSize.x, lightCulling.TileSize);
		var tileCountY = DivRoundUp(viewSize.y, lightCulling.TileSize);
		var lightIndexCount = DivRoundUp(pointLightCount, 32);

		var lightBuffer = renderGraph.GetBuffer(new(pointLightCount, UnsafeUtility.SizeOf<LightData>()), Shader.PropertyToID("PointLights"));
		var lightDepthMinMaxBuffer = renderGraph.GetBuffer(new(lightCulling.DepthSlices), Shader.PropertyToID("LightDepthMinMax"));
		var tileView = renderGraph.AddViewInfo(new(tileCountX, tileCountY), 1, lightIndexCount);
		var visibleLightBits = renderGraph.GetTexture(new(tileView, GraphicsFormat.R32_UInt, true, dimension: TextureDimension.Tex2DArray), Shader.PropertyToID("VisibleLightBits"));
		var dataBuffer = renderGraph.GetBuffer(new(1, 4 * 8, GraphicsBuffer.Target.Constant), Shader.PropertyToID("PointLightData"));

		using (var pass = renderGraph.AddRenderPass("Set Light Data"))
		{
			pass.ViewHandle = tileView; // TODO: Would be nice to not need to specify this always
			pass.AddUavOutputs(stackalloc ResourceHandle[] { lightBuffer, lightDepthMinMaxBuffer, visibleLightBits, dataBuffer });

			var dataBufferData = stackalloc[]
			{(
				(float)lightCulling.TileSize,
				tileCountX * tileCountY,
				tileCountX,
				lightIndexCount,
				lightCulling.DepthSlices,
				binWidth,
				Rcp(lightCulling.TileSize),
				Rcp(binWidth)
			)}.ToNativeArray();

			pass.SetRenderFunction((pointLights, pointLightCount, lightBuffer, lightDepthMinMaxBuffer, lightDepthMinMax, visibleLightBits, renderGraph, dataBuffer, dataBufferData), static (command, data) =>
			{
				command.SetBufferData(data.renderGraph.GetBufferResource(data.lightBuffer), data.pointLights, 0, 0, data.pointLightCount);
				command.SetBufferData(data.renderGraph.GetBufferResource(data.lightDepthMinMaxBuffer), data.lightDepthMinMax);
				command.SetBufferData(data.renderGraph.GetBufferResource(data.dataBuffer), data.dataBufferData);

				// Clear the light bitmask texture (TODO: Can we do this in another way)
				command.SetRenderTarget(data.renderGraph.GetTextureResource(data.visibleLightBits), 0, CubemapFace.Unknown, -1);
				command.ClearRenderTarget(false, true, default);
			});
		}

		return (environmentData, sunShadow, dataBuffer, lightBuffer, lightDepthMinMaxBuffer, visibleLightBits, pointLightCount, intersectingLightCount, pointShadows);
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
