using UnityEngine;
using UnityEngine.Rendering;

public class EnvironmentSettings
{
    private Color ambientLight, fogColor;
    private float fogStartDistance, fogEndDistance;
    private bool fogEnabled;
    private bool isInitialized;

    public bool NeedsRebuild(bool force = false)
    {
        var fogEnabled = RenderSettings.fog;

#if UNITY_EDITOR
        if (UnityEditor.SceneView.currentDrawingSceneView != null)
            fogEnabled &= UnityEditor.SceneView.currentDrawingSceneView.sceneViewState.fogEnabled;
#endif

        var ambientLight = RenderSettings.ambientLight.linear;
        var fogColor = RenderSettings.fogColor.linear;
        var fogStartDistance = RenderSettings.fogStartDistance;
        var fogEndDistance = RenderSettings.fogEndDistance;

        var hasChanged = !isInitialized ||
            fogEnabled != this.fogEnabled ||
            ambientLight != this.ambientLight ||
            fogColor != this.fogColor ||
            fogStartDistance != this.fogStartDistance ||
            fogEndDistance != this.fogEndDistance;

        if(hasChanged)
        {
            this.fogEnabled = fogEnabled; 
            this.ambientLight = ambientLight;
            this.fogColor = fogColor;
            this.fogStartDistance = fogStartDistance;
            this.fogEndDistance = fogEndDistance;
        }

        isInitialized = true;

        return hasChanged;
    }

    public void Rebuild(CommandBuffer commandBuffer)
    {
        commandBuffer.SetGlobalVector("_AmbientLightColor", ambientLight);
        commandBuffer.SetGlobalVector("_FogColor", fogColor);
        commandBuffer.SetGlobalFloat("_FogStartDistance", fogStartDistance);
        commandBuffer.SetGlobalFloat("_FogEndDistance", fogEndDistance);
        commandBuffer.SetGlobalFloat("_FogEnabled", fogEnabled ? 1.0f : 0.0f);
    }
}
