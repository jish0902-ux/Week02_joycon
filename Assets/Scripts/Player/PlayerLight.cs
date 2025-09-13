using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerLight : MonoBehaviour
{
    public float interactionRange = 1.5f;
    public LayerMask interactableMask;

    public void OnTnteract(InputAction.CallbackContext context)
    {
        if (context.performed) FindAndInteract();
    }

    private void FindAndInteract()
    {   //내 주변 interactionRange반경 안에 있는 interactableMask레이어 오브젝트 찾음
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, interactionRange, interactableMask);

        GameObject closestObj = null;
        float minDistance = Mathf.Infinity;

        foreach(Collider2D hit in hits)
        {
            float distance = Vector2.Distance(transform.position, hit.transform.position);
            if (distance < minDistance)
            {
                minDistance = distance;
                closestObj = hit.gameObject;
            }
        }
        if (closestObj != null && closestObj.TryGetComponent<IInteractable>(out IInteractable interactable)) interactable.Interact();

    }

    

}
