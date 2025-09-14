using UnityEngine;

public class ComponentOnOff : MonoBehaviour
{
    public MonoBehaviour targetScript;
    public bool conditionMet = false;

    void Update()
    {
        if (conditionMet && !targetScript.enabled)
        {
            targetScript.enabled = true; // 조건을 만족하면 스크립트 켜짐
        }

    }
}
