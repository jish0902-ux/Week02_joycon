using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class InteractWorldPrompt : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] Camera worldCam;        // 없으면 Camera.main
    [SerializeField] CanvasGroup cg;
    [SerializeField] TMP_Text label;
    [SerializeField] Image holdProgress;     // Image.type = Filled, FillMethod = Radial360

    Transform _follow;
    Vector3 _offset;
    bool _showProg;

    void Awake()
    {
        if (!worldCam) worldCam = Camera.main;
        if (cg) { cg.alpha = 0f; cg.interactable = false; cg.blocksRaycasts = false; }
        if (holdProgress) holdProgress.fillAmount = 0f;
    }

    public void Show(Transform follow, Vector3 offset, string text, bool showProgress)
    {
        _follow = follow;
        _offset = offset;
        _showProg = showProgress;
        if (label) label.text = text;
        if (holdProgress) { holdProgress.fillAmount = 0f; holdProgress.enabled = showProgress; }
        if (cg) cg.alpha = 1f;
        Update();
    }

    public void Hide()
    {
        _follow = null;
        if (cg) cg.alpha = 0f;
        if (holdProgress) holdProgress.fillAmount = 0f;
    }

    public void SetProgress01(float t)
    {
        if (_showProg && holdProgress) holdProgress.fillAmount = Mathf.Clamp01(t);
    }

    void LateUpdate() => Update();

    void Update()
    {
        if (!_follow) return;
        var wp = _follow.position + _offset;
        transform.position = wp;         // 월드 스페이스 캔버스 기준: 그냥 위치만 따라감
        transform.forward = worldCam ? (transform.position - worldCam.transform.position).normalized
                                     : Vector3.forward; // 빌보드
    }
}
