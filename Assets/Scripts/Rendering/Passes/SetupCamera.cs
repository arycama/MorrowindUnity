using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using Unmath;
using static Unmath.Math;
using Quaternion = Unmath.Quaternion;

public class SetupView
{
	private readonly RenderGraph renderGraph;
	private readonly Dictionary<Camera, (Float3, Quaternion, Float4x4)> previousCameraTransform = new();

	public SetupView(RenderGraph renderGraph)
	{
		this.renderGraph = renderGraph;
	}

	public void Render(Camera camera, bool isFlipped = false, bool updatePrevious = true)
	{
		var viewData = renderGraph.GetBuffer(new(1, UnsafeUtility.SizeOf<ViewDataStruct>(), GraphicsBuffer.Target.Constant), Shader.PropertyToID("ViewData"));
		using (var pass = renderGraph.AddRenderPass("Set ViewData"))
		{
			pass.AddUavOutput(viewData);
			pass.SetRenderFunction((camera, previousCameraTransform, isFlipped, viewData, renderGraph, updatePrevious), static (command, data) =>
			{
				var tanHalfFovY = Tan(0.5f * Radians(data.camera.fieldOfView));
				var tanHalfFov = new Float2(tanHalfFovY * data.camera.aspect, tanHalfFovY);
				var viewSize = new Int2(data.camera.pixelWidth, data.camera.pixelHeight);
				var near = data.camera.nearClipPlane;
				var far = data.camera.farClipPlane;
				var viewPosition = data.camera.transform.WorldPosition();
				var viewRotation = data.camera.transform.WorldRotation();

				// Screen
				var screenToPixel = Float4x4.Scale(new Float3((Float2)viewSize, 1));
				var pixelToScreen = Float4x4.Scale(new Float3(1 / (Float2)viewSize, 1));

				// Clip
				var clipToScreen = Float4x4.ScaleOffset(new Float3(0.5f, -0.5f, 1), new Float2(0.5f, 0).xxy);
				var screenToClip = Float4x4.ScaleOffset(new Float3(2, -2, 1), new Float3(-1, 1, 0));
				var clipToPixel = screenToPixel.Mul(clipToScreen);
				var pixelToClip = screenToClip.Mul(pixelToScreen);

				// View
				var viewToClip = Float4x4.PerspectiveReverseZ(tanHalfFov, near, far, 0, data.isFlipped);
				var clipToView = Float4x4.PerspectiveReverseZInverse(tanHalfFov, near, far);

				var viewToScreen = clipToScreen.Mul(viewToClip);
				var screenToView = clipToView.Mul(screenToClip);

				var viewToPixel = screenToPixel.Mul(viewToScreen);
				var pixelToView = clipToView.Mul(pixelToClip);

				var viewToWorld = Float4x4.Rotate(viewRotation);
				var worldToView = Float4x4.Rotate(viewRotation.Inverse);

				// World
				var worldToClip = viewToClip.Mul(worldToView);
				var clipToWorld = viewToWorld.Mul(clipToView);

				var worldToScreen = clipToScreen.Mul(worldToClip);
				var screenToWorld = viewToWorld.Mul(screenToView);

				var worldToPixel = screenToPixel.Mul(worldToScreen);
				var pixelToWorld = viewToWorld.Mul(pixelToView);

				//var overlayMatrix = Float4x4.Ortho(-Screen.width / 2f, Screen.width / 2f, -Screen.height / 2f, Screen.height / 2f, near, far);
				var overlayMatrix = Matrix4x4.Ortho(0, Screen.width, Screen.height, 0, 0, 1);

				var viewToNonJitteredScreen = clipToScreen.Mul(viewToClip);
				if (!data.previousCameraTransform.TryGetValue(data.camera, out var previousTransform))
					previousTransform = (viewPosition, viewRotation, viewToNonJitteredScreen);

				if (data.updatePrevious)
					data.previousCameraTransform[data.camera] = (viewPosition, viewRotation, viewToNonJitteredScreen);

				var worldToPreviousView = Float4x4.WorldToLocal(previousTransform.Item1 - viewPosition, previousTransform.Item2);
				var worldToPreviousScreen = previousTransform.Item3.Mul(worldToPreviousView);

				var buffer = data.renderGraph.GetBufferResource(data.viewData);
				command.SetBufferData(buffer, stackalloc[]
				{(
					worldToClip,
					viewToClip,
					worldToView,
					viewToWorld,
					pixelToWorld,
					screenToWorld,
					worldToPreviousScreen,
					overlayMatrix,
					(far - near) * Rcp(near * far), Rcp(far), near, far,
					(Float2)viewSize, 1.0f / (Float2)viewSize,
					data.camera.transform.WorldPosition(), 0f,
					tanHalfFov, 0, 0
				)}.AsArray());
			});
		}

		if (isFlipped)
			renderGraph.SetResource(new ViewDataFlipped(viewData));
		else
			renderGraph.SetResource(new ViewData(viewData));
	}

	private struct ViewDataStruct
	{
		public Float4x4 worldToClip;
		public Float4x4 viewToClip;
		public Float4x4 worldToView;
		public Float4x4 viewToWorld;
		public Float4x4 pixelToWorld;
		public Float4x4 screenToWorld;
		public Float4x4 worldToPreviousScreen;
		public Float4x4 overlayMatrix;
		public float Item5;
		public float Item6;
		public float near;
		public float far;
		public Float2 Item9;
		public Float2 Item10;
		public Float3 Item11;
		public float Item12;
		public Float2 tanHalfFov;
		public int Item14;
		public int Item15;
	}
}
;