using UnityEngine;

public class TransparentWall : MonoBehaviour
{// 1. 유니티 에디터에서 반투명하게 만들 벽을 연결할 변수
    [Tooltip("투명하게 만들 벽의 Sprite Renderer를 여기에 연결하세요.")]
    public SpriteRenderer wallSpriteRenderer;

    // 2. 얼마나 투명하게 만들지 설정 (0 = 완전 투명, 1 = 완전 불투명)
    [Tooltip("벽이 얼마나 투명해질지 설정합니다. (0.0 ~ 1.0)")]
    [Range(0f, 1f)]
    public float transparencyLevel = 0.5f;

    private Color originalColor; // 벽의 원래 색상을 저장할 변수

    void Start()
    {
        // 3. 스크립트가 시작될 때, 벽의 원래 색상을 저장해 둡니다.
        if (wallSpriteRenderer != null)
        {
            originalColor = wallSpriteRenderer.color;
        }
        else
        {
            Debug.LogError("Wall Sprite Renderer가 연결되지 않았습니다! 인스펙터 창에서 연결해주세요.");
        }
    }

    // 4. 트리거 안에 무언가 들어왔을 때 호출되는 함수
    private void OnTriggerEnter2D(Collider2D other)
    {
        // 들어온 오브젝트의 태그(Tag)가 "Player"인지 확인합니다.
        if (other.CompareTag("Player"))
        {
            // 새로운 투명한 색상을 만듭니다.
            Color newColor = new Color(originalColor.r, originalColor.g, originalColor.b, transparencyLevel);
            // 벽의 색상을 투명하게 변경합니다.
            wallSpriteRenderer.color = newColor;
        }
    }

    // 5. 트리거에서 무언가 나갔을 때 호출되는 함수
    private void OnTriggerExit2D(Collider2D other)
    {
        // 나간 오브젝트의 태그(Tag)가 "Player"인지 확인합니다.
        if (other.CompareTag("Player"))
        {
            // 벽의 색상을 원래대로 되돌립니다.
            wallSpriteRenderer.color = originalColor;
        }
    }
}
