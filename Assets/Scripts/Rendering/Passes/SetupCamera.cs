using System.Collections.Generic;
using UnityEngine;
using Unmath;
using static Unmath.Math;
using Quaternion = Unmath.Quaternion;

public class SetupView
{
	private readonly RenderGraph renderGraph;
	private readonly Dictionary<Camera, (Float3 position, Quaternion rotation, Float4x4 viewToScreen)> previousCameraTransform = new();

	public SetupView(RenderGraph renderGraph)
	{
		this.renderGraph = renderGraph;
	}

	public void Render(Camera camera, bool isFlipped = false, bool updatePrevious = true)
	{
		using var buffer = renderGraph.AddConstantBuffer("ViewData", out var viewData);

		var tanHalfFovY = Tan(0.5f * Radians(camera.fieldOfView));
		var tanHalfFov = new Float2(tanHalfFovY * camera.aspect, tanHalfFovY);
		var viewSize = new Int2(camera.pixelWidth, camera.pixelHeight);
		var near = camera.nearClipPlane;
		var far = camera.farClipPlane;
		var viewPosition = camera.transform.WorldPosition();
		var viewRotation = camera.transform.WorldRotation();

		var clipToScreen = Float4x4.ScaleOffset(new Float3(0.5f, -0.5f, 1), new Float2(0.5f, 0).xxy);
		var viewToClip = Float4x4.PerspectiveReverseZ(tanHalfFov, near, far, 0, isFlipped);
		buffer.AddData(viewToClip);

		var viewToScreen = clipToScreen.Mul(viewToClip);
		if (!previousCameraTransform.TryGetValue(camera, out var previousTransform))
			previousTransform = (viewPosition, viewRotation, viewToScreen);

		if (updatePrevious)
			previousCameraTransform[camera] = (viewPosition, viewRotation, viewToScreen);

		var worldToPreviousView = Float4x4.WorldToLocal(previousTransform.position - viewPosition, previousTransform.rotation);
		var worldToPreviousScreen = previousTransform.viewToScreen.Mul(worldToPreviousView);
		buffer.AddData(worldToPreviousScreen);

		var worldToView = Float4x4.Rotate(viewRotation.Inverse);
		buffer.AddData(worldToView);

		var worldToClip = viewToClip.Mul(worldToView);
		buffer.AddData(worldToClip);

		var pixelToScreen = Float4x4.Scale(new Float3(1 / (Float2)viewSize, 1));
		var screenToClip = Float4x4.ScaleOffset(new Float3(2, -2, 1), new Float3(-1, 1, 0));
		var clipToView = Float4x4.PerspectiveReverseZInverse(tanHalfFov, near, far);
		var viewToWorld = Float4x4.Rotate(viewRotation);
		buffer.AddData(viewToWorld);

		var pixelToClip = screenToClip.Mul(pixelToScreen);

		var pixelToView = clipToView.Mul(pixelToClip);
		var screenToView = clipToView.Mul(screenToClip);
		var screenToWorld = viewToWorld.Mul(screenToView);
		buffer.AddData(screenToWorld);

		var pixelToWorld = viewToWorld.Mul(pixelToView);
		buffer.AddData(pixelToWorld);

		buffer.AddData(((far - near) * Rcp(near * far), Rcp(far), near, far));
		buffer.AddData(((Float2)viewSize, 1.0f / (Float2)viewSize));
		buffer.AddData((camera.transform.WorldPosition(), 0f));
		buffer.AddData((tanHalfFov, 0, 0));

		if (isFlipped)
			renderGraph.SetResource(new ViewDataFlipped(viewData));
		else
			renderGraph.SetResource(new ViewData(viewData));
	}
}