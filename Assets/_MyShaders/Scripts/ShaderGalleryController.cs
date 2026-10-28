using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public sealed class ShaderGalleryController : MonoBehaviour
{
    public Camera view;
    public TideGate portal;
    public Vector3[] focusPoints;
    public Vector3[] shotPositions;
    public float moveSpeed = 6f;
    public bool showInterface = true;
    CharacterController controller;
    float yaw, pitch;
    int selected = -1;
    static readonly string[] Titles = { "01 / ENERGIA PULSANTE", "02 / DISOLUCION", "03 / AGUA ESTILIZADA", "04 / CAMPO DE FUERZA", "05 / HOLOGRAMA", "06 / UMBRAL DE MAREA" };
    static readonly string[] Notes = {
        "Time x Pulse Speed > Sine > Remap > HDR Emission. El Bloom hace visible el resplandor.",
        "Voronoi > Step > Alpha Clip. Dos umbrales aislan el borde HDR; el slider controla la desaparicion.",
        "Time x Flow Speed desplaza las UV. Gradient Noise > Normal From Height crea ondas sin mover vertices.",
        "Fresnel + celdas Voronoi animadas. Rim Power, Cell Density y Shield Color estan expuestos.",
        "UV.y > Multiply > Fraction > Step crea scanlines. Time desplaza las UV y Fresnel ilumina los bordes.",
        "Portal funcional: UV en dos direcciones + ruido + umbrales + Fresnel + pulsacion. R cambia su estado; atraviesalo para viajar." };

    void Awake()
    {
        Application.runInBackground = true;
        controller = GetComponent<CharacterController>();
        SyncAngles();
        TideGate.ResetCount();
    }
    void SyncAngles()
    {
        var angles = view.transform.rotation.eulerAngles;
        yaw = angles.y;
        pitch = angles.x > 180 ? angles.x - 360 : angles.x;
    }
    void Update()
    {
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;
        if (keyboard == null) return;
        for (int i = 0; i < 6; i++)
            if (keyboard[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame) Focus(i);
        if (keyboard.tabKey.wasPressedThisFrame) Overview();
        if (keyboard.rKey.wasPressedThisFrame && portal != null) portal.ToggleReady();
        if (keyboard.hKey.wasPressedThisFrame) showInterface = !showInterface;
        if (mouse != null && mouse.rightButton.isPressed)
        {
            var delta = mouse.delta.ReadValue();
            yaw += delta.x * 0.12f;
            pitch = Mathf.Clamp(pitch - delta.y * 0.12f, -80f, 80f);
            view.transform.rotation = Quaternion.Euler(pitch, yaw, 0);
        }
        Vector3 movement = Vector3.zero;
        if (keyboard.wKey.isPressed) movement += view.transform.forward;
        if (keyboard.sKey.isPressed) movement -= view.transform.forward;
        if (keyboard.dKey.isPressed) movement += view.transform.right;
        if (keyboard.aKey.isPressed) movement -= view.transform.right;
        if (keyboard.eKey.isPressed) movement += Vector3.up;
        if (keyboard.qKey.isPressed) movement -= Vector3.up;
        if (movement.sqrMagnitude > 0) ShowAllEffects();
        controller.Move(movement.normalized * moveSpeed * (keyboard.leftShiftKey.isPressed ? 2f : 1f) * Time.deltaTime);
    }
    public void Teleport(Vector3 position)
    {
        controller.enabled = false;
        transform.position = position;
        controller.enabled = true;
    }
    public void Focus(int index)
    {
        if (index < 0 || index >= focusPoints.Length || index >= shotPositions.Length) return;
        selected = index;
        view.cullingMask = 1 | (1 << (8 + index));
        Teleport(shotPositions[index]);
        view.transform.LookAt(focusPoints[index]);
        SyncAngles();
    }
    public void Overview()
    {
        selected = -1;
        view.cullingMask = ~0;
        Teleport(new Vector3(0, 13, -21));
        view.transform.LookAt(new Vector3(0, 1, 4));
        SyncAngles();
    }
    public void ShowAllEffects() { selected = -1; view.cullingMask = ~0; }
    void OnGUI()
    {
        if (!showInterface) return;
        float scale = Mathf.Clamp(Screen.width / 1400f, 0.65f, 1.6f);
        var saved = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * scale);
        float width = Screen.width / scale;
        GUI.color = new Color(0.025f, 0.04f, 0.075f, 0.94f);
        GUI.DrawTexture(new Rect(0, 0, width, 105), Texture2D.whiteTexture);
        GUI.color = new Color(0.25f, 0.95f, 0.86f);
        var title = new GUIStyle(GUI.skin.label) { fontSize = 23, fontStyle = FontStyle.Bold };
        GUI.Label(new Rect(24, 12, width - 40, 32), selected < 0 ? "SHANDER / SHADER MASTERY" : Titles[selected], title);
        GUI.color = Color.white;
        var body = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };
        GUI.Label(new Rect(24, 48, width - 48, 48), selected < 0 ? "NIVEL 1: Glow / Disolucion / Agua    |    NIVEL 2: Escudo / Holograma    |    NIVEL 3: Portal jugable" : Notes[selected], body);
        GUI.color = new Color(0.025f, 0.04f, 0.075f, 0.94f);
        GUI.DrawTexture(new Rect(0, Screen.height / scale - 48, width, 48), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(24, Screen.height / scale - 39, width - 48, 36), "1-6: primer plano    TAB: galeria    WASD: mover    Q/E: altura    Clic derecho: mirar    R: portal " + (portal != null && portal.Ready ? "ACTIVO" : "INACTIVO") + "    H: ocultar panel    Viajes: " + TideGate.TeleportCount, body);
        GUI.matrix = saved;
    }
}
