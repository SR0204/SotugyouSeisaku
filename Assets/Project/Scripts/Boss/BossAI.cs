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
    public float phase2HpThreshold = 0.5f;

    [System.Serializable]
    public struct MeleeCombo
    {
        public string name;
        public string animTrigger;
        public float attackRange;
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
    [SerializeField] private MeleeCombo quickCombo = new MeleeCombo { name = "通常近接", animTrigger = "Attack", attackRange = 3.5f, damage = 25f };
    [SerializeField] private MeleeCombo heavyDelayedSlam = new MeleeCombo { name = "強攻撃", animTrigger = "HeavyAttack", attackRange = 4.0f, damage = 60f };

    [Header("=== 範囲衝撃波 ===")]
    [SerializeField] private AoEGroundSlam groundSlam = new AoEGroundSlam { name = "範囲爆発", animTrigger = "AreaAttack", radius = 8.0f, damage = 50f };

    [Header("=== 遠距離・弾幕設定 ===")]
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private Transform firePoint;

    [Header("=== レーザー設定 ===")]
    [SerializeField] private GameObject laserPrefab;

    private float attackTimer = 0f;
    private bool isPerformingAction = false;

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

        CheckPhaseTransition();

        float distance = Vector3.Distance(transform.position, player.position);

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

        if (attackTimer > 0)
        {
            attackTimer -= Time.deltaTime;
        }
        else
        {
            ChooseAction(distance);
            attackTimer = 2.0f;
        }
    }

    private void CheckPhaseTransition()
    {
        if (!isPhase2 && bossHealth != null)
        {
            if (bossHealth.currentHealth / bossHealth.maxHealth <= phase2HpThreshold)
            {
                isPhase2 = true;
                if (animator != null) animator.SetTrigger("Phase2Transition");
            }
        }
    }

    private void ChooseAction(float distance)
    {
        List<string> availableTriggers = new List<string>();

        // 全攻撃トリガーを一度リストに入れてランダム抽選（テスト用）
        availableTriggers.Add(quickCombo.animTrigger);      // "Attack"
        availableTriggers.Add(heavyDelayedSlam.animTrigger); // "HeavyAttack"
        availableTriggers.Add(groundSlam.animTrigger);       // "AreaAttack"
        availableTriggers.Add("RangedAttack");
        availableTriggers.Add("LaserAttack");

        if (availableTriggers.Count == 0) return;

        string selectedTrigger = availableTriggers[Random.Range(0, availableTriggers.Count)];
        ExecuteAttack(selectedTrigger);
    }

    private void ExecuteAttack(string animTrigger)
    {
        isPerformingAction = true;
        if (agent != null && agent.isOnNavMesh) agent.isStopped = true;

        LookAtPlayer();
        if (animator != null) animator.SetTrigger(animTrigger);

        // ★ モーションの振り終わりに合わせたタイミングで発動させる調整
        if (animTrigger == quickCombo.animTrigger)
        {
            Invoke("AE_MeleeHit", 0.6f); // 手を振り下ろす瞬間に合わせる
        }
        else if (animTrigger == heavyDelayedSlam.animTrigger)
        {
            Invoke("AE_MeleeHit", 0.9f); // ため動作の後
        }
        else if (animTrigger == groundSlam.animTrigger)
        {
            Invoke("AE_GroundSlamImpact", 0.7f); // 地面を殴った瞬間
        }
        else if (animTrigger == "RangedAttack")
        {
            Invoke("AE_FireRangedBullet", 0.5f); // 弾発射
        }
        else if (animTrigger == "LaserAttack")
        {
            Invoke("AE_StartLaser", 0.4f); // レーザー照射
        }

        // 行動解除のタイマー
        CancelInvoke("ResetActionState");
        Invoke("ResetActionState", 2.5f);
    }

    private void ResetActionState()
    {
        isPerformingAction = false;
    }

    public void AE_MeleeHit()
    {
        if (player == null) return;
        float dist = Vector3.Distance(transform.position, player.position);
        if (dist <= quickCombo.attackRange + 1.5f)
        {
            if (playerController != null) playerController.TakeDamage(quickCombo.damage);
        }
    }

    public void AE_GroundSlamImpact()
    {
        if (groundSlam.shockwavePrefab != null)
        {
            Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position + transform.forward * 2f;
            spawnPos.y = 0.05f;

            // 生成したエフェクトを変数に保持
            GameObject vfx = Instantiate(groundSlam.shockwavePrefab, spawnPos, transform.rotation);

            Destroy(vfx, 1.5f);
        }
    }

    public void AE_FireRangedBullet()
    {
        if (bulletPrefab == null) return;
        Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position + transform.forward + Vector3.up;
        Quaternion rot = GetTargetRotation(spawnPos);
        Instantiate(bulletPrefab, spawnPos, rot);
    }

    public void AE_StartLaser()
    {
        Transform point = firePoint != null ? firePoint : transform;
        LaserBeam laserBeam = point.GetComponent<LaserBeam>();
        if (laserBeam != null)
        {
            laserBeam.FireLaser(0.5f, 2.0f, 0.5f, 15f, 30f, 360f);
        }
        else if (laserPrefab != null)
        {
            Instantiate(laserPrefab, point.position, point.rotation);
        }
    }

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
        if (player == null) return;
        Vector3 direction = (player.position - transform.position).normalized;
        direction.y = 0;
        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }
    }
}