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

        // ★ プレイヤーの落下速度（Y軸速度）を計算
        float verticalVelocity = (target.position.y - lastTargetPos.y) / Time.deltaTime;
        bool isGrounded = (targetController != null) ? targetController.isGrounded : true;

        // ★ 急激な落下（崖から落ちた時）かどうかを判定
        bool isFallingFast = !isGrounded && verticalVelocity < fallThresholdVelocity;

        if (isFallingFast)
        {
            // 落下中はカメラの高さ（Y座標）移動をほぼ固定（超ゆっくり追従）
            currentTargetY = Mathf.Lerp(currentTargetY, target.position.y, Time.deltaTime * 0.5f);
        }
        else
        {
            // 通常時はスムーズにY軸追従
            currentTargetY = Mathf.Lerp(currentTargetY, target.position.y, Time.deltaTime * verticalSmoothSpeed);
        }

        lastTargetPos = target.position;

        if (isLockedOn && lockOnTarget != null)
        {
            Vector3 dirToTarget = lockOnTarget.position - target.position;
            dirToTarget.y = 0;

            if (dirToTarget != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(dirToTarget);

                Vector3 smoothTargetPos = new Vector3(target.position.x, currentTargetY, target.position.z);
                Vector3 position = smoothTargetPos - (targetRotation * Vector3.forward * distance) + Vector3.up * height;
                transform.position = Vector3.Lerp(transform.position, position, Time.deltaTime * 10f);

                Vector3 lookAtPoint = Vector3.Lerp(smoothTargetPos + Vector3.up * 1.5f, lockOnTarget.position + lockOnOffset, 0.3f);
                transform.LookAt(lookAtPoint);

                target.rotation = Quaternion.Slerp(target.rotation, targetRotation, Time.deltaTime * 10f);

                Vector3 angles = transform.eulerAngles;
                x = angles.y;
                y = angles.x;
            }
        }
        else
        {
            Quaternion rotation = Quaternion.Euler(y, x, 0);

            Vector3 smoothTargetPos = new Vector3(target.position.x, currentTargetY, target.position.z);
            Vector3 targetPosition = smoothTargetPos + Vector3.up * height;
            Vector3 position = targetPosition - (rotation * Vector3.forward * distance);

            transform.position = position;

            if (isFallingFast)
            {
                // ★ 落下中はカメラの位置を崖際に残しつつ、下へ落ちていくプレイヤーを注視（見下ろす）
                transform.LookAt(target.position + Vector3.up * 0.5f);

                // 手動操作の角度座標を現在の注視角度に同期
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