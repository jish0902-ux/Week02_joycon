using UnityEngine;

public class FPSLimiter : MonoBehaviour
{
    [SerializeField] private int targetFPS = 60;

    void Awake()
    {
        // VSync 끄기 (VSync가 켜져 있으면 Application.targetFrameRate가 무시됨)
        QualitySettings.vSyncCount = 0;

        // 목표 프레임 고정
        Application.targetFrameRate = targetFPS;
    }
}
