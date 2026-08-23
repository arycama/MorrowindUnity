using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Unmath;
using static Unmath.Math;

public class NewPipeline : RenderPipelineBase
{
	private readonly NewPipelineAsset asset;
	private readonly Material blitMaterial;

	public NewPipeline(NewPipelineAsset asset) 
	{
		this.asset = asset;
		blitMaterial = new Material(Shader.Find("Hidden/Blit Material")) { hideFlags = HideFlags.HideAndDontSave };
	}

	protected override void RenderCamera(Camera camera, ScriptableCullingParameters cullingParameters, ScriptableRenderContext context)
	{
		cullingParameters.cullingOptions = CullingOptions.DisablePerObjectCulling;
		var cullingResults = context.Cull(ref cullingParameters);

		var fogEnabled = RenderSettings.fog;
#if UNITY_EDITOR
		if (SceneView.currentDrawingSceneView != null)
			fogEnabled &= SceneView.currentDrawingSceneView.sceneViewState.fogEnabled;
#endif

		var setViewData = renderGraph.AddRenderPass("Set View Data", false, (camera, fogEnabled), static (command, data) =>
		{
			var tanHalfFovY = Tan(0.5f * Radians(data.camera.fieldOfView));
			var tanHalfFov = new Float2(tanHalfFovY * data.camera.aspect, tanHalfFovY);
			var worldToView = Float4x4.WorldToLocal(0.0f, data.camera.transform.WorldRotation());
			var viewToClip = Float4x4.PerspectiveReverseZ(tanHalfFov, data.camera.nearClipPlane, data.camera.farClipPlane);
			var worldToClip = viewToClip.Mul(worldToView);

			command.SetGlobalMatrix("WorldToView", worldToView);
			command.SetGlobalMatrix("WorldToClip", worldToClip);
			command.SetGlobalVector("ViewPosition", data.camera.transform.position);
			command.SetGlobalVector("SunDirection", data.camera.transform.WorldRotation().InverseRotate(-RenderSettings.sun.transform.forward));
			command.SetGlobalVector("SunColor", RenderSettings.sun.color.linear);
			command.SetGlobalVector("AmbientLight", RenderSettings.ambientLight.linear);
			command.SetGlobalVector("FogColor", RenderSettings.fogColor.linear);
			command.SetGlobalFloat("FogScale", data.fogEnabled ? 1 / (RenderSettings.fogEndDistance - RenderSettings.fogStartDistance) : 0);
			command.SetGlobalFloat("FogOffset", data.fogEnabled ? RenderSettings.fogStartDistance / (RenderSettings.fogStartDistance - RenderSettings.fogEndDistance) : 0);
			command.SetGlobalVector("ViewSize", new(data.camera.pixelWidth, data.camera.pixelHeight));
		});

		var depthFormat = camera.targetTexture == null ? GraphicsFormat.D32_SFloat_S8_UInt : camera.targetTexture.depthStencilFormat;
		var cameraDepth = renderGraph.GetTexture(new(new(camera.pixelWidth, camera.pixelHeight), depthFormat, asset.Samples, true), true);

		// TODO: This should also account for HDR
		var backbufferFormat = QualitySettings.activeColorSpace == ColorSpace.Linear ? GraphicsFormat.R8G8B8A8_SRGB : GraphicsFormat.R8G8B8A8_UNorm;
		var targetFormat = camera.targetTexture == null ? backbufferFormat : camera.targetTexture.graphicsFormat;
		var cameraColor = renderGraph.GetTexture(new(new(camera.pixelWidth, camera.pixelHeight), targetFormat, asset.Samples, true, RenderSettings.fogColor.linear), false);

		// This is only required if the camera is rendering to a no-resolved MSAA texture, which is the case for depth in the scene view..
		var invertCulling = asset.Samples > 1 && camera.targetTexture == null;

		var opaqueRendererList = context.CreateRendererList(new(new ShaderTagId("Forward"), cullingResults, camera) { renderQueueRange = RenderQueueRange.opaque, sortingCriteria = SortingCriteria.CommonOpaque });
		var renderForwardOpaque = renderGraph.AddRenderPass("Render Forward Opaque", invertCulling, opaqueRendererList, static (command, opaqueRendererList) =>
		{
			command.DrawRendererList(opaqueRendererList);
		});
		{
			renderForwardOpaque.SetRenderPassParams(new(camera.pixelWidth, camera.pixelHeight), asset.Samples);
			renderForwardOpaque.WriteTexture(cameraDepth, true);
			renderForwardOpaque.WriteTexture(cameraColor, false);
		}

		var transparentRendererList = context.CreateRendererList(new(new ShaderTagId("Forward"), cullingResults, camera) { renderQueueRange = RenderQueueRange.transparent, sortingCriteria = SortingCriteria.CommonTransparent });
		var renderForwardTransparent = renderGraph.AddRenderPass("Render Forward Transparent", invertCulling, (transparentRendererList, invertCulling), static (command, data) =>
		{
			command.DrawRendererList(data.transparentRendererList);
		});
		{
			renderForwardTransparent.SetRenderPassParams(new(camera.pixelWidth, camera.pixelHeight), asset.Samples);
			renderForwardTransparent.WriteTexture(cameraDepth, true);
			renderForwardTransparent.WriteTexture(cameraColor, false);
		}

		// Can only render directly to backbuffer if there is no msaa samples and there is no target texture
		// TODO: Check for hardware msaa backbuffer resolve support
		var renderToBackbuffer = asset.Samples == 1 && camera.targetTexture == null;
		if (renderToBackbuffer)
		{
			renderGraph.ExportTexture(cameraColor, BuiltinRenderTextureType.CameraTarget);
		}
		else
		{
			var requiresSceneDepth = camera.cameraType == CameraType.SceneView;
			var finalBlitSetup = renderGraph.AddRenderPass("Final Blit Setup", false, (camera, asset, requiresSceneDepth), static (command, data) =>
			{
				if (data.camera.targetTexture == null)
					command.EnableShaderKeyword("FLIP");

				if (data.requiresSceneDepth)
				{
					switch (data.asset.Samples)
					{
						case 1:
							command.EnableShaderKeyword("DEPTH");
							break;
						case 2:
							command.EnableShaderKeyword("DEPTH_MSAA_2");
							break;
						case 4:
							command.EnableShaderKeyword("DEPTH_MSAA_4");
							break;
						case 8:
							command.EnableShaderKeyword("DEPTH_MSAA_8");
							break;
					}
				}

				command.SetWireframe(false);
			});

			// Final blit/resolve if needed
			var finalBlitPass = renderGraph.AddRenderPass("Final Blit", false, (blitMaterial, camera, asset, requiresSceneDepth), static (command, data) =>
			{
				command.DrawProcedural(Matrix4x4.identity, data.blitMaterial, 0, MeshTopology.Triangles, 3);

				if (data.camera.targetTexture == null)
					command.DisableShaderKeyword("FLIP");

				if (data.requiresSceneDepth)
				{
					switch (data.asset.Samples)
					{
						case 1:
							command.DisableShaderKeyword("DEPTH");
							break;
						case 2:
							command.DisableShaderKeyword("DEPTH_MSAA_2");
							break;
						case 4:
							command.DisableShaderKeyword("DEPTH_MSAA_4");
							break;
						case 8:
							command.DisableShaderKeyword("DEPTH_MSAA_8");
							break;
					}
				}
			});
			{
				finalBlitPass.ReadTexture(cameraColor, Shader.PropertyToID("CameraColor"));
				var backbufferColor = renderGraph.GetTexture(new(new(camera.pixelWidth, camera.pixelHeight), targetFormat), false);
				finalBlitPass.WriteTexture(backbufferColor, false);
				renderGraph.ExportTexture(backbufferColor, camera.targetTexture == null ? BuiltinRenderTextureType.CameraTarget : camera.targetTexture);

				// For sceneView, take the first depth sample for for gizmos, wireframe, etc.
				if (requiresSceneDepth)
				{
					finalBlitPass.ReadTexture(cameraDepth, Shader.PropertyToID("CameraDepth"));

					var sceneDepth = renderGraph.GetTexture(new(new(camera.pixelWidth, camera.pixelHeight), depthFormat), false);
					finalBlitPass.WriteTexture(sceneDepth, false);
					renderGraph.ExportTexture(sceneDepth, camera.targetTexture);
				}

				finalBlitPass.SetRenderPassParams(new(camera.pixelWidth, camera.pixelHeight), 1);
			}
		}

		// Render UI. For now this is done automatically so it only sets matrices for UI rendering
		renderGraph.AddRenderPass("Set UI Matrices", false, 0, static (command, data) =>
		{
			var overlayMatrix = Float4x4.OrthoReverseZ(-Screen.width / 2f, Screen.width / 2f, -Screen.height / 2f, Screen.height / 2f, 0, 1);
			command.SetGlobalMatrix("UiOverlayMatrix", overlayMatrix);
		});
	}
}
