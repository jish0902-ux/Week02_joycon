using UnityEngine;
using UnityEngine.Rendering.Universal;

public class LightSwitch : MonoBehaviour, IInteractable
{
    [SerializeField] private Light2D targetLight;
    private bool isLightOn = false;

    private void Awake()
    {
        if(targetLight != null)
        {
            targetLight.enabled = false;
        }
    }
    public void Interact()
    {
        if (isLightOn || targetLight == null) return;
        targetLight.enabled = true;
        isLightOn = true;
        Debug.Log($"{gameObject.name}À» Ä×½À´Ï´Ù");
    }
}
