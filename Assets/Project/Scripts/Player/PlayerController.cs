using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI; // ★UIを操作するために必要！

public class PlayerController : MonoBehaviour
{
    [Header("移動・ダッシュ設定")]
    public float walkSpeed = 6f;
    public float sprintSpeed = 10f;
    public float rotationSpeed = 10f;

    [Header("スタミナ設定")]
    public float maxStamina = 100f;
    public float currentStamina;
    public float staminaDrainRate = 25f;
    public float staminaRegenRate = 15f;
    public float regenDelay = 1.0f;

    private float regenTimer = 0f;
    private bool isSprinting = false;

    [Header("ジャンプ・重力設定")]
    public float jumpHeight = 1.5f;
    public float jumpStaminaCost = 20f;
    public float gravity = -19.62f;

    [Header("UI設定")]
    public Slider staminaSlider; // ★スタミナバー（UI）の参照

    [Header("参照")]
    public Transform cameraTransform;

    private CharacterController controller;
    private Vector3 velocity;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        currentStamina = maxStamina;

        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        // スタミナバーの初期設定
        if (staminaSlider != null)
        {
            staminaSlider.maxValue = maxStamina;
            staminaSlider.value = currentStamina;
        }
    }

    void Update()
    {
        // --- 1. 接地判定と重力リセット ---
        bool isGrounded = controller.isGrounded;
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        // 入力の取得
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector3 dir = new Vector3(h, 0, v).normalized;

        // --- 2. ダッシュとスタミナ消費処理 ---
        bool wantsToSprint = Input.GetKey(KeyCode.LeftShift);

        if (wantsToSprint && dir.magnitude >= 0.1f && currentStamina > 0f)
        {
            isSprinting = true;
            currentStamina -= staminaDrainRate * Time.deltaTime;
            currentStamina = Mathf.Max(currentStamina, 0f);
            regenTimer = regenDelay;
        }
        else
        {
            isSprinting = false;
        }

        float currentSpeed = isSprinting ? sprintSpeed : walkSpeed;

        // --- 3. 移動と回転 ---
        if (dir.magnitude >= 0.1f)
        {
            float targetAngle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg + cameraTransform.eulerAngles.y;
            Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;

            controller.Move(moveDir.normalized * currentSpeed * Time.deltaTime);

            Quaternion targetRotation = Quaternion.Euler(0f, targetAngle, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        // --- 4. ジャンプ処理 ---
        if (Input.GetButtonDown("Jump") && isGrounded && currentStamina >= jumpStaminaCost)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            currentStamina -= jumpStaminaCost;
            regenTimer = regenDelay;
        }

        // --- 5. スタミナ自然回復処理 ---
        if (!isSprinting)
        {
            if (regenTimer > 0f)
            {
                regenTimer -= Time.deltaTime;
            }
            else if (currentStamina < maxStamina)
            {
                currentStamina += staminaRegenRate * Time.deltaTime;
                currentStamina = Mathf.Min(currentStamina, maxStamina);
            }
        }

        // --- 6. スタミナUIの更新 ---
        if (staminaSlider != null)
        {
            staminaSlider.value = currentStamina; // UIの数値を現在のスタミナに同期
        }

        // --- 7. 重力の加算とY軸移動 ---
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}