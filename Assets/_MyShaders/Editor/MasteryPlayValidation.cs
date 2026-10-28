using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Integration check in real Play mode: animated dissolve, HDR readiness, trigger teleport,
// and rendered frames of the original material for the Dev Kata GIF.
[InitializeOnLoad]
public static class MasteryPlayValidation
{
    const string Key = "MasterySmoke.Phase";
    static ShaderGalleryController player;
    static TideGate gate;
    static Renderer dissolve;
    static float firstAmount;
    static Quaternion firstRotation;
    static double phaseStarted;
    static int captured;
    static bool finishing;

    static MasteryPlayValidation()
    {
        if (SessionState.GetInt(Key,0)>0)
            EditorApplication.update += Tick;
    }
    [MenuItem("Tools/Shader Mastery/Probar animacion y teletransporte")]
    public static void Start()
    {
        PlayerSettings.runInBackground = true;
        if (!EditorApplication.isPlaying)
            EditorSceneManager.OpenScene(ShaderMasterySetup.ScenePath, OpenSceneMode.Single);
        ShaderMasterySetup.ValidateScene();
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        SessionState.SetInt(Key, 1);
        EditorApplication.isPlaying = true;
    }
    static float Amount()
    {
        var block = new MaterialPropertyBlock(); dissolve.GetPropertyBlock(block); return block.GetFloat("_DissolveAmount");
    }
    static void State(int next) { SessionState.SetInt(Key, next); phaseStarted=EditorApplication.timeSinceStartup; }
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    static void Tick()
    {
        if (finishing || EditorApplication.isCompiling) return;
        int phase = SessionState.GetInt(Key,0);
        try
        {
            if (phase==0)
            {
                return;
            }
            if (!EditorApplication.isPlaying) return;
            if (phase==1)
            {
                player=UnityEngine.Object.FindFirstObjectByType<ShaderGalleryController>(); gate=player.portal;
                dissolve=UnityEngine.Object.FindFirstObjectByType<DissolveDemonstration>().GetComponent<Renderer>();
                firstAmount=Amount(); firstRotation=dissolve.transform.rotation;
                State(2); return;
            }
            if (player==null) { State(1); return; }
            double elapsed=EditorApplication.timeSinceStartup-phaseStarted;
            if (phase==2 && elapsed>0.9)
            {
                Require(Mathf.Abs(Amount()-firstAmount)>0.01f,"Dissolve property did not animate.");
                Require(Quaternion.Angle(dissolve.transform.rotation,firstRotation)>1,"Showcase object did not rotate.");
                player.Focus(5); player.showInterface=false;
                Directory.CreateDirectory("EntregaShaders/Evidencias/KataFrames"); captured=0; State(3); return;
            }
            if (phase==3)
            {
                if(elapsed>=captured*0.12 && captured<24)
                {
                    ShaderMasterySetup.Capture(player.view,"EntregaShaders/Evidencias/KataFrames/frame_"+captured.ToString("000")+".png",800,640);
                    captured++;
                }
                if(captured<24) return;
                gate.ToggleReady(); Require(!gate.Ready,"Portal did not switch off.");
                var block=new MaterialPropertyBlock(); gate.membrane.GetPropertyBlock(block);
                Require(Mathf.Approximately(block.GetFloat("_Readiness"),0),"Inactive state not sent to shader.");
                player.Teleport(gate.transform.position+new Vector3(0,2,-1.3f)); State(4); return;
            }
            if (phase==4)
            {
                player.GetComponent<CharacterController>().Move(Vector3.forward*6f*Mathf.Min(Time.deltaTime,0.03f));
                if(elapsed<0.8) return;
                Require(TideGate.TeleportCount==0,"Inactive portal teleported the player.");
                player.Teleport(gate.transform.position+new Vector3(0,2,-2f)); Physics.SyncTransforms();
                gate.ToggleReady();
                Require(gate.Ready,"Portal did not switch on."); State(6); return;
            }
            if(phase==6)
            {
                if(elapsed<0.4) return;
                State(5); return;
            }
            if(phase==5)
            {
                if(TideGate.TeleportCount==0) { player.GetComponent<CharacterController>().Move(Vector3.forward*6f*Mathf.Min(Time.deltaTime,0.03f)); Physics.SyncTransforms(); }
                if(TideGate.TeleportCount>0)
                {
                    Require(Vector3.Distance(player.transform.position,gate.destination.position)<0.8f,"Portal destination mismatch.");
                    File.WriteAllText("Library/MasteryPlayValidation.txt","PASS: dissolve changes over time; object rotates; inactive portal blocks teleport; _Readiness changes; active portal trigger teleports to its destination. Captured 24 live material frames.");
                    File.WriteAllText("EntregaShaders/Evidencias/Validacion.txt",File.ReadAllText("Library/MasteryPlayValidation.txt")+"\n"+File.ReadAllText("Library/MasterySceneValidation.txt"));
                    finishing=true; SessionState.SetInt(Key,0); EditorApplication.isPlaying=false; EditorApplication.update-=Tick;
                    if(Application.isBatchMode) EditorApplication.Exit(0);
                }
                else if(elapsed>8) throw new InvalidOperationException("Portal trigger did not teleport. Player="+player.transform.position+", gate="+gate.transform.position+", trigger="+gate.GetComponent<Collider>().bounds+", ready="+gate.Ready+", gameTime="+Time.time+", frames="+Time.frameCount+", layerCollision="+!Physics.GetIgnoreLayerCollision(0,gate.gameObject.layer));
            }
        }
        catch(Exception e)
        {
            File.WriteAllText("Library/MasteryPlayError.txt",e.ToString()); Debug.LogException(e);
            finishing=true; SessionState.SetInt(Key,0); EditorApplication.isPlaying=false; EditorApplication.update-=Tick;
            if(Application.isBatchMode) EditorApplication.Exit(1);
        }
    }
}
