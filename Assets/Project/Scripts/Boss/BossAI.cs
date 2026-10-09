using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class BossAI : MonoBehaviour
{
    private NavMeshAgent agent;
    private Transform player;
    private PlayerController playerController;
    private BossHealth bossHealth;
    private Animator animator;

    [Header("=== ボス状態設定 ===")]
    public bool isPhase2 = false;
    public float phase2HpThreshold = 0.5f; // HP50%でフェーズ2移行

    // 各種設定構造体
    [System.Serializable]
    public struct MeleeCombo
    {
        public string name;
        public string animTrigger;      // アニメーションのトリガー名
        public float attackRange;       // 技の発動可能距離
        public float damage;
    }

    [System.Serializable]
    public struct AoEGroundSlam
    {
        public string name;
        public string animTrigger;
        public GameObject warningPrefab;
        public GameObject shockwavePrefab;
        public float radius;
        public float damage;
    }

    [Header("=== 近接・緩急攻撃（槌・剣） ===")]
    [SerializeField] private MeleeCombo quickCombo = new MeleeCombo { name = "出の早い2連撃", animTrigger = "AttackQuick", attackRange = 3.5f, damage = 25f };
    [SerializeField] private MeleeCombo heavyDelayedSlam = new MeleeCombo { name = "超ディレイ振り下ろし", animTrigger = "AttackDelayed", attackRange = 4.0f, damage = 60f };

    [Header("=== 範囲衝撃波（ラダゴン風叩きつけ） ===")]
    [SerializeField] private AoEGroundSlam groundSlam = new AoEGroundSlam { name = "黄金の衝撃波", animTrigger = "SlamAoE", radius = 8.0f, damage = 50f };

    [Header("=== 遠距離・弾幕設定 ===")]
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private Transform firePoint;

    [Header("=== レーザー設定 ===")]
    [SerializeField] private GameObject laserPrefab;

    private float attackTimer = 0f;
    private bool isPerformingAction = false;
    private Vector3 currentTargetPos;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
        bossHealth = GetComponent<BossHealth>();

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
            playerController = playerObj.GetComponent<PlayerController>();
        }
    }

    void Update()
    {
        if (player == null || isPerformingAction) return;

        // フェーズチェック
        CheckPhaseTransition();

        float distance = Vector3.Distance(transform.position, player.position);

        // 追跡処理
        if (agent != null && agent.isOnNavMesh)
        {
            if (distance > quickCombo.attackRange)
            {
                agent.isStopped = false;
                agent.SetDestination(player.position);
            }
            else
            {
                agent.isStopped = true;
                LookAtPlayer();
            }
        }

        // 攻撃タイマー
        if (attackTimer > 0)
        {
            attackTimer -= Time.deltaTime;
        }
        else
        {
            ChooseAction(distance);
            attackTimer = 2.0f; // 次の攻撃までのインターバル
        }
    }

    private void CheckPhaseTransition()
    {
        if (!isPhase2 && bossHealth != null)
        {
            // currentHp ➔ currentHealth、maxHp ➔ maxHealth に変更
            if (bossHealth.currentHealth / bossHealth.maxHealth <= phase2HpThreshold)
            {
                isPhase2 = true;
                // フェーズ2移行時のトリガー
                if (animator != null) animator.SetTrigger("Phase2Transition");
            }
        }
    }

    private void ChooseAction(float distance)
    {
        List<string> availableTriggers = new List<string>();

        // 【近距離】
        if (distance <= quickCombo.attackRange)
        {
            if (!string.IsNullOrEmpty(quickCombo.animTrigger))
                availableTriggers.Add(quickCombo.animTrigger);

            if (!string.IsNullOrEmpty(heavyDelayedSlam.animTrigger))
                availableTriggers.Add(heavyDelayedSlam.animTrigger);

            if (!string.IsNullOrEmpty(groundSlam.animTrigger))
                availableTriggers.Add(groundSlam.animTrigger);
        }
        // 【中・遠距離】
        else
        {
            if (!string.IsNullOrEmpty(groundSlam.animTrigger))
                availableTriggers.Add(groundSlam.animTrigger);

            if (!string.IsNullOrEmpty(quickCombo.animTrigger))
                availableTriggers.Add(quickCombo.animTrigger);
        }

        if (availableTriggers.Count == 0) return;

        // ランダムに攻撃トリガーを選んで実行
        string selectedTrigger = availableTriggers[Random.Range(0, availableTriggers.Count)];
        ExecuteAttack(selectedTrigger);
    }

    private void ExecuteAttack(string animTrigger)
    {
        isPerformingAction = true;
        if (agent != null && agent.isOnNavMesh) agent.isStopped = true;

        LookAtPlayer();
        if (animator != null) animator.SetTrigger(animTrigger);
    }

    // ===================================================
    // ★ アニメーションイベント（Animation Events）から呼ばれるメソッド
    // ===================================================

    // アニメーション内で「武器が届く瞬間」に呼び出す
    public void AE_MeleeHit()
    {
        if (player == null) return;
        float dist = Vector3.Distance(transform.position, player.position);
        if (dist <= quickCombo.attackRange + 1.0f)
        {
            if (playerController != null) playerController.TakeDamage(quickCombo.damage);
        }
    }

    // 叩きつけ（地面を殴った瞬間）に呼び出す
    public void AE_GroundSlamImpact()
    {
        if (groundSlam.shockwavePrefab != null)
        {
            Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position + transform.forward * 2f;
            spawnPos.y = 0.05f; // 地面スレスレ
            Instantiate(groundSlam.shockwavePrefab, spawnPos, transform.rotation);
        }
    }

    // 弾を発射する瞬間に呼び出す
    public void AE_FireRangedBullet()
    {
        if (bulletPrefab == null) return;
        Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position + transform.forward + Vector3.up;
        Quaternion rot = GetTargetRotation(spawnPos);
        Instantiate(bulletPrefab, spawnPos, rot);
    }

    // 4. レーザー攻撃（照射開始）
    public void AE_StartLaser()
    {
        Transform point = firePoint != null ? firePoint : transform;

        // firePoint（CastPoint）に付いている LaserBeam スクリプトを探して実行
        LaserBeam laserBeam = point.GetComponent<LaserBeam>();
        if (laserBeam != null)
        {
            // FireLaser(予兆時間, 本照射時間, レーザー太さ, 距離, 毎秒ダメージ, 回転角度)
            laserBeam.FireLaser(0.5f, 2.0f, 0.5f, 15f, 30f, 360f);
        }
        else if (laserPrefab != null)
        {
            // LaserBeamが無い場合はプレハブを生成
            Instantiate(laserPrefab, point.position, point.rotation);
        }
    }

    // アニメーションが完全に終了した瞬間に呼び出す（行動可能に戻す）
    public void AE_OnActionEnd()
    {
        isPerformingAction = false;
    }

    private Quaternion GetTargetRotation(Vector3 spawnPos)
    {
        Vector3 targetDirection = (player.position + Vector3.up * 1.0f) - spawnPos;
        return Quaternion.LookRotation(targetDirection);
    }

    private void LookAtPlayer()
    {
        Vector3 direction = (player.position - transform.position).normalized;
        direction.y = 0;
        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }
    }
}