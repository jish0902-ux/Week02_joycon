using UnityEngine;
using TMPro;
using System;



public class AreaNameUI : MonoBehaviour
{
 

    public TextMeshProUGUI label;

    void Awake()
    {
        if (!label) label = GetComponent<TextMeshProUGUI>();

    }

    public void SetAreaName(string name)
    {
        if (!label) return;
        label.text = string.IsNullOrEmpty(name) ? "" : name;
    }






}
