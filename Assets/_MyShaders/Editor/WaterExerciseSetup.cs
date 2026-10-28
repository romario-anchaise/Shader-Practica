using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Editor-only builder. The saved graph, material and scene work without this script.

public static class WaterExerciseSetup
{
    const string Root = "Assets/_MyShaders/Level1/Water";
    const string GraphPath = Root + "/SG_Water.shadergraph";
    const string ScenePath = Root + "/WaterDemo.unity";
    const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static readonly Color Tint = new Color(0.025f, 0.48f, 0.72f, 0.8f);

    public static Type T(string name)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var type = assembly.GetType(name);
            if (type != null) return type;
        }
        throw new InvalidOperationException("Type not found: " + name);
    }
    public static object New(string name) => Activator.CreateInstance(T(name), true);
    public static object Get(object o, string name) => o.GetType().GetProperty(name, Flags).GetValue(o);
    public static void Set(object o, string name, object value)
    {
        var property = o.GetType().GetProperty(name, Flags);
        if (property == null) throw new MissingMemberException(o.GetType().FullName, name);
        property.SetValue(o, value);
    }
    public static object Call(object o, string name, params object[] args)
    {
        var method = o.GetType().GetMethods(Flags).First(m => m.Name == name && !m.IsGenericMethod && m.GetParameters().Length == args.Length);
        return method.Invoke(o, args);
    }
    public static object Node(object graph, string name, float x, float y)
    {
        var n = New("UnityEditor.ShaderGraph." + name);
        Call(n, "UpdateNodeAfterDeserialization");
        var draw = Get(n, "drawState");
        Set(draw, "position", new Rect(x, y, 210, 160));
        Set(draw, "expanded", true);
        Set(n, "drawState", draw);
        Call(graph, "AddNode", n, true);
        return n;
    }
    public static object Property(object graph, string type, string label, string reference, object value)
    {
        var p = New("UnityEditor.ShaderGraph.Internal." + type);
        Set(p, "displayName", label);
        Set(p, "overrideReferenceName", reference);
        Set(p, "generatePropertyBlock", true);
        Set(p, "value", value);
        Call(graph, "AddGraphInput", p, -1);
        return p;
    }
    public static object PropertyNode(object graph, object property, float x, float y)
    {
        var n = Node(graph, "PropertyNode", x, y);
        Set(n, "property", property);
        return n;
    }
    public static void Edge(object graph, object from, int output, object to, int input)
    {
        var result = Call(graph, "Connect", Call(from, "GetSlotReference", output), Call(to, "GetSlotReference", input));
        if (result == null) throw new InvalidOperationException("Connection failed.");
    }
    public static void FloatSlot(object node, int id, float value)
    {
        var method = node.GetType().GetMethods(Flags).First(m => m.Name == "FindSlot" && m.IsGenericMethod);
        var slot = method.MakeGenericMethod(T("UnityEditor.ShaderGraph.MaterialSlot")).Invoke(node, new object[] { id });
        Set(slot, "value", value);
    }

    [MenuItem("Tools/Agua/Crear ejercicio de agua")]
    public static void Build()
    {
        SessionState.SetBool("WaterExercise.Attempted.v4", true);
        try
        {
            Directory.CreateDirectory(Root);
            if (!File.Exists(GraphPath)) CreateGraph();
            AssetDatabase.ImportAsset(GraphPath, ImportAssetOptions.ForceSynchronousImport);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(GraphPath);
            if (shader == null || ShaderUtil.ShaderHasError(shader))
                throw new InvalidOperationException("SG_Water did not import correctly.");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(Root + "/M_Water.mat");
            if (mat == null)
            {
                mat = new Material(shader);
                mat.SetColor("_WaterTint", Tint);
                mat.SetVector("_FlowSpeed", new Vector4(0.1f, 0.05f, 0, 0));
                AssetDatabase.CreateAsset(mat, Root + "/M_Water.mat");
            }
            if (!File.Exists(ScenePath)) CreateScene(mat);
            AssetDatabase.SaveAssets();
            Selection.activeObject = mat;
            EditorGUIUtility.PingObject(mat);
            File.WriteAllText("Library/WaterExerciseResult.txt", "SUCCESS: SG_Water imported without shader errors. Material and WaterDemo scene saved.");
            Debug.Log("Agua lista: Assets/WaterExercise/WaterDemo.unity. Abre SG_Water para estudiar los nodos.");
            if (Application.isBatchMode && SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
                SavePreview();
        }
        catch (Exception e)
        {
            File.WriteAllText("Library/WaterExerciseResult.txt", e.ToString());
            Debug.LogException(e);
        }
    }

    static void CreateGraph()
    {
        var graph = New("UnityEditor.ShaderGraph.GraphData");
        Call(graph, "AddContexts");
        Set(graph, "path", "Ejercicios");
        var target = New("UnityEditor.Rendering.Universal.ShaderGraph.UniversalTarget");
        Call(target, "TrySetActiveSubTarget", T("UnityEditor.Rendering.Universal.ShaderGraph.UniversalLitSubTarget"));
        var surfaceProperty = target.GetType().GetProperty("surfaceType", Flags);
        surfaceProperty.SetValue(target, Enum.Parse(surfaceProperty.PropertyType, "Transparent"));
        var targets = Array.CreateInstance(T("UnityEditor.ShaderGraph.Target"), 1);
        targets.SetValue(target, 0);
        var names = new[] { "VertexDescription.Position", "VertexDescription.Normal", "VertexDescription.Tangent", "SurfaceDescription.BaseColor", "SurfaceDescription.NormalTS", "SurfaceDescription.Metallic", "SurfaceDescription.Smoothness", "SurfaceDescription.Emission", "SurfaceDescription.Occlusion", "SurfaceDescription.Alpha" };
        var descriptors = Array.CreateInstance(T("UnityEditor.ShaderGraph.BlockFieldDescriptor"), names.Length);
        for (int i = 0; i < names.Length; i++)
        {
            var parts = names[i].Split('.');
            descriptors.SetValue(T("UnityEditor.ShaderGraph.BlockFields+" + parts[0]).GetField(parts[1], Flags).GetValue(null), i);
        }
        Call(graph, "InitializeOutputs", targets, descriptors);
        var blocks = ((IEnumerable)Get(Get(graph, "fragmentContext"), "blocks")).Cast<object>().Select(b => Get(b, "value")).ToArray();
        Func<string, object> block = name => blocks.First(b => (string)Get(b, "name") == "SurfaceDescription." + name);
        Set(Get(graph, "fragmentContext"), "position", new Vector2(1140, 150));
        Set(Get(graph, "vertexContext"), "position", new Vector2(1140, -160));
        var speed = Property(graph, "Vector2ShaderProperty", "Flow Speed", "_FlowSpeed", new Vector4(0.1f, 0.05f, 0, 0));
        var color = Property(graph, "ColorShaderProperty", "Water Tint", "_WaterTint", Tint);
        var time = Node(graph, "TimeNode", -1050, -200);
        var flow = PropertyNode(graph, speed, -1050, 30);
        var multiply = Node(graph, "MultiplyNode", -740, -80);
        var tiling = Node(graph, "TilingAndOffsetNode", -420, -80);
        var noise = Node(graph, "GradientNoiseNode", -90, -80);
        var normal = Node(graph, "NormalFromHeightNode", 240, -80);
        // The slide leaves strength unspecified; this makes the waves readable on a 10m plane.
        FloatSlot(normal, 2, 0.35f);
        var tint = PropertyNode(graph, color, 200, 350);
        var split = Node(graph, "SplitNode", 540, 490);
        Edge(graph, time, 0, multiply, 0);
        Edge(graph, flow, 0, multiply, 1);
        Edge(graph, multiply, 2, tiling, 2);
        Edge(graph, tiling, 3, noise, 0);
        Edge(graph, noise, 2, normal, 0);
        Edge(graph, normal, 1, block("NormalTS"), 0);
        Edge(graph, tint, 0, block("BaseColor"), 0);
        Edge(graph, tint, 0, split, 0);
        Edge(graph, split, 4, block("Alpha"), 0);
        FloatSlot(block("Smoothness"), 0, 0.9f);
        Call(graph, "ValidateGraph");
        var serialize = T("UnityEditor.ShaderGraph.Serialization.MultiJson").GetMethod("Serialize", Flags);
        File.WriteAllText(GraphPath, (string)serialize.Invoke(null, new[] { graph }));
    }

    static Material Solid(string name, Color color, float smoothness)
    {
        string path = Root + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;
        mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Smoothness", smoothness);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }
    static GameObject Shape(PrimitiveType type, string name, Vector3 pos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.position = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }
    static void CreateScene(Material water)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
            Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.65f, 0.78f, 0.92f);
        RenderSettings.ambientEquatorColor = new Color(0.26f, 0.38f, 0.45f);
        RenderSettings.ambientGroundColor = new Color(0.18f, 0.19f, 0.22f);
        var stone = Solid("M_Borde", new Color(0.16f, 0.23f, 0.28f), 0.3f);
        var sand = Solid("M_FondoClaro", new Color(0.68f, 0.74f, 0.65f), 0.2f);
        var dark = Solid("M_FondoOscuro", new Color(0.28f, 0.40f, 0.42f), 0.2f);
        Shape(PrimitiveType.Plane, "Agua - SG_Water", Vector3.zero, new Vector3(1, 1, 1), water);
        Shape(PrimitiveType.Cube, "Base", new Vector3(0, -0.65f, 0), new Vector3(10.8f, 0.5f, 10.8f), stone);
        for (int x = 0; x < 10; x++)
            for (int z = 0; z < 10; z++)
                Shape(PrimitiveType.Cube, "Baldosa sumergida", new Vector3(x - 4.5f, -0.35f, z - 4.5f), new Vector3(0.99f, 0.1f, 0.99f), (x + z) % 2 == 0 ? sand : dark);
        Shape(PrimitiveType.Cube, "Borde norte", new Vector3(0, 0, 5.2f), new Vector3(10.8f, 0.5f, 0.4f), stone);
        Shape(PrimitiveType.Cube, "Borde sur", new Vector3(0, 0, -5.2f), new Vector3(10.8f, 0.5f, 0.4f), stone);
        Shape(PrimitiveType.Cube, "Borde este", new Vector3(5.2f, 0, 0), new Vector3(0.4f, 0.5f, 10), stone);
        Shape(PrimitiveType.Cube, "Borde oeste", new Vector3(-5.2f, 0, 0), new Vector3(0.4f, 0.5f, 10), stone);
        var light = new GameObject("Sol - reflejo de las ondas").AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 2.5f;
        light.transform.rotation = Quaternion.Euler(48, -30, 0);
        light.shadows = LightShadows.Soft;
        RenderSettings.sun = light;
        var camera = new GameObject("Water Camera").AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.transform.position = new Vector3(11, 10, -13);
        camera.transform.LookAt(new Vector3(0, -0.2f, 0));
        camera.fieldOfView = 42;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.055f, 0.09f, 0.14f);
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 100;
        camera.gameObject.AddComponent<AudioListener>();
        EditorSceneManager.SaveScene(scene, ScenePath);
        if (SceneView.lastActiveSceneView != null)
            SceneView.lastActiveSceneView.LookAt(Vector3.zero, camera.transform.rotation, 10);
    }

    // Used by batch validation with a graphics device, and available for local previews.
    [MenuItem("Tools/Agua/Guardar vista previa")]
    public static void SavePreview()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var camera = Camera.main;
        var material = AssetDatabase.LoadAssetAtPath<Material>(Root + "/M_Water.mat");
        ShaderUtil.CompilePass(material, 0, true);
        if (ShaderUtil.ShaderHasError(material.shader))
            throw new InvalidOperationException("SG_Water has GPU compilation errors.");
        var target = new RenderTexture(1280, 900, 24);
        camera.targetTexture = target;
        camera.Render();
        var previous = RenderTexture.active;
        RenderTexture.active = target;
        var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
        image.Apply();
        File.WriteAllBytes("WaterPreview.png", image.EncodeToPNG());
        RenderTexture.active = previous;
        camera.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(image);
        target.Release();
        UnityEngine.Object.DestroyImmediate(target);
        File.WriteAllText("Library/WaterPreviewResult.txt", "SUCCESS: GPU compilation and 1280x900 camera render completed.");
    }
}
