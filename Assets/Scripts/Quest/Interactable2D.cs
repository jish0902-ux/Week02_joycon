using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public sealed class Interactable2D : MonoBehaviour
{
    [SerializeField] private string interactionId = "Box_A";
    public int IdHash { get; private set; }

    void Awake() => IdHash = Animator.StringToHash(interactionId);
    public string Id => interactionId;
}
