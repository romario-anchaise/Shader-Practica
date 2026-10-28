using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Repairs references in the already-open scene after assets were imported.

public static class WaterSceneRepair
{
    const string Root = "Assets/_MyShaders/Level1/Water/";
    [MenuItem("Tools/Agua/Reparar materiales de la escena abierta")]
    public static void Repair()
    {
        if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            // Run this repair explicitly from Tools/Agua when needed.
            return;
        }
        SessionState.SetBool("WaterSceneRepair.v1", true);
        try
        {
            AssetDatabase.ImportAsset(Root + "SG_Water.shadergraph", ImportAssetOptions.ForceSynchronousImport);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(Root + "SG_Water.shadergraph");
            var water = AssetDatabase.LoadAssetAtPath<Material>(Root + "M_Water.mat");
            var border = AssetDatabase.LoadAssetAtPath<Material>(Root + "M_Borde.mat");
            var light = AssetDatabase.LoadAssetAtPath<Material>(Root + "M_FondoClaro.mat");
            var dark = AssetDatabase.LoadAssetAtPath<Material>(Root + "M_FondoOscuro.mat");
            if (shader == null || water == null || border == null || light == null || dark == null)
                throw new InvalidOperationException("Missing water exercise assets.");
            Undo.RecordObject(water, "Reparar shader de agua");
            water.shader = shader;
            EditorUtility.SetDirty(water);
            foreach (var mat in new[] { border, light, dark })
            {
                Undo.RecordObject(mat, "Reparar shader URP");
                mat.shader = Shader.Find("Universal Render Pipeline/Lit");
                EditorUtility.SetDirty(mat);
            }
            var scene = SceneManager.GetSceneByPath(Root + "WaterDemo.unity");
            if (!scene.IsValid() || !scene.isLoaded)
                scene = EditorSceneManager.OpenScene(Root + "WaterDemo.unity", OpenSceneMode.Additive);
            int repaired = 0;
            foreach (var renderer in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshRenderer>(true)))
            {
                Material material;
                if (renderer.name.StartsWith("Agua")) material = water;
                else if (renderer.name == "Baldosa sumergida")
                {
                    int x = Mathf.RoundToInt(renderer.transform.position.x + 4.5f);
                    int z = Mathf.RoundToInt(renderer.transform.position.z + 4.5f);
                    material = (x + z) % 2 == 0 ? light : dark;
                }
                else if (renderer.name == "Base" || renderer.name.StartsWith("Borde")) material = border;
                else continue;
                Undo.RecordObject(renderer, "Reasignar material de agua");
                renderer.sharedMaterial = material;
                repaired++;
            }
            SceneManager.SetActiveScene(scene);
            // Unload the untouched template scene so only the water camera renders.
            var sample = SceneManager.GetSceneByPath("Assets/Scenes/SampleScene.unity");
            if (sample.IsValid() && sample.isLoaded && !sample.isDirty)
                EditorSceneManager.CloseScene(sample, true);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            ShaderUtil.CompilePass(water, 0, true);
            if (ShaderUtil.ShaderHasError(shader))
                throw new InvalidOperationException("Water shader compilation failed in the live editor.");
            SceneView.RepaintAll();
            EditorApplication.QueuePlayerLoopUpdate();
            EditorApplication.ExecuteMenuItem("Window/General/Game");
            WaterExerciseSetup.SavePreview();
            File.WriteAllText("Library/WaterLivePreview.txt", "SUCCESS: rendered WaterDemo using the repaired materials in the user's live project.");
            File.WriteAllText("Library/WaterLiveRepair.txt", "SUCCESS: repaired " + repaired + " renderer references in live WaterDemo; shader compiled. Scenes loaded: " + SceneManager.sceneCount);
            Debug.Log("Agua reparada: materiales reasignados y escena guardada.");
        }
        catch (Exception e)
        {
            File.WriteAllText("Library/WaterLiveRepair.txt", e.ToString());
            Debug.LogException(e);
        }
    }
}
