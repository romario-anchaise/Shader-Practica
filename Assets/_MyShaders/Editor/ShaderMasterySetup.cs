using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class ShaderMasterySetup
{
    public const string Root = "Assets/_MyShaders";
    public const string ScenePath = Root + "/Scenes/ShaderShowcase.unity";
    const string Evidence = "EntregaShaders/Evidencias";
    static readonly string[] Graphs = {
        "Level1/SG_PulseGlow.shadergraph", "Level1/SG_Dissolve.shadergraph", "Level1/Water/SG_Water.shadergraph",
        "Level2/SG_ForceField.shadergraph", "Level2/SG_Hologram.shadergraph", "Level3/SG_TideGate.shadergraph" };
    static readonly string[] Materials = { "M_PulseGlow", "M_Dissolve", "M_Water", "M_ForceField", "M_Hologram", "M_TideGate" };
    static readonly Vector3[] Positions = { new Vector3(-6,1.8f,0), new Vector3(0,1.8f,0), new Vector3(6,0.4f,0), new Vector3(-6,1.8f,8), new Vector3(0,1.7f,8), new Vector3(6,2.2f,8) };

    [MenuItem("Tools/Shader Mastery/Crear entrega completa")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.isPlaying = false;
            EditorApplication.delayCall += Build;
            return;
        }
        if (EditorApplication.isCompiling) { EditorApplication.delayCall += Build; return; }
        SessionState.SetBool("Mastery.Attempt.v2", true);
        try
        {
            foreach (string dir in new[] { "Level1", "Level2", "Level3", "Materials", "Prefabs", "Scenes", "Scripts", "Editor" })
                Directory.CreateDirectory(Root + "/" + dir);
            AssetDatabase.Refresh();
            if (AssetDatabase.IsValidFolder("Assets/WaterExercise") && !AssetDatabase.IsValidFolder(Root + "/Level1/Water"))
            {
                string error = AssetDatabase.MoveAsset("Assets/WaterExercise", Root + "/Level1/Water");
                if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
            }
            if (!File.Exists(Root + "/Level1/Water/SG_Water.shadergraph")) WaterExerciseSetup.Build();
            MasteryGraphs.CreateAll();
            var mats = new Material[6];
            for (int i = 0; i < Graphs.Length; i++)
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(Root + "/" + Graphs[i]);
                string path = i == 2 ? Root + "/Level1/Water/M_Water.mat" : Root + "/Materials/" + Materials[i] + ".mat";
                mats[i] = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mats[i] == null) { mats[i] = new Material(shader); AssetDatabase.CreateAsset(mats[i], path); }
                else { mats[i].shader = shader; EditorUtility.SetDirty(mats[i]); }
                ShaderUtil.CompilePass(mats[i], 0, true);
                if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Shader compilation failed: " + Graphs[i]);
            }
            if (!File.Exists(ScenePath)) CreateScene(mats);
            else if (SceneManager.GetActiveScene().path != ScenePath) EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            PolishScene();
            AssetDatabase.SaveAssets();
            var builds = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            builds.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = builds.ToArray();
            ValidateScene();
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null) RenderEvidence();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            File.WriteAllText("Library/MasteryComplete.txt", "SUCCESS: six Shader Graphs compiled, exposed materials saved, functional portal prefab and showcase scene validated.\n" + DateTime.UtcNow.ToString("O"));
            File.WriteAllText("Library/MasteryPolished.txt", "SUCCESS: front-face transparent shields, hologram glitch and brighter gallery.");
            Debug.Log("Shader Mastery listo. Abre Assets/_MyShaders/Scenes/ShaderShowcase.unity. Play: 1-6 primeros planos, TAB galeria, WASD mover, R portal.");
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            EditorApplication.ExecuteMenuItem("Window/General/Game");
        }
        catch (Exception e)
        {
            File.WriteAllText("Library/MasteryError.txt", e.ToString());
            Debug.LogException(e);
            if (Application.isBatchMode) throw;
        }
    }

    static Material Solid(string name, Color color, float metallic = 0, float smoothness = 0.4f)
    {
        var path = Root + "/Materials/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;
        mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetColor("_BaseColor", color); mat.SetFloat("_Metallic", metallic); mat.SetFloat("_Smoothness", smoothness);
        AssetDatabase.CreateAsset(mat, path); return mat;
    }
    static GameObject Shape(PrimitiveType type, string name, Vector3 pos, Vector3 scale, Material material, Transform parent = null)
    {
        var o = GameObject.CreatePrimitive(type); o.name = name;
        if (parent != null) o.transform.SetParent(parent, false);
        o.transform.localPosition = pos; o.transform.localScale = scale;
        o.GetComponent<Renderer>().sharedMaterial = material; return o;
    }
    static void Label(string text, Vector3 position, Color color, float size = 0.09f)
    {
        var go = new GameObject(text); go.transform.position = position;
        var t = go.AddComponent<TextMesh>(); t.text = text; t.anchor = TextAnchor.MiddleCenter;
        t.alignment = TextAlignment.Center; t.fontSize = 64; t.characterSize = size; t.color = color;
    }
    static GameObject Prefab(GameObject source, string name)
    {
        string path = Root + "/Prefabs/" + name + ".prefab";
        PrefabUtility.SaveAsPrefabAssetAndConnect(source, path, InteractionMode.AutomatedAction);
        return source;
    }
    static void CreateScene(Material[] mats)
    {
        // Save existing named scenes before showing the new scene.
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var current = SceneManager.GetSceneAt(i);
            if (current.isDirty && !string.IsNullOrEmpty(current.path)) EditorSceneManager.SaveScene(current);
        }
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.3f,0.4f,0.55f);
        RenderSettings.ambientEquatorColor = new Color(0.14f,0.19f,0.24f);
        RenderSettings.ambientGroundColor = new Color(0.05f,0.07f,0.1f);
        var metal = Solid("M_Architecture", new Color(0.045f,0.075f,0.1f), 0.6f, 0.55f);
        var floor = Solid("M_GalleryFloor", new Color(0.065f,0.085f,0.11f), 0.1f, 0.25f);
        var interior = Solid("M_ShieldCore", new Color(0.23f,0.28f,0.31f), 0.8f, 0.6f);
        Shape(PrimitiveType.Cube,"Galeria - piso",new Vector3(0,-0.3f,4),new Vector3(23,0.5f,21),floor);
        var names = new[] { "01  GLOW PULSANTE", "02  DISOLUCION", "03  AGUA ESTILIZADA", "04  CAMPO DE FUERZA", "05  HOLOGRAMA", "06  UMBRAL DE MAREA" };
        for (int i = 0; i < 6; i++)
        {
            Shape(PrimitiveType.Cube, "Pedestal " + (i+1), new Vector3(Positions[i].x,0.15f,Positions[i].z), new Vector3(4.4f,0.3f,4.4f), metal);
            Label(names[i], new Vector3(Positions[i].x,0.72f,Positions[i].z-2.15f), new Color(0.65f,0.9f,0.93f),0.055f);
        }
        var pulse = Shape(PrimitiveType.Sphere,"Energia pulsante",Positions[0],Vector3.one*2,mats[0]); pulse.AddComponent<SimpleRotator>(); Prefab(pulse,"PulseBeacon");
        var dissolve = Shape(PrimitiveType.Cube,"Objeto en disolucion",Positions[1],Vector3.one*2,mats[1]);
        dissolve.AddComponent<SimpleRotator>(); dissolve.AddComponent<DissolveDemonstration>(); Prefab(dissolve,"DissolvingRelic");
        var pond = new GameObject("Estanque de agua"); pond.transform.position = Positions[2];
        Shape(PrimitiveType.Plane,"Superficie de agua",Vector3.zero,new Vector3(0.39f,1,0.39f),mats[2],pond.transform);
        Shape(PrimitiveType.Cube,"Fondo del estanque",new Vector3(0,-0.3f,0),new Vector3(4,0.2f,4),interior,pond.transform);
        for (int i = -1; i <= 1; i += 2)
        {
            Shape(PrimitiveType.Cube,"Marco",new Vector3(i*2,0,0),new Vector3(0.15f,0.3f,4.15f),metal,pond.transform);
            Shape(PrimitiveType.Cube,"Marco",new Vector3(0,0,i*2),new Vector3(4.15f,0.3f,0.15f),metal,pond.transform);
        }
        Prefab(pond,"WaterBasin");
        var shieldRoot = new GameObject("Campo de fuerza"); shieldRoot.transform.position = Positions[3];
        var shield = Shape(PrimitiveType.Sphere,"Membrana de escudo",Vector3.zero,Vector3.one*2.8f,mats[3],shieldRoot.transform);
        UnityEngine.Object.DestroyImmediate(shield.GetComponent<Collider>());
        Shape(PrimitiveType.Cube,"Nucleo protegido",Vector3.zero,Vector3.one*0.8f,interior,shieldRoot.transform);
        shieldRoot.AddComponent<SimpleRotator>(); Prefab(shieldRoot,"ForceFieldGenerator");
        var holo = Shape(PrimitiveType.Capsule,"Baliza holografica",Positions[4],new Vector3(1.5f,1.3f,1.5f),mats[4]);
        holo.AddComponent<SimpleRotator>(); Prefab(holo,"HolographicBeacon");
        var portalRoot = new GameObject("Umbral de Marea - portal funcional"); portalRoot.transform.position = new Vector3(6,0.4f,8);
        Shape(PrimitiveType.Cube,"Pilar izquierdo",new Vector3(-1.65f,2,0),new Vector3(0.3f,4.3f,0.5f),metal,portalRoot.transform);
        Shape(PrimitiveType.Cube,"Pilar derecho",new Vector3(1.65f,2,0),new Vector3(0.3f,4.3f,0.5f),metal,portalRoot.transform);
        Shape(PrimitiveType.Cube,"Dintel",new Vector3(0,4.05f,0),new Vector3(3.6f,0.3f,0.5f),metal,portalRoot.transform);
        var membrane = Shape(PrimitiveType.Quad,"Membrana - Shader Maestro",new Vector3(0,2,0),new Vector3(3,3.8f,1),mats[5],portalRoot.transform);
        UnityEngine.Object.DestroyImmediate(membrane.GetComponent<Collider>());
        for (int i = 0; i < 5; i++)
            Shape(PrimitiveType.Cube,"Runas del marco",new Vector3(-1.65f,0.5f+i*0.75f,-0.28f),new Vector3(0.18f,0.2f,0.05f),mats[0],portalRoot.transform);
        var trigger = portalRoot.AddComponent<BoxCollider>(); trigger.isTrigger = true; trigger.center = new Vector3(0,2,0); trigger.size = new Vector3(2.7f,3.8f,0.9f);
        var rigid = portalRoot.AddComponent<Rigidbody>(); rigid.isKinematic = true; rigid.useGravity = false;
        var gate = portalRoot.AddComponent<TideGate>(); gate.membrane = membrane.GetComponent<Renderer>();
        Prefab(portalRoot,"TideGate");
        var destination = new GameObject("Destino de teletransporte"); destination.transform.position = new Vector3(-6,1.8f,-4);
        gate.destination = destination.transform;
        PrefabUtility.RecordPrefabInstancePropertyModifications(gate);
        Label("UMBRAL DE MAREA / R: CAMBIAR ESTADO",new Vector3(6,5.15f,8),new Color(0.3f,1f,0.8f),0.047f);
        var sun = new GameObject("Key Light").AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity=1.8f; sun.color=new Color(0.8f,0.9f,1f); sun.transform.rotation=Quaternion.Euler(45,-35,0); sun.shadows=LightShadows.Soft; RenderSettings.sun=sun;
        var fill = new GameObject("Fill Light").AddComponent<Light>(); fill.type=LightType.Directional; fill.intensity=0.6f; fill.color=new Color(0.35f,0.7f,1f); fill.transform.rotation=Quaternion.Euler(30,150,0);
        var volumeObject=new GameObject("Global Volume - Bloom HDR"); var volume=volumeObject.AddComponent<Volume>(); volume.isGlobal=true;
        var profile=ScriptableObject.CreateInstance<VolumeProfile>(); var bloom=profile.Add<Bloom>(true); bloom.intensity.Override(0.55f); bloom.threshold.Override(1); bloom.scatter.Override(0.65f);
        var tone=profile.Add<Tonemapping>(true); tone.mode.Override(TonemappingMode.ACES);
        AssetDatabase.CreateAsset(profile,Root+"/Materials/ShowcaseVolume.asset"); volume.sharedProfile=profile;
        var player=new GameObject("Visitante - controles WASD"); player.transform.position=new Vector3(0,13,-21); player.tag="Player";
        var cc=player.AddComponent<CharacterController>(); cc.height=1.5f; cc.radius=0.25f; cc.minMoveDistance=0;
        var cameraObject=new GameObject("Showcase Camera"); cameraObject.transform.SetParent(player.transform,false);
        var camera=cameraObject.AddComponent<Camera>(); camera.tag="MainCamera"; camera.allowHDR=true; camera.fieldOfView=48; camera.nearClipPlane=0.08f; camera.farClipPlane=100;
        camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(0.014f,0.025f,0.045f); camera.transform.LookAt(new Vector3(0,1.2f,4)); cameraObject.AddComponent<AudioListener>();
        camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
        var controls=player.AddComponent<ShaderGalleryController>(); controls.view=camera; controls.portal=gate; controls.focusPoints=Positions;
        controls.shotPositions=Positions.Select((p,i)=>p+new Vector3(i==2?4.8f:0,i==2?4.2f:0.6f,i==2?-5.4f:-6.8f)).ToArray();
        EditorSceneManager.SaveScene(scene,ScenePath);
        if(SceneView.lastActiveSceneView!=null) SceneView.lastActiveSceneView.LookAt(new Vector3(0,1,4),camera.transform.rotation,18);
    }
    public static void ValidateScene()
    {
        if (!EditorApplication.isPlaying && SceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var scene=SceneManager.GetActiveScene();
        var renderers=scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<Renderer>(true)).ToArray();
        foreach(var renderer in renderers)
            foreach(var mat in renderer.sharedMaterials)
                if(mat==null || mat.shader==null || !mat.shader.isSupported || ShaderUtil.ShaderHasError(mat.shader))
                    throw new InvalidOperationException("Invalid material on "+renderer.name);
        var gate=UnityEngine.Object.FindFirstObjectByType<TideGate>();
        if(gate==null || gate.membrane==null || gate.destination==null || !PrefabUtility.IsPartOfPrefabInstance(gate))
            throw new InvalidOperationException("Portal prefab integration failed.");
        if(UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length!=1)
            throw new InvalidOperationException("Showcase must have one audio listener.");
        File.WriteAllText("Library/MasterySceneValidation.txt","SUCCESS: "+renderers.Length+" renderers have valid supported shaders; one audio listener; connected gameplay portal prefab.");
    }
    static void LayerTree(GameObject root, int layer)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer=layer;
    }
    static void PolishScene()
    {
        var scene=SceneManager.GetActiveScene();
        var roots=scene.GetRootGameObjects();
        var objectNames=new[]{"Energia pulsante","Objeto en disolucion","Estanque de agua","Campo de fuerza","Baliza holografica","Umbral de Marea - portal funcional"};
        for(int i=0;i<6;i++)
        {
            foreach(var root in roots.Where(r=>r.name==objectNames[i] || r.name=="Pedestal "+(i+1) || r.name.StartsWith((i+1).ToString("00")+"  ")))
                LayerTree(root,8+i);
        }
        foreach(var root in roots.Where(r=>(r.name.StartsWith("PORTAL ACTIVO") || r.name.StartsWith("UMBRAL DE MAREA /")))) LayerTree(root,13);
        var floor=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/M_GalleryFloor.mat"); floor.SetColor("_BaseColor",new Color(0.22f,0.26f,0.32f)); EditorUtility.SetDirty(floor);
        var metal=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/M_Architecture.mat"); metal.SetColor("_BaseColor",new Color(0.17f,0.22f,0.28f)); metal.SetFloat("_Metallic",0.3f); EditorUtility.SetDirty(metal);
        var dissolve=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/M_Dissolve.mat"); dissolve.SetColor("_BaseColor",new Color(0.3f,0.4f,0.5f)); EditorUtility.SetDirty(dissolve);
        var controls=UnityEngine.Object.FindFirstObjectByType<ShaderGalleryController>();
        controls.shotPositions[5]=Positions[5]+new Vector3(0,0.9f,-9);
        controls.view.backgroundColor=new Color(0.045f,0.06f,0.085f);
        EditorUtility.SetDirty(controls);
    }
    [MenuItem("Tools/Shader Mastery/Guardar evidencias de los efectos")]
    public static void RenderEvidence()
    {
        Directory.CreateDirectory(Evidence);
        var controls=UnityEngine.Object.FindFirstObjectByType<ShaderGalleryController>();
        var camera=controls.view;
        var oldPosition=controls.transform.position; var oldRotation=camera.transform.rotation;
        int oldMask=camera.cullingMask;
        Capture(camera,Evidence+"/00_Galeria.png",1600,1000);
        for(int i=0;i<6;i++)
        {
            controls.transform.position=controls.shotPositions[i]; camera.transform.LookAt(controls.focusPoints[i]);
            camera.cullingMask=1 | (1<<(8+i));
            Capture(camera,Evidence+"/"+(i+1).ToString("00")+"_"+Materials[i]+".png",1280,1000);
        }
        controls.transform.position=oldPosition; camera.transform.rotation=oldRotation;
        camera.cullingMask=oldMask;
        File.WriteAllText("Library/MasteryRenderValidation.txt","SUCCESS: seven camera views rendered inside the project with the final material references.");
    }
    public static void Capture(Camera camera,string path,int width,int height)
    {
        var target=new RenderTexture(width,height,24,RenderTextureFormat.ARGBHalf);
        var previous=RenderTexture.active;
        try
        {
            camera.targetTexture=target; camera.Render(); RenderTexture.active=target;
            var image=new Texture2D(width,height,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,width,height),0,0); image.Apply(); File.WriteAllBytes(path,image.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(image);
        }
        finally { camera.targetTexture=null; RenderTexture.active=previous; target.Release(); UnityEngine.Object.DestroyImmediate(target); }
    }
}
