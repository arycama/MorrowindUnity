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
	private readonly MorrowindRenderPipelineAsset asset;

	public SetupCamera(RenderGraph renderGraph, MorrowindRenderPipelineAsset asset) : base(renderGraph)
	{
		this.asset = asset;
	}

	public override void Render(in ReadOnlySpan<ViewParameter> viewParameters, in ViewPassData viewPassData, in DisplayData displayOutputData, ScriptableRenderContext context)
	{
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

		var fogStart = fogEnabled ? RenderSettings.fogStartDistance : 0;
		var fogEnd = fogEnabled ? RenderSettings.fogEndDistance : 0;
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

		var isFlipped = viewPassData.isFlipped;

		var rotation = viewPassData.rotation;
		var corner0 = isFlipped ? new Float2(-1, 1) : new Float2(-1, -1);
		var corner1 = isFlipped ? new Float2(-1, -3) : new Float2(-1, 3);
		var corner2 = isFlipped ? new Float2(3, 1) : new Float2(3, -1);
		var tanHalfFov = viewPassData.tanHalfFov;

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
			0f,
			rotation.Rotate(new Float3(tanHalfFov * corner0, 1)), 0,
			rotation.Rotate(new Float3(tanHalfFov * corner1, 1)), 0,
			rotation.Rotate(new Float3(tanHalfFov * corner2, 1)), 0
		))));
	}
}
