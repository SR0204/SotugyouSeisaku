using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DestroySelf : MonoBehaviour
{
    [Tooltip("Á–Å‚·‚é‚Ü‚Å‚ÌŠÔi•bj")]
    public float lifetime = 1.5f;

    void Start()
    {
        Destroy(gameObject, lifetime);
    }
}
