using System.Collections;
using UnityEngine;

public class LaserBeam : MonoBehaviour
{
    [Header("=== レーザー表示設定 ===")]
    public LineRenderer lineRenderer;

    [Header("=== 色設定 ===")]
    public Color warningColor = Color.red;
    public Color attackColor = Color.cyan;

    private float laserRadius = 1.0f;
    private float laserDistance = 30f;
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
    /// レーザー発射メイン処理
    /// </summary>
    /// <param name="sweepAngle">0の場合は直進、それ以外は左右の薙ぎ払い角度（例: 60なら-30°〜+30°）</param>
    public void FireLaser(float chargeTime, float duration, float radius, float distance, float damage, float sweepAngle = 0f)
    {
        laserRadius = radius;
        laserDistance = distance;
        laserDamagePerSecond = damage;

        StartCoroutine(LaserRoutine(chargeTime, duration, sweepAngle));
    }

    private IEnumerator LaserRoutine(float chargeTime, float duration, float sweepAngle)
    {
        if (lineRenderer == null) yield break;

        lineRenderer.enabled = true;

        // 開始角度と終了角度の計算（現在の正面向を基準）
        Quaternion startRot = transform.localRotation;
        Quaternion leftRot = startRot * Quaternion.Euler(0, -sweepAngle / 2f, 0);
        Quaternion rightRot = startRot * Quaternion.Euler(0, sweepAngle / 2f, 0);

        // ----------------------------------------
        // 1. 予兆（赤い細線）
        // ----------------------------------------
        lineRenderer.startWidth = 0.08f;
        lineRenderer.endWidth = 0.08f;
        lineRenderer.startColor = warningColor;
        lineRenderer.endColor = warningColor;

        if (sweepAngle > 0f)
        {
            // 薙ぎ払いの場合は、開始位置（左端）へ銃口（判定）を向ける
            transform.localRotation = leftRot;
        }

        float timer = 0f;
        while (timer < chargeTime)
        {
            timer += Time.deltaTime;
            UpdateLinePositions();
            yield return null;
        }

        // ----------------------------------------
        // 2. 本照射（極太レーザー＋薙ぎ払い移動）
        // ----------------------------------------
        lineRenderer.startWidth = laserRadius * 2f;
        lineRenderer.endWidth = laserRadius * 2f;
        lineRenderer.startColor = attackColor;
        lineRenderer.endColor = attackColor;

        timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            float progress = Mathf.Clamp01(timer / duration);

            // 薙ぎ払い角度の補間（左から右へ回転）
            if (sweepAngle > 0f)
            {
                transform.localRotation = Quaternion.Slerp(leftRot, rightRot, progress);
            }

            UpdateLinePositions();

            // 判定処理（円柱状判定）
            RaycastHit[] hits = Physics.SphereCastAll(
                transform.position,
                laserRadius,
                transform.forward,
                laserDistance
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

        // ----------------------------------------
        // 3. 照射終了・向きを元に戻す
        // ----------------------------------------
        lineRenderer.enabled = false;
        transform.localRotation = startRot;
    }

    private void UpdateLinePositions()
    {
        lineRenderer.SetPosition(0, transform.position);
        lineRenderer.SetPosition(1, transform.position + transform.forward * laserDistance);
    }
}