using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BossMinionAI : MonoBehaviour
{
    public enum MinionType
    {
        ChaseContact,   // 追尾して接触ダメージ（基本タイプ）
        Exploder,       // 近づくとカウントダウンして自爆するタイプ
        RangedCaster    // 距離を保って魔法弾を撃つタイプ
    }

    [Header("=== ミニオン基本設定 ===")]
    public MinionType minionType = MinionType.ChaseContact;
    public float hp = 1.0f;
    public float damage = 10.0f;
    public float speed = 3.5f;
    [Tooltip("自動消滅するまでの寿命（秒）")]
    public float lifeTime = 15.0f;
    [Tooltip("接触ダメージが発生する距離（m）")]
    public float contactRange = 1.2f;

    [Header("=== 自爆タイプ（Exploder）設定 ===")]
    public float explodeTriggerRange = 2.2f;
    public float explodeRadius = 3.5f;
    public float explodeDelay = 0.8f;
    public GameObject explosionVfxPrefab;

    [Header("=== 遠距離タイプ（RangedCaster）設定 ===")]
    public float castRange = 10.0f;
    public float fireInterval = 2.5f;
    public GameObject projectilePrefab;
    public float projectileSpeed = 12f;

    [Header("=== アニメーション・演出設定（将来用・空欄でも動作） ===")]
    [Tooltip("死亡アニメーション再生後に消滅するまでの猶予時間（キューブ時は0）")]
    public float deathDestroyDelay = 0f;
    [Tooltip("撃破時エフェクト（任意）")]
    public GameObject deathVfxPrefab;

    private Transform player;
    private PlayerController playerCtrl;
    private Rigidbody rb;
    private Animator animator;
    private bool isExploding = false;
    private bool isDead = false;
    private float fireTimer = 1.0f;
    private float attackAnimTimer = 0f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponentInChildren<Animator>();

        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints.FreezeRotation;
        }

        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
        {
            player = p.transform;
            playerCtrl = p.GetComponent<PlayerController>();
        }

        Destroy(gameObject, lifeTime);
    }

    void FixedUpdate()
    {
        if (player == null || isExploding || isDead) return;

        // プレイヤーの方向を向く（Y軸固定）
        Vector3 targetPosition = player.position;
        targetPosition.y = transform.position.y;
        transform.LookAt(targetPosition);

        float dist = Vector3.Distance(transform.position, targetPosition);

        // 1. 自爆タイプ
        if (minionType == MinionType.Exploder && dist <= explodeTriggerRange)
        {
            SetMoveAnim(0f);
            StartCoroutine(ExplodeRoutine());
            return;
        }

        // 2. 遠距離タイプ
        if (minionType == MinionType.RangedCaster && dist <= castRange)
        {
            SetMoveAnim(0f);
            fireTimer -= Time.fixedDeltaTime;
            if (fireTimer <= 0f)
            {
                fireTimer = fireInterval;
                TriggerAttackAnim();
                FireProjectile();
            }
            return;
        }

        // 3. 基本タイプ（ChaseContact）
        if (minionType == MinionType.ChaseContact && dist <= contactRange)
        {
            SetMoveAnim(0f);
            if (playerCtrl != null)
            {
                playerCtrl.TakeDamage(damage * Time.fixedDeltaTime, false);
            }

            // アニメーション付きモデルにした際、一定間隔で攻撃モーションを再生
            attackAnimTimer -= Time.fixedDeltaTime;
            if (attackAnimTimer <= 0f)
            {
                attackAnimTimer = 1.2f;
                TriggerAttackAnim();
            }
            return;
        }

        // 移動処理
        Vector3 moveDirection = (targetPosition - transform.position).normalized;
        moveDirection.y = 0f;

        if (!Physics.Raycast(transform.position + Vector3.up * 0.5f, moveDirection, 0.7f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (rb != null)
                rb.MovePosition(rb.position + moveDirection * speed * Time.fixedDeltaTime);
            else
                transform.position += moveDirection * speed * Time.fixedDeltaTime;

            SetMoveAnim(1.0f);
        }
        else
        {
            SetMoveAnim(0f);
        }
    }

    private void SetMoveAnim(float speedVal)
    {
        if (animator != null)
        {
            animator.SetFloat("Speed", speedVal);
        }
    }

    private void TriggerAttackAnim()
    {
        if (animator != null)
        {
            animator.SetTrigger("Attack");
        }
    }

    private IEnumerator ExplodeRoutine()
    {
        isExploding = true;
        TriggerAttackAnim();

        Vector3 baseScale = transform.localScale;
        float timer = 0f;
        while (timer < explodeDelay)
        {
            timer += Time.deltaTime;
            float pulse = 1f + Mathf.Sin(timer * 25f) * 0.15f;
            transform.localScale = baseScale * pulse;
            yield return null;
        }

        if (explosionVfxPrefab != null)
        {
            GameObject vfx = Instantiate(explosionVfxPrefab, transform.position, Quaternion.identity);
            Destroy(vfx, 2.5f);
        }

        Collider[] hits = Physics.OverlapSphere(transform.position, explodeRadius);
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Player"))
            {
                PlayerController targetPlayer = hit.GetComponent<PlayerController>();
                if (targetPlayer != null)
                {
                    targetPlayer.TakeDamage(damage);
                }
            }
        }

        Destroy(gameObject);
    }

    private void FireProjectile()
    {
        if (projectilePrefab == null || player == null) return;

        Vector3 spawnPos = transform.position + Vector3.up * 1.0f + transform.forward * 0.6f;
        Vector3 dir = (player.position + Vector3.up * 1.0f - spawnPos).normalized;

        GameObject bullet = Instantiate(projectilePrefab, spawnPos, Quaternion.LookRotation(dir));
        Rigidbody bulletRb = bullet.GetComponent<Rigidbody>();
        if (bulletRb != null)
        {
            bulletRb.velocity = dir * projectileSpeed;
        }
        Destroy(bullet, 5.0f);
    }

    public void TakeDamage(float amount)
    {
        if (isDead) return;

        hp -= amount;
        if (hp <= 0)
        {
            isDead = true;

            if (deathVfxPrefab != null)
            {
                GameObject vfx = Instantiate(deathVfxPrefab, transform.position + Vector3.up * 0.5f, Quaternion.identity);
                Destroy(vfx, 2.0f);
            }

            if (animator != null && deathDestroyDelay > 0f)
            {
                // 死亡モーション中は当たり判定と物理を切る
                Collider col = GetComponent<Collider>();
                if (col != null) col.enabled = false;
                if (rb != null) rb.isKinematic = true;

                animator.SetTrigger("Die");
                Destroy(gameObject, deathDestroyDelay);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}