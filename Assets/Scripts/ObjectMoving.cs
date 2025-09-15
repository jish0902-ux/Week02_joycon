using UnityEngine;

public class ObjectMoving : MonoBehaviour
{
    public Transform target;      // 가리킬 목표 위치 (Inspector에 드래그)
    public float speed = 2f;      // 이동 속도 (units/sec)
    public float stopDistance = 0.1f;

    void Update()
    {
        if (target == null) return;

        Vector3 dir = target.position - transform.position;
        if (dir.magnitude <= stopDistance) return; // 도달하면 멈춤

        transform.position = Vector3.MoveTowards(transform.position, target.position, speed * Time.deltaTime);
    }
}
