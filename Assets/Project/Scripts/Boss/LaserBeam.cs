using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LaserBeam : MonoBehaviour
{
    [Header("=== レーザー表示設定 ===")]
    public LineRenderer lineRenderer;

    [Header("=== 色設定 ===")]
    public Color warningColor = Color.red;
    public Color attackColor = Color.cyan;

    [Header("=== レイヤー設定 ===")]
    [Tooltip("柱や壁（DefaultやEnvironmentなど）のレイヤーを指定")]
    public LayerMask obstacleMask = ~0;
    [Tooltip("プレイヤーのレイヤー（またはTagチェック用）")]
    public LayerMask playerMask = ~0;

    private float laserRadius = 1.0f;
    private float laserDistance = 100f; // 射程距離
    private float laserDamagePerSecond = 40f;

    void Awake()
    {
        if (lineRenderer == null)
        {
            lineRenderer = GetComponent<LineRenderer>();
        }

        if (lineRenderer != null)
        {
            if (lineRenderer.material == null || lineRenderer.material.name.Contains("Default"))
            {
                lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            }
            lineRenderer.positionCount = 2; // 線を描画するための2点を確保
            lineRenderer.enabled = false;
        }
    }

    /// <summary>
    /// レーザーを発射・回転させる
    /// </summary>
    public void FireLaser(float chargeTime, float duration, float radius, float distance, float damage, float sweepAngle = 0f)
    {
        laserRadius = radius;
        laserDistance = distance;
        laserDamagePerSecond = damage;

        // ★ 新しいレーザー発射前に古いコルーチンを確実に止めて初期化
        DisableLaser();

        StartCoroutine(LaserRoutine(chargeTime, duration, sweepAngle));
    }

    /// <summary>
    /// レーザーを安全に停止・非表示化する
    /// </summary>
    public void DisableLaser()
    {
        StopAllCoroutines();
        if (lineRenderer != null)
        {
            lineRenderer.enabled = false;
        }
    }

    private IEnumerator LaserRoutine(float warningTime, float duration, float sweepAngle)
    {
        if (lineRenderer == null) lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null) yield break;

        lineRenderer.enabled = true;
        lineRenderer.positionCount = 2;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");

        // ★ プレイヤーの方を向く（最初の基準角度）
        if (playerObj != null)
        {
            Vector3 targetPos = playerObj.transform.position + Vector3.up * 0.8f;
            Vector3 targetDir = (targetPos - transform.position).normalized;
            if (targetDir != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(targetDir);
            }
        }

        // ★ 変更点1：localRotation ではなくワールドの rotation を基準にする
        Quaternion startWorldRot = transform.rotation;

        // ----------------------------------------------------
        // 1. 予兆（赤い警告線で発射方向と長さを提示）
        // ----------------------------------------------------
        lineRenderer.startWidth = 0.25f;
        lineRenderer.endWidth = 0.25f;
        lineRenderer.startColor = warningColor;
        lineRenderer.endColor = warningColor;

        float timer = 0f;
        while (timer < warningTime)
        {
            timer += Time.deltaTime;
            // ★ 変更点2：ワールド回転を固定
            transform.rotation = startWorldRot;
            UpdateLinePositions(laserRadius);
            yield return null;
        }

        // ----------------------------------------------------
        // 2. 本照射（極太レーザーで回転＋判定）
        // ----------------------------------------------------
        lineRenderer.startWidth = laserRadius * 2f;
        lineRenderer.endWidth = laserRadius * 2f;
        lineRenderer.startColor = attackColor;
        lineRenderer.endColor = attackColor;

        HashSet<PlayerController> hitPlayersThisBeam = new HashSet<PlayerController>();

        float forwardOffset = laserRadius + 0.6f;

        timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;

            if (Mathf.Abs(sweepAngle) > 0.1f)
            {
                float progress = Mathf.Clamp01(timer / duration);
                float currentAngle = Mathf.Lerp(0f, sweepAngle, progress);
                // ★ 変更点3：ワールド回転ベースで薙ぎ払い
                transform.rotation = startWorldRot * Quaternion.Euler(0, currentAngle, 0);
            }

            float currentHitDistance = UpdateLinePositions(laserRadius);

            // 発射起点より前方にずらして判定開始（背中への判定漏れ防止）
            Vector3 castOrigin = transform.position + transform.forward * forwardOffset;
            float adjustedDistance = Mathf.Max(0.1f, currentHitDistance - forwardOffset);

            RaycastHit[] hits = Physics.SphereCastAll(
                castOrigin,
                laserRadius,
                transform.forward,
                adjustedDistance,
                playerMask,
                QueryTriggerInteraction.Ignore
            );

            foreach (var hit in hits)
            {
                if (hit.collider.CompareTag("Player"))
                {
                    // プレイヤーがボスの背中側にいる場合は無視する
                    Vector3 toPlayer = hit.collider.transform.position - transform.position;
                    if (Vector3.Dot(transform.forward, toPlayer) <= 0f)
                    {
                        continue;
                    }

                    PlayerController player = hit.collider.GetComponent<PlayerController>();
                    if (player == null) player = hit.collider.GetComponentInParent<PlayerController>();

                    if (player != null)
                    {
                        if (!hitPlayersThisBeam.Contains(player))
                        {
                            hitPlayersThisBeam.Add(player);
                            player.TakeDamage(laserDamagePerSecond);
                        }
                    }
                }
            }

            yield return null;
        }

        // ----------------------------------------------------
        // 3. 終了処理
        // ----------------------------------------------------
        DisableLaser();
        // ★ 変更点4：終了時もワールド回転を戻す
        transform.rotation = startWorldRot;

        BossRadagonAI bossAI = GetComponentInParent<BossRadagonAI>();
        if (bossAI != null)
        {
            bossAI.OnLaserFinished();
        }
    }

    private float UpdateLinePositions(float checkRadius)
    {
        if (lineRenderer == null) return laserDistance;

        lineRenderer.SetPosition(0, transform.position);

        RaycastHit hit;
        bool isHit = false;

        if (checkRadius > 0.01f)
        {
            isHit = Physics.SphereCast(transform.position, checkRadius, transform.forward, out hit, laserDistance, obstacleMask, QueryTriggerInteraction.Ignore);
        }
        else
        {
            isHit = Physics.Raycast(transform.position, transform.forward, out hit, laserDistance, obstacleMask, QueryTriggerInteraction.Ignore);
        }

        if (isHit)
        {
            if (!hit.collider.CompareTag("Player") && !hit.collider.CompareTag("Boss"))
            {
                lineRenderer.SetPosition(1, transform.position + transform.forward * hit.distance);
                return hit.distance;
            }
        }

        lineRenderer.SetPosition(1, transform.position + transform.forward * laserDistance);
        return laserDistance;
    }
}