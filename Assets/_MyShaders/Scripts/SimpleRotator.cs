using UnityEngine;

public sealed class SimpleRotator : MonoBehaviour
{
    [SerializeField] Vector3 axis = Vector3.up;
    [SerializeField] float speed = 30f;
    void Update() { transform.Rotate(axis * speed * Time.deltaTime, Space.Self); }
}
