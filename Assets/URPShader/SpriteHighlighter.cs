using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class SpriteHighlighter : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private SpriteRenderer sr;

    [Header("Highlight Style")]
    [SerializeField, Range(0f, 8f)] private float onOutlineThickness = 2f;
    [SerializeField] private Color outlineColor = new Color(1f, 1f, 0f, 1f);
    [SerializeField, Range(0f, 5f)] private float onEmission = 1.2f;

    [Header("Anim")]
    [SerializeField, Range(0.02f, 1f)] private float fadeDuration = 0.12f;
    [SerializeField] private bool unscaledTime = true;

    MaterialPropertyBlock _mpb;
    float _t;            // 0=Off, 1=On
    float _targetT;      // 애니메이션 목표

    void Reset()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    void Awake()
    {
        if (!sr) sr = GetComponent<SpriteRenderer>();
        _mpb = new MaterialPropertyBlock();

        // 초기값 적용
        ApplyMPB(immediate: true);
    }

    void OnDisable()
    {
        if (sr != null) sr.SetPropertyBlock(null); // 깔끔하게 초기화
    }

    void Update()
    {
        if (Mathf.Approximately(_t, _targetT)) return;

        float dt = unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        float speed = (fadeDuration <= 0.001f) ? 1e9f : 1f / fadeDuration;
        _t = Mathf.MoveTowards(_t, _targetT, dt * speed);
        ApplyMPB(immediate: true);
    }

    /// <summary>하이라이트 켜기/끄기</summary>
    public void SetHighlight(bool on)
    {
        _targetT = on ? 1f : 0f;
        if (fadeDuration <= 0.001f) { _t = _targetT; ApplyMPB(immediate: true); }
    }

    /// <summary>강도(0~1)를 직접 지정</summary>
    public void SetHighlight01(float t)
    {
        _targetT = Mathf.Clamp01(t);
        if (fadeDuration <= 0.001f) { _t = _targetT; ApplyMPB(immediate: true); }
    }

    void ApplyMPB(bool immediate)
    {
        sr.GetPropertyBlock(_mpb);
        _mpb.SetFloat("_HighlightLerp", _t);
        _mpb.SetFloat("_OutlineThickness", onOutlineThickness);
        _mpb.SetFloat("_Emission", onEmission);
        _mpb.SetColor("_OutlineColor", outlineColor);
        sr.SetPropertyBlock(_mpb);
    }
}
