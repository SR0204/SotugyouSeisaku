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
        [Tooltip("弾の飛行スピード")]
        public float projectileSpeed = 15f;
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

    // ★ 各攻撃の有効/無効フラグ ＆ 発生確率（重み）設定用構造体
    [System.Serializable]
    public class AttackProbabilities
    {
        [Header("--- 近接攻撃 ---")]
        [Tooltip("通常近接攻撃を使用するか")]
        public bool enableMeleeNormal = true;
        [Tooltip("通常近接攻撃の発生確率（重み）")]
        public float meleeNormalWeight = 20f;

        [Tooltip("強近接攻撃を使用するか")]
        public bool enableMeleeHeavy = true;
        [Tooltip("強近接攻撃の発生確率（重み）")]
        public float meleeHeavyWeight = 15f;

        [Header("--- 遠距離（弾）攻撃 ---")]
        [Tooltip("単発弾を使用するか")]
        public bool enableRangedSingle = true;
        [Tooltip("単発弾の発生確率（重み）")]
        public float rangedSingleWeight = 10f;

        [Tooltip("連射弾を使用するか")]
        public bool enableRangedRapid = true;
        [Tooltip("連射弾の発生確率（重み）")]
        public float rangedRapidWeight = 10f;

        [Tooltip("拡散弾を使用するか")]
        public bool enableRangedSpread = true;
        [Tooltip("拡散弾の発生確率（重み）")]
        public float rangedSpreadWeight = 10f;

        [Header("--- 範囲攻撃 ---")]
        [Tooltip("範囲爆破攻撃を使用するか")]
        public bool enableAreaImpact = true;
        [Tooltip("範囲爆破攻撃の発生確率（重み）")]
        public float areaImpactWeight = 15f;

        [Header("--- レーザー攻撃 ---")]
        [Tooltip("直線レーザーを使用するか")]
        public bool enableLaserStraight = true;
        [Tooltip("直線レーザーの発生確率（重み）")]
        public float laserStraightWeight = 10f;

        [Tooltip("360度レーザーを使用するか")]
        public bool enableLaserSweep360 = true;
        [Tooltip("360度レーザーの発生確率（重み）")]
        public float laserSweep360Weight = 5f;

        [Tooltip("90度レーザーを使用するか")]
        public bool enableLaserSweep90 = true;
        [Tooltip("90度レーザーの発生確率（重み）")]
        public float laserSweep90Weight = 5f;
    }

    // ★ 追加：分裂（部位切断/サブボス生成）ギミック設定
    [System.Serializable]
    public class BossSplitSettings
    {
        [Tooltip("分裂を発生させるHPの割合（0.5 = 50%）")]
        public float splitHpThreshold = 0.5f;

        [Tooltip("出現させるサブボス（切断部位）のプレハブ")]
        public GameObject subBossPrefab;

        [Tooltip("本体からの出現オフセット位置")]
        public Vector3 spawnOffset = new Vector3(3f, 0f, 0f);

        [Tooltip("出現時のVFXエフェクト（任意）")]
        public GameObject spawnVfxPrefab;
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

    [Header("=== 5. 各攻撃の発生確率（重み）設定 ===")]
    public AttackProbabilities attackWeights;

    [Header("=== 6. ★ 分裂（百足のデーモン風）設定 ===")]
    public BossSplitSettings splitSettings;

    [Header("=== 共通・フェーズ設定 ===")]
    [Tooltip("攻撃と攻撃の間のインターバル時間")]
    public float attackInterval = 2.0f;
    public bool isPhase2 = false;
    public float phase2HpThreshold = 0.5f;

    private float attackTimer = 0f;
    private bool isPerformingAction = false;
    private bool hasSplit = false; // ★ 1度だけ分裂させるためのフラグ
    private AttackCategory currentAttack = AttackCategory.None;

    public enum AttackCategory
    {
        None,
        MeleeNormal,
        MeleeHeavy,
        Ranged,
        RangedRapid,
        RangedSpread,
        Area,
        LaserStraight,
        LaserSweep360,
        LaserSweep90
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

        isPerformingAction = false;
        hasSplit = false;
    }

    void Update()
    {
        if (player == null || isPerformingAction) return;

        CheckPhaseTransition();
        CheckBossSplit(); // ★ 分裂条件の毎フレーム監視

        float distance = Vector3.Distance(transform.position, player.position);

        // 1. 移動・追尾処理
        if (agent != null && agent.isOnNavMesh)
        {
            float stopDistance = meleeAttack.attackRange;
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

        // 2. 思考ルーチン
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

    // ★ 追加：HP50%以下になった時に部位切断/サブボスを生成する処理
    private void CheckBossSplit()
    {
        if (!hasSplit && bossHealth != null && bossHealth.maxHealth > 0)
        {
            float currentHpPercent = bossHealth.currentHealth / bossHealth.maxHealth;

            if (currentHpPercent <= splitSettings.splitHpThreshold)
            {
                hasSplit = true;
                ExecuteBossSplit();
            }
        }
    }

    // ★ サブボスの生成（スポーン）実行処理
    private void ExecuteBossSplit()
    {
        Debug.Log("<color=yellow>【百足のデーモン風ギミック】ボスが部位切断し、サブボスが分裂出現しました！</color>");

        if (splitSettings.subBossPrefab != null)
        {
            // 本体の回転に合わせたオフセット位置を算出
            Vector3 spawnPos = transform.position + transform.TransformDirection(splitSettings.spawnOffset);

            // サブボスを生成
            GameObject subBoss = Instantiate(splitSettings.subBossPrefab, spawnPos, transform.rotation);

            // 出現エフェクト（VFX）があれば生成
            if (splitSettings.spawnVfxPrefab != null)
            {
                Instantiate(splitSettings.spawnVfxPrefab, spawnPos, Quaternion.identity);
            }
        }
        else
        {
            Debug.LogWarning("BossRadagonAI: SubBossPrefab が Inspector 上で未割り当てです！");
        }
    }

    // ★ 重み付けを用いた確率判定で次の攻撃を決定（ON/OFFチェック機能付き）
    private void ChooseAndExecuteAttack(float distance)
    {
        Dictionary<AttackCategory, float> attackPool = new Dictionary<AttackCategory, float>();

        // チェックが入っており(true)、重みが0より大きい場合のみ候補に追加
        if (attackWeights.enableMeleeNormal && attackWeights.meleeNormalWeight > 0)
            attackPool.Add(AttackCategory.MeleeNormal, attackWeights.meleeNormalWeight);

        if (attackWeights.enableMeleeHeavy && attackWeights.meleeHeavyWeight > 0)
            attackPool.Add(AttackCategory.MeleeHeavy, attackWeights.meleeHeavyWeight);

        if (attackWeights.enableRangedSingle && attackWeights.rangedSingleWeight > 0)
            attackPool.Add(AttackCategory.Ranged, attackWeights.rangedSingleWeight);

        if (attackWeights.enableRangedRapid && attackWeights.rangedRapidWeight > 0)
            attackPool.Add(AttackCategory.RangedRapid, attackWeights.rangedRapidWeight);

        if (attackWeights.enableRangedSpread && attackWeights.rangedSpreadWeight > 0)
            attackPool.Add(AttackCategory.RangedSpread, attackWeights.rangedSpreadWeight);

        if (attackWeights.enableAreaImpact && attackWeights.areaImpactWeight > 0)
            attackPool.Add(AttackCategory.Area, attackWeights.areaImpactWeight);

        if (attackWeights.enableLaserStraight && attackWeights.laserStraightWeight > 0)
            attackPool.Add(AttackCategory.LaserStraight, attackWeights.laserStraightWeight);

        if (attackWeights.enableLaserSweep360 && attackWeights.laserSweep360Weight > 0)
            attackPool.Add(AttackCategory.LaserSweep360, attackWeights.laserSweep360Weight);

        if (attackWeights.enableLaserSweep90 && attackWeights.laserSweep90Weight > 0)
            attackPool.Add(AttackCategory.LaserSweep90, attackWeights.laserSweep90Weight);

        if (attackPool.Count == 0) return;

        // 全重みの合計を計算
        float totalWeight = 0f;
        foreach (var item in attackPool)
        {
            totalWeight += item.Value;
        }

        // 0 ～ totalWeight のランダムな数値を算出
        float randomValue = Random.Range(0f, totalWeight);
        float currentSum = 0f;

        // ランダム値が収まる位置の攻撃を選択
        foreach (var item in attackPool)
        {
            currentSum += item.Value;
            if (randomValue <= currentSum)
            {
                currentAttack = item.Key;
                break;
            }
        }

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
                    Invoke("AE_MeleeHit", 0.6f);
                    break;
                case AttackCategory.MeleeHeavy:
                    animator.SetTrigger("HeavyAttack");
                    Invoke("AE_MeleeHit", 0.9f);
                    break;
                case AttackCategory.Ranged:
                    animator.SetTrigger("RangedAttack");
                    Invoke("AE_ShootProjectile", 0.5f);
                    break;
                case AttackCategory.RangedRapid:
                    animator.SetTrigger("RangedAttack");
                    Invoke("AE_ShootProjectileRapid", 0.5f);
                    break;
                case AttackCategory.RangedSpread:
                    animator.SetTrigger("RangedAttack");
                    Invoke("AE_ShootProjectileSpread", 0.5f);
                    break;
                case AttackCategory.Area:
                    animator.SetTrigger("AreaAttack");
                    Invoke("AE_AreaImpact", 0.7f);
                    break;
                case AttackCategory.LaserStraight:
                    animator.SetTrigger("LaserAttack");
                    Invoke("AE_StartLaserStraight", 0.4f);
                    break;
                case AttackCategory.LaserSweep360:
                    animator.SetTrigger("LaserAttack");
                    Invoke("AE_StartLaser360", 0.4f);
                    break;
                case AttackCategory.LaserSweep90:
                    animator.SetTrigger("LaserAttack");
                    Invoke("AE_StartLaser90", 0.4f);
                    break;
            }
        }
        else
        {
            // アニメーターが付いていないオブジェクト（Cube等のモック）でも攻撃判定が発生するようにフォールバック処理
            switch (attack)
            {
                case AttackCategory.MeleeNormal:
                case AttackCategory.MeleeHeavy:
                    Invoke("AE_MeleeHit", 0.5f);
                    break;
                case AttackCategory.Ranged:
                    Invoke("AE_ShootProjectile", 0.5f);
                    break;
                case AttackCategory.RangedRapid:
                    Invoke("AE_ShootProjectileRapid", 0.5f);
                    break;
                case AttackCategory.RangedSpread:
                    Invoke("AE_ShootProjectileSpread", 0.5f);
                    break;
                case AttackCategory.Area:
                    Invoke("AE_AreaImpact", 0.5f);
                    break;
                case AttackCategory.LaserStraight:
                    Invoke("AE_StartLaserStraight", 0.4f);
                    break;
                case AttackCategory.LaserSweep360:
                    Invoke("AE_StartLaser360", 0.4f);
                    break;
                case AttackCategory.LaserSweep90:
                    Invoke("AE_StartLaser90", 0.4f);
                    break;
            }
        }

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
    // ★ 各ジャンル用 Animation Events / Invoke用メソッド
    // ===================================================

    /// 1. 近接判定（壁越しヒット防止機能 ＆ Trigger無視対応）
    public void AE_MeleeHit()
    {
        if (player == null) return;
        float dist = Vector3.Distance(transform.position, player.position);
        if (dist <= meleeAttack.attackRange + 1.5f && playerController != null)
        {
            Vector3 originPos = transform.position + Vector3.up * 1.0f; // 胸の高さ
            Vector3 targetPos = player.position + Vector3.up * 1.0f;     // プレイヤーの胸の高さ
            Vector3 dir = (targetPos - originPos).normalized;
            float checkDist = Vector3.Distance(originPos, targetPos);

            // ★ QueryTriggerInteraction.Ignore を追加して Trigger コライダー（自分自身）を無視
            if (Physics.Raycast(originPos, dir, out RaycastHit hit, checkDist, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (!hit.collider.CompareTag("Player"))
                {
                    // 壁や障害物に防がれたためダメージを与えない
                    return;
                }
            }

            float dmg = (currentAttack == AttackCategory.MeleeHeavy) ? meleeAttack.heavyDamage : meleeAttack.damage;
            playerController.TakeDamage(dmg);
        }
    }

    // 2. 遠距離攻撃（単発）
    public void AE_ShootProjectile()
    {
        SpawnSingleBullet(Quaternion.identity);
    }

    // 遠距離攻撃（連射）
    public void AE_ShootProjectileRapid()
    {
        StartCoroutine(RapidFireRoutine(3, 0.15f));
    }

    private IEnumerator RapidFireRoutine(int count, float interval)
    {
        for (int i = 0; i < count; i++)
        {
            SpawnSingleBullet(Quaternion.identity);
            yield return new WaitForSeconds(interval);
        }
    }

    // 遠距離攻撃（拡散）
    public void AE_ShootProjectileSpread()
    {
        SpawnSingleBullet(Quaternion.identity);
        SpawnSingleBullet(Quaternion.Euler(0, -15f, 0));
        SpawnSingleBullet(Quaternion.Euler(0, 15f, 0));
    }

    private void SpawnSingleBullet(Quaternion offsetRotation)
    {
        Transform spawnPoint = rangedAttack.spawnPoint != null ? rangedAttack.spawnPoint : transform;
        if (rangedAttack.projectilePrefab != null && player != null)
        {
            Vector3 spawnPos = spawnPoint.position + transform.forward * 0.8f;
            Vector3 baseDir = (player.position + Vector3.up * 1.0f - spawnPos).normalized;

            Vector3 finalDir = offsetRotation * baseDir;

            GameObject bullet = Instantiate(rangedAttack.projectilePrefab, spawnPos, Quaternion.LookRotation(finalDir));

            Rigidbody rb = bullet.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.velocity = finalDir * rangedAttack.projectileSpeed;
            }

            Destroy(bullet, 5.0f);
        }
    }

    // 3. 範囲攻撃（壁越しヒット防止機能 ＆ Trigger無視対応）
    public void AE_AreaImpact()
    {
        Transform origin = (areaAttack.areaPoint != null) ? areaAttack.areaPoint : transform;

        if (areaAttack.areaVfxPrefab != null)
        {
            GameObject vfx = Instantiate(areaAttack.areaVfxPrefab, origin.position, Quaternion.identity);
            StartCoroutine(FadeOutAndDestroy(vfx, 1.2f));
        }

        Collider[] hits = Physics.OverlapSphere(origin.position, areaAttack.radius);
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Player") && playerController != null)
            {
                Vector3 originPos = origin.position + Vector3.up * 0.5f;
                Vector3 targetPos = hit.transform.position + Vector3.up * 1.0f;
                Vector3 dir = (targetPos - originPos).normalized;
                float checkDist = Vector3.Distance(originPos, targetPos);

                // ★ QueryTriggerInteraction.Ignore を追加
                if (Physics.Raycast(originPos, dir, out RaycastHit hitInfo, checkDist, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    if (!hitInfo.collider.CompareTag("Player"))
                    {
                        continue;
                    }
                }

                playerController.TakeDamage(areaAttack.damage);
            }
        }
    }

    private IEnumerator FadeOutAndDestroy(GameObject target, float fadeDuration)
    {
        if (target == null) yield break;

        Vector3 originalScale = target.transform.localScale;
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
        float timer = 0f;

        while (timer < fadeDuration)
        {
            if (target == null) yield break;

            timer += Time.deltaTime;
            float progress = timer / fadeDuration;

            target.transform.localScale = Vector3.Lerp(originalScale, Vector3.zero, progress);

            foreach (var rend in renderers)
            {
                if (rend != null && rend.material != null && rend.material.HasProperty("_Color"))
                {
                    Color c = rend.material.color;
                    c.a = Mathf.Lerp(1f, 0f, progress);
                    rend.material.color = c;
                }
            }

            yield return null;
        }

        Destroy(target);
    }

    // 4. レーザー攻撃
    public void AE_StartLaserStraight()
    {
        Transform origin = (laserAttack.firingPoint != null) ? laserAttack.firingPoint : transform;
        LaserBeam laserBeam = origin.GetComponent<LaserBeam>();
        if (laserBeam != null)
        {
            laserBeam.FireLaser(0.8f, laserAttack.duration, 0.5f, laserAttack.maxDistance, laserAttack.damagePerTick, 0f);
        }
    }

    public void AE_StartLaser360()
    {
        Transform origin = (laserAttack.firingPoint != null) ? laserAttack.firingPoint : transform;
        LaserBeam laserBeam = origin.GetComponent<LaserBeam>();
        if (laserBeam != null)
        {
            laserBeam.FireLaser(0.3f, 0.5f, 0.5f, laserAttack.maxDistance, laserAttack.damagePerTick, 360f);
        }
    }

    public void AE_StartLaser90()
    {
        Transform origin = (laserAttack.firingPoint != null) ? laserAttack.firingPoint : transform;
        LaserBeam laserBeam = origin.GetComponent<LaserBeam>();
        if (laserBeam != null)
        {
            laserBeam.FireLaser(0.6f, laserAttack.duration, 0.5f, laserAttack.maxDistance, laserAttack.damagePerTick, 90f);
        }
    }

    public void AE_OnActionEnd()
    {
        isPerformingAction = false;
    }

    private void OnDrawGizmosSelected()
    {
        if (areaAttack != null)
        {
            Gizmos.color = Color.yellow;
            Transform origin = (areaAttack.areaPoint != null) ? areaAttack.areaPoint : transform;
            Gizmos.DrawWireSphere(origin.position, areaAttack.radius);
        }
    }
}