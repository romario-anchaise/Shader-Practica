using UnityEngine;

// Gameplay prefab: the same readiness value drives the portal shader and teleport permission.
public sealed class TideGate : MonoBehaviour
{
    public Transform destination;
    public Renderer membrane;
    [SerializeField] bool ready = true;
    [SerializeField] float cooldown = 2f;
    float lastTeleport = -10f;
    MaterialPropertyBlock values;
    public bool Ready => ready;
    public static int TeleportCount { get; private set; }
    public static void ResetCount() { TeleportCount = 0; }
    void OnEnable() { ApplyState(); }
    public void ToggleReady() { ready = !ready; ApplyState(); }
    void ApplyState()
    {
        if (membrane == null) return;
        if (values == null) values = new MaterialPropertyBlock();
        membrane.GetPropertyBlock(values);
        values.SetFloat("_Readiness", ready ? 1f : 0f);
        membrane.SetPropertyBlock(values);
    }
    void OnTriggerEnter(Collider other) { TryTeleport(other); }
    void OnTriggerStay(Collider other) { TryTeleport(other); }
    void TryTeleport(Collider other)
    {
        if (!ready || destination == null || Time.time - lastTeleport < cooldown) return;
        var player = other.GetComponentInParent<ShaderGalleryController>();
        if (player == null) return;
        lastTeleport = Time.time;
        player.ShowAllEffects();
        player.Teleport(destination.position);
        TeleportCount++;
    }
}
