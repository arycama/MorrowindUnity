using System;
using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using Unmath;
using static Unmath.Math;
using Quaternion = Unmath.Quaternion;

public class SetupView : IDisposable
{
	private readonly RenderGraph renderGraph;
	private readonly GraphicsBuffer viewDataBuffer;
	private readonly Dictionary<int, (Float3, Quaternion, Float4x4)> previousCameraTransform = new();

	public SetupView(RenderGraph renderGraph)
	{
		this.renderGraph = renderGraph;
		viewDataBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Constant, 1, UnsafeUtility.SizeOf<ViewDataStruct>());
	}

	public void Dispose()
	{
		viewDataBuffer.Dispose();
	}

	public GraphicsBuffer Render(Camera camera, bool isFlipped = false)
	{
		using (var pass = renderGraph.AddRenderPass("Set ViewData"))
		{
			pass.SetRenderFunction((camera, previousCameraTransform, viewDataBuffer, isFlipped), static (command, data) =>
			{
				var tanHalfFovY = Tan(0.5f * Radians(data.camera.fieldOfView));
				var tanHalfFov = new Float2(tanHalfFovY * data.camera.aspect, tanHalfFovY);
				var viewToWorld = Float4x4.Rotate(data.camera.transform.WorldRotation());
				var worldToView = Float4x4.Rotate(data.camera.transform.WorldRotation().Inverse);
				var viewToClip = Float4x4.PerspectiveReverseZ(tanHalfFov, data.camera.nearClipPlane, data.camera.farClipPlane, 0, data.isFlipped);
				var worldToClip = viewToClip.Mul(worldToView);
				var overlayMatrix = Float4x4.OrthoReverseZ(-Screen.width / 2f, Screen.width / 2f, -Screen.height / 2f, Screen.height / 2f, 0, 1);

				var viewSize = new Int2(data.camera.pixelWidth, data.camera.pixelHeight);
				var near = data.camera.nearClipPlane;
				var far = data.camera.farClipPlane;

				command.SetBufferData(data.viewDataBuffer, stackalloc[]
				{(
					worldToClip,
					viewToClip,
					worldToView,
					viewToWorld,
					overlayMatrix,
					(far - near) * Rcp(near * far), Rcp(far), near, far,
					(Float2)viewSize, 1.0f / (Float2)viewSize,
					data.camera.transform.WorldPosition(), 0f,
					tanHalfFov, 0, 0
				)}.AsArray());
			});
		}

		return viewDataBuffer;
	}

	private struct ViewDataStruct
	{
		public Float4x4 worldToClip;
		public Float4x4 viewToClip;
		public Float4x4 worldToView;
		public Float4x4 viewToWorld;
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
