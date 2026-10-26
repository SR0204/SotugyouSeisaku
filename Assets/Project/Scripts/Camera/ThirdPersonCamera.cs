using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("追従設定")]
    public Transform target;
    public float distance = 4.0f;
    public float height = 2.0f;
    public float xSpeed = 120.0f;
    public float ySpeed = 80.0f;

    public float yMinLimit = -20f;
    public float yMaxLimit = 80f;

    [Header("エルデンリング風・落下カメラ設定")]
    [Tooltip("通常時のY軸追従スピード")]
    public float verticalSmoothSpeed = 5.0f;
    [Tooltip("落下検知しきい値（この速度以上で急速落下するとY軸追従を停止して見下ろす）")]
    public float fallThresholdVelocity = -8.0f;

    [Header("ロックオン設定")]
    public KeyCode lockOnKey = KeyCode.Mouse2;
    public bool isLockedOn = false;
    public Transform lockOnTarget;
    public Image lockOnIcon;

    [Tooltip("ロックオンアイコンの高さ")]
    public Vector3 lockOnOffset = new Vector3(0, 1.5f, 0);

    [Tooltip("ロックオン可能な最大距離")]
    public float maxLockOnDistance = 25.0f;

    [Header("★ ロックオン画角・距離調整設定")]
    [Tooltip("ロックオン時の最小カメラ距離（超近接時）")]
    public float minLockOnDistance = 4.5f;
    [Tooltip("ロックオン時の最大カメラ距離（離れている時）")]
    public float maxLockOnCameraDistance = 7.0f;
    [Tooltip("ロックオン時のカメラの高さ補正")]
    public float lockOnCameraHeight = 2.5f;

    private float x = 0.0f;
    private float y = 0.0f;
    private Camera cam;

    private float currentTargetY;
    private CharacterController targetController;
    private Vector3 lastTargetPos;

    void Start()
    {
        cam = GetComponent<Camera>();
        Vector3 angles = transform.eulerAngles;
        x = angles.y;
        y = angles.x;

        if (target != null)
        {
            currentTargetY = target.position.y;
            lastTargetPos = target.position;
            targetController = target.GetComponent<CharacterController>();
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (Input.GetKeyDown(lockOnKey) || Input.GetKeyDown(KeyCode.R))
        {
            ToggleLockOn();
        }

        if (isLockedOn && lockOnTarget == null)
        {
            ToggleLockOn();
        }

        if (!isLockedOn)
        {
            x += Input.GetAxis("Mouse X") * xSpeed * Time.deltaTime;
            y -= Input.GetAxis("Mouse Y") * ySpeed * Time.deltaTime;
            y = Mathf.Clamp(y, yMinLimit, yMaxLimit);
        }

        UpdateLockOnIcon();
    }

    void LateUpdate()
    {
        if (target == null) return;

        // プレイヤーの落下速度（Y軸速度）を計算
        float verticalVelocity = (target.position.y - lastTargetPos.y) / Time.deltaTime;
        bool isGrounded = (targetController != null) ? targetController.isGrounded : true;

        bool isFallingFast = !isGrounded && verticalVelocity < fallThresholdVelocity;

        if (isFallingFast)
        {
            currentTargetY = Mathf.Lerp(currentTargetY, target.position.y, Time.deltaTime * 0.5f);
        }
        else
        {
            currentTargetY = Mathf.Lerp(currentTargetY, target.position.y, Time.deltaTime * verticalSmoothSpeed);
        }

        lastTargetPos = target.position;

        // ==========================================
        // ★ ロックオン時のカメラ位置計算（画角見切れ防止）
        // ==========================================
        if (isLockedOn && lockOnTarget != null)
        {
            Vector3 playerPos = new Vector3(target.position.x, currentTargetY, target.position.z);
            Vector3 enemyPos = lockOnTarget.position;

            // 1. プレイヤーから敵への方向と距離を取得
            Vector3 dirToEnemy = enemyPos - playerPos;
            float currentDistToEnemy = dirToEnemy.magnitude;
            dirToEnemy.y = 0;
            dirToEnemy.Normalize();

            if (dirToEnemy != Vector3.zero)
            {
                // 2. 敵との距離に応じてカメラの距離を引き直す（オートズームアウト）
                float targetCamDistance = Mathf.Lerp(minLockOnDistance, maxLockOnCameraDistance, currentDistToEnemy / maxLockOnDistance);

                // 3. 敵とプレイヤーの中間地点（注視点）を算出
                Vector3 centerPoint = Vector3.Lerp(playerPos + Vector3.up * 1.2f, enemyPos + lockOnOffset, 0.4f);

                // 4. カメラの目標位置をセット（プレイヤーの背後方向に引く）
                Quaternion targetRotation = Quaternion.LookRotation(dirToEnemy);
                Vector3 desiredCameraPos = playerPos - (targetRotation * Vector3.forward * targetCamDistance) + Vector3.up * lockOnCameraHeight;

                // スムーズに補間
                transform.position = Vector3.Lerp(transform.position, desiredCameraPos, Time.deltaTime * 8f);

                // 常にプレイヤーと敵が画面に入る中間地点を見つめる
                transform.LookAt(centerPoint);

                // プレイヤーを敵の方に向ける
                target.rotation = Quaternion.Slerp(target.rotation, targetRotation, Time.deltaTime * 10f);

                // ロックオン解除後の視点リセット用データ同期
                Vector3 angles = transform.eulerAngles;
                x = angles.y;
                y = angles.x;
            }
        }
        // ==========================================
        // 非ロックオン時のカメラ（通常時）
        // ==========================================
        else
        {
            Quaternion rotation = Quaternion.Euler(y, x, 0);

            Vector3 smoothTargetPos = new Vector3(target.position.x, currentTargetY, target.position.z);
            Vector3 targetPosition = smoothTargetPos + Vector3.up * height;
            Vector3 position = targetPosition - (rotation * Vector3.forward * distance);

            transform.position = position;

            if (isFallingFast)
            {
                transform.LookAt(target.position + Vector3.up * 0.5f);

                Vector3 angles = transform.eulerAngles;
                x = angles.y;
                y = angles.x;
            }
            else
            {
                transform.rotation = rotation;
            }
        }
    }

    void ToggleLockOn()
    {
        if (isLockedOn)
        {
            isLockedOn = false;
            lockOnTarget = null;
            if (lockOnIcon != null) lockOnIcon.gameObject.SetActive(false);
        }
        else
        {
            BossHealth nearestEnemy = FindNearestEnemy();
            if (nearestEnemy != null)
            {
                lockOnTarget = nearestEnemy.transform;
                isLockedOn = true;
                if (lockOnIcon != null) lockOnIcon.gameObject.SetActive(true);
            }
        }
    }

    private BossHealth FindNearestEnemy()
    {
        BossHealth[] enemies = FindObjectsOfType<BossHealth>();
        BossHealth nearest = null;
        float minDistance = maxLockOnDistance;

        foreach (var enemy in enemies)
        {
            if (enemy == null || target == null) continue;

            float dist = Vector3.Distance(target.position, enemy.transform.position);

            if (dist < minDistance)
            {
                Vector3 screenPos = cam.WorldToScreenPoint(enemy.transform.position);
                if (screenPos.z > 0)
                {
                    minDistance = dist;
                    nearest = enemy;
                }
            }
        }

        return nearest;
    }

    void UpdateLockOnIcon()
    {
        if (lockOnIcon == null) return;

        if (isLockedOn && lockOnTarget != null)
        {
            if (!lockOnIcon.gameObject.activeSelf)
            {
                lockOnIcon.gameObject.SetActive(true);
            }

            Vector3 targetWorldPos = lockOnTarget.position + lockOnOffset;
            Vector3 screenPos = cam.WorldToScreenPoint(targetWorldPos);

            if (screenPos.z > 0)
            {
                lockOnIcon.transform.position = screenPos;
            }
            else
            {
                lockOnIcon.gameObject.SetActive(false);
            }
        }
        else
        {
            if (lockOnIcon.gameObject.activeSelf)
            {
                lockOnIcon.gameObject.SetActive(false);
            }
        }
    }
}