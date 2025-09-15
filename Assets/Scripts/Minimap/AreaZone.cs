using System;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class AreaZone : MonoBehaviour
{
    public string displayName;


    private void Awake()
    {
    }

    void Reset()
    {
        var col = GetComponent<Collider2D>();
        if (col) col.isTrigger = true; // 감지용 트리거


    }
}
