using UnityEngine;
using static Unity.Burst.Intrinsics.Arm;

public class SlingShoot : MonoBehaviour
{
    public bool GetOn;
    public GameObject player;
    void Awake()
    {
        player = GameObject.FindGameObjectWithTag("Player");
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (GetOn)
        {
            player.transform.position = transform.position + Vector3.up * 0.1f;
            //if (input)
        }
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            GetOn=true;
            transform.position = collision.transform.position;
        }
    }
    void Jump()
    {

    }
}
