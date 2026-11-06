using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AI; // ★ NavMeshAgent 制御用に追加

public class BossHealth : MonoBehaviour
{
    [Header("ステータス設定")]
    public float maxHealth = 100f;
    public float currentHealth;

    [Header("UI参照")]
    public Slider bossHPSlider;

    [Header("死亡時設定")]
    [Tooltip("死亡アニメーションが完了してから消滅するまでの時間（秒）")]
    public float destroyDelay = 3.5f;

    private Animator animator;
    private NavMeshAgent agent;
    private BossRadagonAI bossAI;
    private bool isDead = false; // 二重死亡処理の防止フラグ

    void Start()
    {
        currentHealth = maxHealth;
        UpdateUI();

        // ★ 必要なコンポーネントを自動取得
        animator = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();
        bossAI = GetComponent<BossRadagonAI>();
    }

    public void TakeDamage(float damage)
    {
        if (isDead) return; // 既に死亡している場合はダメージ処理を行わない

        currentHealth -= damage;
        currentHealth = Mathf.Max(currentHealth, 0f);

        UpdateUI();

        Debug.Log("ボスに " + damage + " ダメージ！ 残りHP: " + currentHealth);

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void UpdateUI()
    {
        if (bossHPSlider != null)
        {
            bossHPSlider.maxValue = maxHealth;
            bossHPSlider.value = currentHealth;
        }
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        Debug.Log("ボス撃破！");

        // 1. HPゲージ非表示
        if (bossHPSlider != null)
        {
            bossHPSlider.gameObject.SetActive(false);
        }

        // 2. AI思考と移動ナビゲーションを完全停止
        if (bossAI != null)
        {
            bossAI.StopAllCoroutines(); // 実行中の攻撃ルーチンを強制停止
            bossAI.enabled = false;     // AIスクリプトをオフ
        }

        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            agent.enabled = false;     // 当たり判定や押し出しを解除
        }

        // 3. 死亡アニメーション（Die）を呼び出し
        if (animator != null)
        {
            animator.SetTrigger("Die");
        }

        // 4. アニメーションが十分流れた数秒後にオブジェクトを破棄
        Destroy(gameObject, destroyDelay);
    }
}