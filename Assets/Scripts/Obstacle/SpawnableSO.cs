// Assets/Scripts/Spawn/SpawnableSO.cs
using UnityEngine;

[CreateAssetMenu(menuName = "Spawn/Spawnable Object")]
public sealed class SpawnableSO : ScriptableObject
{
    [Header("Prefab")]
    public GameObject prefab;

    [Header("Weight / Prewarm")]
    [Min(0f)] public float weight = 1f;   // 가중치(랜덤 선택용)
    [Min(0)] public int prewarm = 0;     // 풀 미리 채울 개수

    [Header("Placement")]
    public Vector2 localOffset = Vector2.zero;  // 스폰 시 추가 오프셋(로컬 기준)
    public float rotation = 0;  // 회전 소환
    public bool alignToHitNormal = true;     // 충돌 면 법선에 회전 정렬할지
    [Range(0f, 0.5f)] public float surfaceOffset = 0.02f; // 표면에서 띄우기
}
