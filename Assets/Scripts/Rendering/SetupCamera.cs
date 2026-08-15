using CustomRenderPipeline;
using System;
using UnityEngine;
using UnityEngine.Rendering;
using Unmath;
using System.Collections.Generic;
using Quaternion = Unmath.Quaternion;
using static Unmath.Math;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class SetupCamera : ViewRenderFeature
{
	private readonly Dictionary<int, (Float3, Quaternion, Float4x4)> previousCameraTransform = new();
	private readonly LightingSettings lighting;
	private readonly MorrowindRenderPipelineAsset asset;

	public SetupCamera(RenderGraph renderGraph, LightingSettings lighting, MorrowindRenderPipelineAsset asset) : base(renderGraph)
	{
		this.lighting = lighting;
		this.asset = asset;
	}

	public override void Render(in ReadOnlySpan<ViewParameter> viewParameters, in ViewPassData viewPassData, in DisplayData displayOutputData, ScriptableRenderContext context)
	{
		context.SetupCameraProperties(viewPassData.camera);

		var cullingParameters = viewPassData.cullingParameters;
		cullingParameters.shadowDistance = lighting.DirectionalShadowDistance;
		cullingParameters.cullingOptions = CullingOptions.NeedsLighting | CullingOptions.DisablePerObjectCulling | CullingOptions.ShadowCasters;

		var cullingResults = context.Cull(ref cullingParameters);
		renderGraph.SetResource(new CullingResultsData(cullingResults));

		// Setup globals
		var fogEnabled = RenderSettings.fog;
#if UNITY_EDITOR
		if (SceneView.currentDrawingSceneView != null)
			fogEnabled &= SceneView.currentDrawingSceneView.sceneViewState.fogEnabled;
#endif

		var fogStart = fogEnabled ? RenderSettings.fogStartDistance : 0;
		var fogEnd = fogEnabled ? RenderSettings.fogEndDistance : 0;
		var fogScale = fogEnabled ? 1 / (fogEnd - fogStart) : 0;
		var fogOffset = fogEnabled ? fogStart / (fogStart - fogEnd) : 0;

		var target = asset.FogStartDensity; // value we want the fog to have at halfway between start and end
		var targetAt = Lerp(fogStart, fogEnd, asset.FogAtDensity);
		var fogDensity = -Log2(target) * Rcp(targetAt);

		var environmentDataBuffer = renderGraph.SetConstantBuffer(
		(
			RenderSettings.ambientLight.LinearFloat3(),
			fogScale,
			RenderSettings.fogColor.LinearFloat3(),
			fogOffset,
			Time.time,
			fogStart,
			fogEnd,
			fogDensity
		));

		renderGraph.SetResource(new EnvironmentData(environmentDataBuffer));

		// Screen
		var screenToPixel = Float4x4.Scale(new Float3((Float2)viewPassData.viewSize, 1));
		var pixelToScreen = Float4x4.Scale(new Float3(1 / (Float2)viewPassData.viewSize, 1));

		// Clip
		var clipToScreen = Float4x4.ScaleOffset(new Float3(0.5f, 0.5f, 1), new Float2(0.5f, 0).xxy);
		var clipToScreen1 = Float4x4.ScaleOffset(new Float3(0.5f, -0.5f, 1), new Float2(0.5f, 0).xxy);
		var screenToClip = Float4x4.ScaleOffset(new Float3(2, -2, 1), new Float3(-1, 1, 0));
		var clipToPixel = screenToPixel.Mul(clipToScreen);
		var pixelToClip = screenToClip.Mul(pixelToScreen);

		// View
		var viewToClip = Float4x4.PerspectiveReverseZ(viewPassData.tanHalfFov, viewPassData.near, viewPassData.far, 0);
		var clipToView = Float4x4.PerspectiveReverseZInverse(viewPassData.tanHalfFov, viewPassData.near, viewPassData.far);

		var viewToScreen = clipToScreen.Mul(viewToClip);
		var screenToView = clipToView.Mul(screenToClip);

		var viewToPixel = screenToPixel.Mul(viewToScreen);
		var pixelToView = clipToView.Mul(pixelToClip);

		var viewToWorld = Float4x4.TRS(0.0f, viewPassData.rotation, 1.0f);
		var worldToView = Float4x4.WorldToLocal(0.0f, viewPassData.rotation);

		// World
		var worldToClip = viewToClip.Mul(worldToView);
		var clipToWorld = viewToWorld.Mul(clipToView);

		var worldToScreen = clipToScreen.Mul(worldToClip);
		var screenToWorld = viewToWorld.Mul(screenToView);

		var worldToPixel = screenToPixel.Mul(worldToScreen);
		var pixelToWorld = viewToWorld.Mul(pixelToView);

		// Previous frame matrices
		var viewToNonJitteredScreen = clipToScreen1.Mul(viewToClip);
		if (!previousCameraTransform.TryGetValue(viewPassData.viewId, out var previousTransform))
			previousTransform = (viewPassData.position, viewPassData.rotation, viewToNonJitteredScreen);

		previousCameraTransform[viewPassData.viewId] = (viewPassData.position, viewPassData.rotation, viewToNonJitteredScreen);

		var worldToPreviousView = Float4x4.WorldToLocal(previousTransform.Item1 - viewPassData.position, previousTransform.Item2);
		var worldToPreviousScreen = previousTransform.Item3.Mul(worldToPreviousView);
		var overlayMatrix = Float4x4.Ortho(-Screen.width / 2f, Screen.width / 2f, -Screen.height / 2f, Screen.height / 2f, 0, 1);
		overlayMatrix = GL.GetGPUProjectionMatrix(overlayMatrix, false);

		renderGraph.SetResource(new ViewData(renderGraph.SetConstantBuffer
		((
			worldToClip,
			viewToClip,
			worldToView.r0,
			worldToView.r1,
			worldToView.r2,
			viewToWorld,
			pixelToClip,
			screenToWorld,
			worldToPreviousScreen,
			pixelToWorld,
			overlayMatrix,
			(viewPassData.far - viewPassData.near) * Rcp(viewPassData.near * viewPassData.far), Rcp(viewPassData.far), viewPassData.near, viewPassData.far,
			(Float2)viewPassData.viewSize,
			1.0f / (Float2)viewPassData.viewSize,
			viewPassData.position,
			0f,

			// TODO: Is it even worth calculating these
			new Float3(viewPassData.tanHalfFov * new Float2(-1, -1), 1), 0,
			new Float3(viewPassData.tanHalfFov * new Float2(-1, 3), 1), 0,
			new Float3(viewPassData.tanHalfFov * new Float2(3, -1), 1), 0
		))));
	}
}
