using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class BossRadagonAI : MonoBehaviour
{
    private NavMeshAgent agent;
    private Transform player;
    private PlayerController playerController;
    private BossHealth bossHealth;
    private Animator animator;

    // ==========================================
    // 攻撃ジャンルごとの設定構造体
    // ==========================================
    [System.Serializable]
    public class MeleeAttackSettings
    {
        [Tooltip("通常近接のダメージ")]
        public float damage = 25f;
        [Tooltip("強攻撃・叩きつけのダメージ")]
        public float heavyDamage = 45f;
        [Tooltip("攻撃が届く距離")]
        public float attackRange = 3.5f;
    }

    [System.Serializable]
    public class RangedAttackSettings
    {
        [Tooltip("遠距離攻撃のダメージ")]
        public float damage = 20f;
        [Tooltip("遠距離攻撃を開始する距離")]
        public float minRange = 8f;
        [Tooltip("発射する弾丸・光弾プレハブ")]
        public GameObject projectilePrefab;
        [Tooltip("弾の生成・発射位置")]
        public Transform spawnPoint;
    }

    [System.Serializable]
    public class AreaAttackSettings
    {
        [Tooltip("範囲攻撃（地面叩きつけ等）のダメージ")]
        public float damage = 50f;
        [Tooltip("範囲攻撃を発動する距離")]
        public float triggerRange = 5f;
        [Tooltip("攻撃の有効加害半径")]
        public float radius = 6f;
        [Tooltip("発生させる範囲エフェクト（爆発・衝撃波等）")]
        public GameObject areaVfxPrefab;
        [Tooltip("エフェクト発生位置（足元等）")]
        public Transform areaPoint;
    }

    [System.Serializable]
    public class LaserAttackSettings
    {
        [Tooltip("レーザーの1ヒットあたりのダメージ")]
        public float damagePerTick = 15f;
        [Tooltip("レーザーの有効射程距離")]
        public float maxDistance = 15f;
        [Tooltip("照射時間（秒）")]
        public float duration = 2.0f;
        [Tooltip("レーザー用LineRenderer（または判定エフェクト）")]
        public LineRenderer laserLine;
        [Tooltip("レーザー発射位置（右手・武器先端等）")]
        public Transform firingPoint;
    }

    // ==========================================
    // Inspector 設定項目
    // ==========================================
    [Header("=== 1. 近接攻撃設定 ===")]
    public MeleeAttackSettings meleeAttack;

    [Header("=== 2. 遠距離攻撃設定 ===")]
    public RangedAttackSettings rangedAttack;

    [Header("=== 3. 範囲攻撃（AoE）設定 ===")]
    public AreaAttackSettings areaAttack;

    [Header("=== 4. レーザー攻撃設定 ===")]
    public LaserAttackSettings laserAttack;

    [Header("=== 共通・フェーズ設定 ===")]
    [Tooltip("攻撃と攻撃の間のインターバル時間")]
    public float attackInterval = 2.0f;
    public bool isPhase2 = false;
    public float phase2HpThreshold = 0.5f;

    private float attackTimer = 0f;
    private bool isPerformingAction = false;
    private AttackCategory currentAttack = AttackCategory.None;

    public enum AttackCategory
    {
        None,
        MeleeNormal,
        MeleeHeavy,
        Ranged,
        Area,
        Laser
    }

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        bossHealth = GetComponent<BossHealth>();

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
            playerController = playerObj.GetComponent<PlayerController>();
        }

        if (laserAttack.laserLine != null)
        {
            laserAttack.laserLine.enabled = false;
        }

        isPerformingAction = false;
    }

    void Update()
    {
        if (player == null || isPerformingAction) return;

        CheckPhaseTransition();

        float distance = Vector3.Distance(transform.position, player.position);

        // 1. 移動・追尾処理
        if (agent != null && agent.isOnNavMesh)
        {
            float stopDistance = isPhase2 ? meleeAttack.attackRange : meleeAttack.attackRange;
            if (distance > stopDistance)
            {
                agent.isStopped = false;
                agent.SetDestination(player.position);
                if (animator != null) animator.SetFloat("Speed", agent.velocity.magnitude);
            }
            else
            {
                agent.isStopped = true;
                LookAtPlayer();
                if (animator != null) animator.SetFloat("Speed", 0f);
            }
        }

        // 2. 思考ルーチン（距離とフェーズに応じてジャンルを選択）
        if (attackTimer > 0)
        {
            attackTimer -= Time.deltaTime;
        }
        else
        {
            ChooseAndExecuteAttack(distance);
            attackTimer = attackInterval;
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

    private void ChooseAndExecuteAttack(float distance)
    {
        List<AttackCategory> validAttacks = new List<AttackCategory>();

        // 【テスト用】距離やフェーズに関係なく全攻撃を抽選候補に入れる
        validAttacks.Add(AttackCategory.MeleeNormal);
        validAttacks.Add(AttackCategory.MeleeHeavy);
        validAttacks.Add(AttackCategory.Ranged);
        validAttacks.Add(AttackCategory.Area);
        validAttacks.Add(AttackCategory.Laser);

        if (validAttacks.Count == 0) return;

        // 全攻撃の中からランダムに選択
        currentAttack = validAttacks[Random.Range(0, validAttacks.Count)];
        StartCoroutine(ExecuteAttackRoutine(currentAttack));
    }

    private IEnumerator ExecuteAttackRoutine(AttackCategory attack)
    {
        isPerformingAction = true;
        if (agent != null && agent.isOnNavMesh) agent.isStopped = true;

        LookAtPlayer();

        if (animator != null)
        {
            switch (attack)
            {
                case AttackCategory.MeleeNormal:
                    animator.SetTrigger("Attack");
                    Invoke("AE_MeleeHit", 0.5f);
                    break;
                case AttackCategory.MeleeHeavy:
                    animator.SetTrigger("HeavyAttack");
                    Invoke("AE_MeleeHit", 0.5f);
                    break;
                case AttackCategory.Ranged:
                    animator.SetTrigger("RangedAttack");
                    Invoke("AE_ShootProjectile", 0.3f); // モーション開始0.3秒後に玉発射！
                    break;
                case AttackCategory.Area:
                    animator.SetTrigger("AreaAttack");
                    Invoke("AE_AreaImpact", 0.4f);      // モーション開始0.4秒後に足元爆破！
                    break;
                case AttackCategory.Laser:
                    animator.SetTrigger("LaserAttack");
                    Invoke("AE_StartLaser", 0.2f);      // モーション開始0.2秒後にレーザー照射！
                    break;
            }
        }

        // 保険タイマー（2.5秒後に次の行動を解禁）
        yield return new WaitForSeconds(2.5f);
        isPerformingAction = false;
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

    // ===================================================
    // ★ 各ジャンル用 Animation Events（アニメーションイベント）
    // ===================================================

    // 1. 近接判定（通常・強攻撃）
    public void AE_MeleeHit()
    {
        if (player == null) return;
        float dist = Vector3.Distance(transform.position, player.position);
        if (dist <= meleeAttack.attackRange + 1.5f && playerController != null)
        {
            float dmg = (currentAttack == AttackCategory.MeleeHeavy) ? meleeAttack.heavyDamage : meleeAttack.damage;
            playerController.TakeDamage(dmg);
        }
    }

    // 2. 遠距離攻撃（光弾等を発射）
    public void AE_ShootProjectile()
    {
        if (rangedAttack.projectilePrefab != null && rangedAttack.spawnPoint != null && player != null)
        {
            Vector3 targetDir = (player.position + Vector3.up * 1.0f - rangedAttack.spawnPoint.position).normalized;
            Instantiate(rangedAttack.projectilePrefab, rangedAttack.spawnPoint.position, Quaternion.LookRotation(targetDir));
        }
    }

    // 3. 範囲攻撃（足元の衝撃波・地面爆発）
    public void AE_AreaImpact()
    {
        Transform origin = (areaAttack.areaPoint != null) ? areaAttack.areaPoint : transform;

        // VFXエフェクト発生
        if (areaAttack.areaVfxPrefab != null)
        {
            Instantiate(areaAttack.areaVfxPrefab, origin.position, Quaternion.identity);
        }

        // 球状範囲でプレイヤーへダメージ
        Collider[] hits = Physics.OverlapSphere(origin.position, areaAttack.radius);
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Player") && playerController != null)
            {
                playerController.TakeDamage(areaAttack.damage);
            }
        }
    }

    // 4. レーザー攻撃（照射開始）
    public void AE_StartLaser()
    {
        StartCoroutine(LaserRoutine());
    }

    private IEnumerator LaserRoutine()
    {
        float timer = 0f;
        Transform origin = (laserAttack.firingPoint != null) ? laserAttack.firingPoint : transform;

        if (laserAttack.laserLine != null) laserAttack.laserLine.enabled = true;

        while (timer < laserAttack.duration)
        {
            timer += Time.deltaTime;
            LookAtPlayer();

            Vector3 startPos = origin.position;
            Vector3 targetPos = (player != null) ? player.position + Vector3.up * 1.0f : startPos + transform.forward * laserAttack.maxDistance;
            Vector3 dir = (targetPos - startPos).normalized;

            if (laserAttack.laserLine != null)
            {
                laserAttack.laserLine.SetPosition(0, startPos);
                laserAttack.laserLine.SetPosition(1, startPos + dir * laserAttack.maxDistance);
            }

            // レーザーのレイキャストヒット判定
            if (Physics.Raycast(startPos, dir, out RaycastHit hit, laserAttack.maxDistance))
            {
                if (hit.collider.CompareTag("Player") && playerController != null)
                {
                    playerController.TakeDamage(laserAttack.damagePerTick * Time.deltaTime);
                }
            }

            yield return null;
        }

        if (laserAttack.laserLine != null) laserAttack.laserLine.enabled = false;
    }

    // モーション終了
    public void AE_OnActionEnd()
    {
        isPerformingAction = false;
    }

    private void OnDrawGizmosSelected()
    {
        // 範囲攻撃のデバッグ表示（Scene画面で赤丸表示）
        if (areaAttack != null)
        {
            Gizmos.color = Color.yellow;
            Transform origin = (areaAttack.areaPoint != null) ? areaAttack.areaPoint : transform;
            Gizmos.DrawWireSphere(origin.position, areaAttack.radius);
        }
    }
}