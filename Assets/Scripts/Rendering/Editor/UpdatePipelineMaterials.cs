using UnityEngine;
using UnityEditor;

public class UpdatePipelineMaterials
{
    [MenuItem("Tools/Update Pipeline Materials")]
    public static void OnUpdatePipelineMaterialsSelected()
    {
        var sourceShader = Shader.Find("Universal Render Pipeline/Lit");
        var replacementShader = Shader.Find("Lit Surface");

        var count = 0;
        var materialGuids = AssetDatabase.FindAssets("t:Material");
        for (int i = 0; i < materialGuids.Length; i++)
        {
            EditorUtility.DisplayProgressBar("Updating Materials", $"{i}/{materialGuids.Length}", (float)i / materialGuids.Length);

            string guid = materialGuids[i];
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material.shader == sourceShader)
            {
                count++;
                material.shader = replacementShader;
            }

            material.enableInstancing = true;
        }

        EditorUtility.ClearProgressBar();
        Debug.Log($"Updated {count} materials");
    }
}
