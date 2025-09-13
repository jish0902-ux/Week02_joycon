using UnityEngine;

public class SpriteSwitcher : MonoBehaviour, IInteractable
{
    [SerializeField] private GameObject spriteObjectA; //초기에 활성화 되어있는 오브젝트 연결
    [SerializeField] private GameObject spriteObjectB; //A를 비활성화하고 활성화 할 오브젝트 연결

    private bool hasSwithced = false;

    private void Start()
    {
        if (spriteObjectA != null)
        {
            spriteObjectA.SetActive(true);
        }if (spriteObjectB != null)
        {
            spriteObjectB.SetActive(false);
        }
    }

    public void Interact()
    {
        if (!hasSwithced)
        {
            if (spriteObjectA != null) { spriteObjectA.SetActive(false); }
            if (spriteObjectB != null) { spriteObjectB.SetActive(true); }
            hasSwithced=true;
            Debug.Log($"{spriteObjectA.name}를 비활성화하고 {spriteObjectB.name}을 활성화 했습니다.");
        }
    }

}
