using UnityEngine;

// Animate a renderer override; the material's exposed slider remains editable.
public sealed class DissolveDemonstration : MonoBehaviour
{
    [SerializeField] bool animate = true;
    [SerializeField] float speed = 0.7f;
    Renderer target;
    MaterialPropertyBlock values;
    void Awake() { target = GetComponent<Renderer>(); values = new MaterialPropertyBlock(); }
    void Update()
    {
        if (!animate) return;
        target.GetPropertyBlock(values);
        values.SetFloat("_DissolveAmount", Mathf.Lerp(0.08f, 0.78f, (Mathf.Sin(Time.time * speed) + 1f) * 0.5f));
        target.SetPropertyBlock(values);
    }
    void OnDisable() { if (target != null) target.SetPropertyBlock(null); }
}
