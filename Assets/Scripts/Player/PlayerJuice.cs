using UnityEngine;
using System.Collections;

public class PlayerJuice : MonoBehaviour
{
    //스쿼시&스트레치를 적용할 실제 스프라이트 오브젝트의 Transform
    [SerializeField] private Transform spriteTransform;

    [SerializeField] private Vector3 jumpSqueeze = new Vector3(0.8f, 1.2f, 1f);
    [SerializeField] private Vector3 landSqueeze = new Vector3(1.2f, 0.8f, 1f);
    [SerializeField] private float squeezeDuration = 0.1f;

    [SerializeField] private float landEffectCooldown = 0.2f;
    private float lastLandTIme;

    private Controller2D controller;

    private bool wasOnGround; //이전 프레임에 땅에 있었는지
    private bool isSqueezing = false; //현재 스퀴즈 효과가 재생 중인지
    private Vector3 originalScale;

    private void Start()
    {
        controller = GetComponent<Controller2D>();

        if(spriteTransform == null)
        {
            Transform foundSprite = transform.Find("Sprite");
            if (foundSprite != null) { spriteTransform = foundSprite; }
            else { this.enabled = false; }
            
        }
        originalScale = spriteTransform.localScale;
        wasOnGround = controller.collisions.below;
    }

    private void Update()
    {
        bool landedThisFrame = !wasOnGround && controller.collisions.below;

        if (landedThisFrame && Time.time - lastLandTIme > landEffectCooldown) 
        {
            lastLandTIme = Time.time;
            PlayLandEffects();
        }//착지 효과
        wasOnGround = controller.collisions.below;

    }

    public void PlayJumpEffects() //Player.cs가 호출
    {
        StartSqueeze(jumpSqueeze.x, jumpSqueeze.y, squeezeDuration);

        //점프 파티클, 사운드 이펙트 추가 예정
    }

    private void PlayLandEffects()
    {
        StartSqueeze(landSqueeze.x, landSqueeze.y, squeezeDuration);

        //착지 파티클, 사운드 이펙트 추가 예정
    }

    private void StartSqueeze(float xSqueeze, float ySqueeze, float duration)
    {
        if(!isSqueezing) { StartCoroutine(SqueezeCoroutine(xSqueeze, ySqueeze, duration)); }
    }

    private IEnumerator SqueezeCoroutine(float xSqueeze, float ySqueeze,float duration)
    {
        isSqueezing = true;

        Vector3 squeezeScale = new Vector3(xSqueeze, ySqueeze, originalScale.z);
        spriteTransform.localScale = squeezeScale;

        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            float percent = timer / duration;
            spriteTransform.localScale = Vector3.Lerp(squeezeScale, originalScale, percent);
            yield return null;
        }
        spriteTransform.localScale = originalScale;

        isSqueezing = false;
    }


}
