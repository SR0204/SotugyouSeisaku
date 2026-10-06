using System.Collections;
using UnityEngine;

public class LaserBeam : MonoBehaviour
{
    [Header("=== レーザー表示設定 ===")]
    public LineRenderer lineRenderer;

    [Header("=== 色設定 ===")]
    public Color warningColor = Color.red;
    public Color attackColor = Color.cyan;

    [Header("=== 遮蔽物レイヤー設定 ===")]
    [Tooltip("柱や壁（DefaultやEnvironmentなど）のレイヤーを指定")]
    public LayerMask obstacleMask = ~0; // デフォルトはすべてのレイヤーを対象

    private float laserRadius = 1.0f;
    private float laserDistance = 100f; // ★ 射程距離を拡張（標準100m）
    private float laserDamagePerSecond = 40f;

    void Awake()
    {
        if (lineRenderer == null)
        {
            lineRenderer = GetComponent<LineRenderer>();
        }

        if (lineRenderer != null)
        {
            lineRenderer.enabled = false;
        }
    }

    /// <summary>
    /// レーザーを発射・回転させる
    /// </summary>
    /// <param name="sweepAngle">回転させる角度（360なら全方位1周、720なら2周）</param>
    public void FireLaser(float chargeTime, float duration, float radius, float distance, float damage, float sweepAngle = 360f)
    {
        laserRadius = radius;
        laserDistance = distance; // 引数で指定された射程距離を適用
        laserDamagePerSecond = damage;

        StartCoroutine(LaserRoutine(chargeTime, duration, sweepAngle));
    }

    private IEnumerator LaserRoutine(float chargeTime, float duration, float sweepAngle)
    {
        if (lineRenderer == null) yield break;

        lineRenderer.enabled = true;

        Quaternion startRot = transform.localRotation;

        // 1. 予兆（赤い細線）
        lineRenderer.startWidth = 0.08f;
        lineRenderer.endWidth = 0.08f;
        lineRenderer.startColor = warningColor;
        lineRenderer.endColor = warningColor;

        float timer = 0f;
        while (timer < chargeTime)
        {
            timer += Time.deltaTime;
            UpdateLinePositions(0f);
            yield return null;
        }

        // 2. 本照射（太レーザー＋全方位回転判定）
        lineRenderer.startWidth = laserRadius * 2f;
        lineRenderer.endWidth = laserRadius * 2f;
        lineRenderer.startColor = attackColor;
        lineRenderer.endColor = attackColor;

        timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            float progress = Mathf.Clamp01(timer / duration);

            // ★ 指定した角度（360度など）まで時間経過に合わせて回転
            float currentAngle = Mathf.Lerp(0f, sweepAngle, progress);
            transform.localRotation = startRot * Quaternion.Euler(0, currentAngle, 0);

            // 柱で止まる距離を計算
            float currentHitDistance = UpdateLinePositions(laserRadius);

            // プレイヤーへのヒット判定
            RaycastHit[] hits = Physics.SphereCastAll(
                transform.position,
                laserRadius,
                transform.forward,
                currentHitDistance,
                ~0,
                QueryTriggerInteraction.Ignore
            );

            foreach (var hit in hits)
            {
                if (hit.collider.CompareTag("Player"))
                {
                    PlayerController player = hit.collider.GetComponent<PlayerController>();
                    if (player != null)
                    {
                        player.TakeDamage(laserDamagePerSecond * Time.deltaTime);
                    }
                }
            }

            yield return null;
        }

        // 3. 終了処理
        lineRenderer.enabled = false;
        transform.localRotation = startRot;
    }

    /// <summary>
    /// 柱や壁に当たったらそこでレーザーを止める描画更新
    /// </summary>
    private float UpdateLinePositions(float checkRadius)
    {
        lineRenderer.SetPosition(0, transform.position);

        RaycastHit hit;
        bool isHit = false;

        if (checkRadius > 0.01f)
        {
            isHit = Physics.SphereCast(transform.position, checkRadius, transform.forward, out hit, laserDistance, obstacleMask);
        }
        else
        {
            isHit = Physics.Raycast(transform.position, transform.forward, out hit, laserDistance, obstacleMask);
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