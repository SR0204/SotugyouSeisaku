using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpellBullet : MonoBehaviour
{
    [Header("魔法設定")]
    public float damage = 35f;        // 魔法のダメージ量
    public float destroyTime = 5.0f;  // 寿命（秒）

    [Header("回避検知設定")]
    [Tooltip("ボスがこの距離（メートル）まで近づいたら回避行動を起こす")]
    public float dodgeTriggerDistance = 4.0f; // 3~5mがジャスト回避にベスト

    private bool hasTriggeredDodge = false;
    private BossRadagonAI targetBoss;

    void Start()
    {
        // 一定時間経過したら自動破棄
        Destroy(gameObject, destroyTime);

        // シーン上のボスを取得
        targetBoss = FindObjectOfType<BossRadagonAI>();
    }

    void Update()
    {
        // すでに回避判定済み、またはボスが居ない場合はスキップ
        if (hasTriggeredDodge || targetBoss == null) return;

        // ボスとの距離を計算
        float distanceToBoss = Vector3.Distance(transform.position, targetBoss.transform.position);

        // ★ 指定距離以内に迫ったらボスの直前回避をキック！
        if (distanceToBoss <= dodgeTriggerDistance)
        {
            hasTriggeredDodge = true; // 1発の魔法で何度も回避判定が出ないようガード
            targetBoss.DodgeIncomingSpell();
        }
    }

    // Is Trigger にチェックが入っているコライダーと接触した時に呼ばれる
    private void OnTriggerEnter(Collider other)
    {
        // プレイヤー自身やトリガー判定は無視
        if (other.CompareTag("Player") || other.isTrigger) return;

        // ★ Boss タグに当たった場合のダメージ処理
        if (other.CompareTag("Boss"))
        {
            BossHealth bossHealth = other.GetComponentInParent<BossHealth>();
            if (bossHealth != null)
            {
                bossHealth.TakeDamage(damage);
            }

            Destroy(gameObject); // 着弾して消去
            return;
        }

        // 地面や壁（Defaultレイヤーなど）に当たったら消去
        if (other.gameObject.layer == LayerMask.NameToLayer("Default"))
        {
            Destroy(gameObject);
        }
    }
}