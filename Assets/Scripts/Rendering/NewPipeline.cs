using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Unmath;
using static Unmath.Math;

public class NewPipeline : RenderPipelineBase
{
	private readonly NewPipelineAsset asset;
	private readonly Material blitMaterial, deferredMaterial;

	public NewPipeline(NewPipelineAsset asset)
	{
		this.asset = asset;
		blitMaterial = new Material(Shader.Find("Hidden/Blit Material")) { hideFlags = HideFlags.HideAndDontSave };
		deferredMaterial = new Material(Shader.Find("Hidden/Morrowind Deferred")) { hideFlags = HideFlags.HideAndDontSave };
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

		var sunDirection = camera.transform.WorldRotation().InverseRotate(Float3.Up);
		var sunColor = Float3.One;
		for (var i = 0; i < cullingResults.visibleLights.Length; i++)
		{
			var visibleLight = cullingResults.visibleLights[i];
			if (visibleLight.lightType != LightType.Directional)
				continue;

			var lightToWorld = (Float4x4)visibleLight.localToWorldMatrix;
			var lightRotation = lightToWorld.Rotation;
			var viewSpaceLightRotation = camera.transform.WorldRotation().InverseRotate(lightRotation);
			sunDirection = -viewSpaceLightRotation.Forward;
			sunColor = visibleLight.finalColor.Float3();
		}

		var viewSize = new Int2(camera.pixelWidth, camera.pixelHeight);
		renderGraph.AddRenderPass("Set View Data", false, viewSize, asset.Samples, (camera, fogEnabled, sunDirection, sunColor), default, static (command, data) =>
		{
			var tanHalfFovY = Tan(0.5f * Radians(data.camera.fieldOfView));
			var tanHalfFov = new Float2(tanHalfFovY * data.camera.aspect, tanHalfFovY);
			var worldToView = Float4x4.WorldToLocal(0.0f, data.camera.transform.WorldRotation());
			var viewToClip = Float4x4.PerspectiveReverseZ(tanHalfFov, data.camera.nearClipPlane, data.camera.farClipPlane);
			var worldToClip = viewToClip.Mul(worldToView);

			//ReadOnlySpan<byte> cbuffer = MemoryMarshal.AsBytes(stackalloc[] {
			//(
			//	worldToView,
			//	viewToClip,
			//	worldToClip,
			//	data.camera.transform.position,
			//	(Float4)tanHalfFov,
			//	data.sunDirection,
			//	data.sunColor,
			//	RenderSettings.ambientLight.linear,
			//	RenderSettings.fogColor.linear
			//)});

			command.SetGlobalMatrix("WorldToView", worldToView);
			command.SetGlobalMatrix("ViewToClip", viewToClip);
			command.SetGlobalMatrix("WorldToClip", worldToClip);
			command.SetGlobalVector("ViewPosition", data.camera.transform.position);
			command.SetGlobalVector("TanHalfFov", (Float4)tanHalfFov);

			command.SetGlobalVector("SunDirection", data.sunDirection);
			command.SetGlobalVector("SunColor", data.sunColor);
			command.SetGlobalVector("AmbientLight", RenderSettings.ambientLight.linear);
			command.SetGlobalVector("FogColor", RenderSettings.fogColor.linear);
			command.SetGlobalFloat("FogScale", data.fogEnabled ? 1 / (RenderSettings.fogEndDistance - RenderSettings.fogStartDistance) : 0);
			command.SetGlobalFloat("FogOffset", data.fogEnabled ? RenderSettings.fogStartDistance / (RenderSettings.fogStartDistance - RenderSettings.fogEndDistance) : 0);
			command.SetGlobalVector("ViewSize", new(data.camera.pixelWidth, data.camera.pixelHeight));

			command.SetGlobalFloat("LinearDepthScale", (data.camera.farClipPlane - data.camera.nearClipPlane) * Rcp(data.camera.nearClipPlane * data.camera.farClipPlane));
			command.SetGlobalFloat("LinearDepthOffset", Rcp(data.camera.farClipPlane));

			var overlayMatrix = Float4x4.OrthoReverseZ(-Screen.width / 2f, Screen.width / 2f, -Screen.height / 2f, Screen.height / 2f, 0, 1);
			command.SetGlobalMatrix("UiOverlayMatrix", overlayMatrix);
		});

		var depthFormat = camera.targetTexture == null ? GraphicsFormat.D32_SFloat_S8_UInt : camera.targetTexture.depthStencilFormat;
		var cameraDepth = renderGraph.GetTexture(new(viewSize, depthFormat, true));

		// TODO: This should also account for HDR
		var backbufferFormat = QualitySettings.activeColorSpace == ColorSpace.Linear ? GraphicsFormat.R8G8B8A8_SRGB : GraphicsFormat.R8G8B8A8_UNorm;
		var targetFormat = camera.targetTexture == null ? backbufferFormat : camera.targetTexture.graphicsFormat;
		var cameraColor = renderGraph.GetTexture(new(viewSize, targetFormat, true, RenderSettings.fogColor.linear));
		var albedoMetallic = renderGraph.GetTexture(new(viewSize, GraphicsFormat.R8G8B8A8_UNorm));
		var normalOcclusionRoughness = renderGraph.GetTexture(new(viewSize, GraphicsFormat.R8G8B8A8_UNorm));

		var opaqueRendererParams = new RendererListParams(cullingResults, new(new("GBuffer"), new(camera) { criteria = SortingCriteria.CommonOpaque }) { enableInstancing = true }, new(RenderQueueRange.opaque));
		var opaqueRendererList = context.CreateRendererList(ref opaqueRendererParams);
		renderGraph.AddRenderPass("Gbuffer", true, viewSize, asset.Samples, opaqueRendererList, stackalloc[] { cameraDepth, albedoMetallic, normalOcclusionRoughness, cameraColor },
		static (command, opaqueRendererList) => { command.DrawRendererList(opaqueRendererList); });

		var deferredLighting = renderGraph.AddRenderPass("Deferred Lighting", true, viewSize, asset.Samples, (deferredMaterial, asset), stackalloc[] { cameraDepth, cameraColor }, static (command, data) =>
		{
			if (data.asset.Samples > 1)
				command.EnableShaderKeyword("MSAA_ON");

			command.DrawProcedural(default, data.deferredMaterial, 0, MeshTopology.Triangles, 3);

			if (data.asset.Samples > 1)
				command.DisableShaderKeyword("MSAA_ON");
		});
		{
			deferredLighting.ReadTexture(cameraDepth, Shader.PropertyToID("CameraDepth"));
			deferredLighting.ReadTexture(albedoMetallic, Shader.PropertyToID("GBufferAlbedoMetallic"));
			deferredLighting.ReadTexture(normalOcclusionRoughness, Shader.PropertyToID("GBufferNormalOcclusionRoughness"));
		}

		var transparentRendererParams = new RendererListParams(cullingResults, new(new("Forward"), new(camera) { criteria = SortingCriteria.CommonTransparent }) { enableInstancing = true }, new(RenderQueueRange.transparent));
		var transparentRendererList = context.CreateRendererList(ref transparentRendererParams);
		renderGraph.AddRenderPass("Render Forward Transparent", true, viewSize, asset.Samples, transparentRendererList, stackalloc[] { cameraDepth, cameraColor }, static (command, transparentRendererList) => { command.DrawRendererList(transparentRendererList); });

		// Can only render directly to backbuffer if there is no msaa samples and there is no target texture
		// TODO: Check for hardware msaa backbuffer resolve support
		var renderToBackbuffer = asset.Samples == 1 && camera.targetTexture == null;
		if (renderToBackbuffer)
		{
			renderGraph.ExportResource(cameraColor, BuiltinRenderTextureType.CameraTarget);
		}
		else
		{
			var requiresSceneDepth = camera.cameraType == CameraType.SceneView;
			renderGraph.AddRenderPass("Final Blit Setup", false, viewSize, 1, (camera, asset, requiresSceneDepth), default, static (command, data) =>
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
			var backbufferColor = renderGraph.GetTexture(new(viewSize, targetFormat));

			// For sceneView, take the first depth sample for for gizmos, wireframe, etc.
			TextureHandle sceneDepth = default;
			if (requiresSceneDepth)
			{
				sceneDepth = renderGraph.GetTexture(new(viewSize, depthFormat));
				renderGraph.ExportResource(sceneDepth, camera.targetTexture);
			}

			var outputs = requiresSceneDepth ? stackalloc[] { sceneDepth, backbufferColor } : stackalloc[] { backbufferColor };
			var finalBlitPass = renderGraph.AddRenderPass("Final Blit", true, viewSize, 1, (blitMaterial, camera, asset, requiresSceneDepth), outputs, static (command, data) =>
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

				if (requiresSceneDepth)
					finalBlitPass.ReadTexture(cameraDepth, Shader.PropertyToID("CameraDepth"));
			}

			renderGraph.ExportResource(backbufferColor, camera.targetTexture == null ? BuiltinRenderTextureType.CameraTarget : camera.targetTexture);
		}
	}
}
