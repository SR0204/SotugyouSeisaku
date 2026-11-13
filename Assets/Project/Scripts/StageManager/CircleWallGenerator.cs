using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CircleWallGenerator : MonoBehaviour
{
    [Header("配置する柱・壁・天井のプレハブ")]
    public GameObject wallPrefab;
    public GameObject ceilingPrefab; // ★ 追加：天井プレハブ

    [Header("円形配置の設定")]
    [Tooltip("並べる本数")]
    public int count = 16;
    [Tooltip("ステージ中心からの半径（フロアのフチに合わせる）")]
    public float radius = 18f;
    [Tooltip("壁の高さのオフセット")]
    public float heightOffset = 3f;
    [Tooltip("天井の高さ")]
    public float ceilingHeight = 10.5f; // ★ 追加：天井の高さ

    [ContextMenu("周囲に自動配置する")]
    public void GenerateCircle()
    {
        if (wallPrefab == null)
        {
            Debug.LogWarning("wallPrefab を設定してください！");
            return;
        }

        // すでに生成済みの子オブジェクトがあれば削除して作り直す
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }

        // 1. 周囲の壁を円形に配置
        for (int i = 0; i < count; i++)
        {
            float angle = i * Mathf.PI * 2f / count;
            Vector3 pos = new Vector3(Mathf.Cos(angle) * radius, heightOffset, Mathf.Sin(angle) * radius);

            GameObject obj = Instantiate(wallPrefab, transform);
            obj.transform.localPosition = pos;
            // 中心（大祝福）の方を向かせる
            obj.transform.LookAt(new Vector3(transform.position.x, obj.transform.position.y, transform.position.z));
        }

        // 2. ★ 追加：天井プレハブが設定されていれば、上にフタをするように自動配置
        if (ceilingPrefab != null)
        {
            GameObject ceiling = Instantiate(ceilingPrefab, transform);
            ceiling.transform.localPosition = new Vector3(0f, ceilingHeight, 0f);
        }
    }
}