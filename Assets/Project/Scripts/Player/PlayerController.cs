using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerController : MonoBehaviour
{
    [Header("移動・ダッシュ設定")]
    public float walkSpeed = 6f;
    public float sprintSpeed = 10f;
    public float rotationSpeed = 10f;

    [Header("HP設定")]
    public float maxHealth = 100f;
    public float currentHealth;

    [Header("攻撃設定")]
    public float attackDamage = 25f;
    public float attackStaminaCost = 20f;
    public float attackRange = 2.0f;
    public float attackCooldown = 0.5f;
    private float attackTimer = 0f;

    [Header("回避（ローリング）設定")]
    public float rollSpeed = 12f;
    public float rollDuration = 0.5f;
    public float invincibleDuration = 0.3f;
    public float rollStaminaCost = 20f;
    public bool isRolling = false;
    public bool isInvincible = false;
    private Vector3 rollDirection;

    [Header("エスト瓶（回復）設定")]
    public int maxEstusCount = 3;
    public int currentEstusCount;
    public float healAmount = 40f;
    public TextMeshProUGUI estusText;

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
    public Slider healthSlider;
    public Slider staminaSlider;

    [Header("参照")]
    public Transform cameraTransform;

    private CharacterController controller;
    private Vector3 velocity;

    void Start()
    {
        controller = GetComponent<CharacterController>();

        currentHealth = maxHealth;
        UpdateHealthUI();

        currentEstusCount = maxEstusCount;
        UpdateEstusUI();

        currentStamina = maxStamina;
        if (staminaSlider != null)
        {
            staminaSlider.maxValue = maxStamina;
            staminaSlider.value = currentStamina;
        }

        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }
    }

    void Update()
    {
        if (attackTimer > 0f)
        {
            attackTimer -= Time.deltaTime;
        }

        bool isGrounded = controller.isGrounded;
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector3 dir = new Vector3(h, 0, v).normalized;

        if (isRolling)
        {
            controller.Move(rollDirection * rollSpeed * Time.deltaTime);
            velocity.y += gravity * Time.deltaTime;
            controller.Move(velocity * Time.deltaTime);
            return;
        }

        // 攻撃（マウス左クリック）
        if (Input.GetMouseButtonDown(0) && attackTimer <= 0f && currentStamina >= attackStaminaCost)
        {
            Attack();
        }

        // 回避（Left Controlキー）
        if (Input.GetKeyDown(KeyCode.LeftControl) && currentStamina >= rollStaminaCost)
        {
            StartCoroutine(RollRoutine(dir));
        }

        // エスト使用（Eキー / Rキー）
        if (Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.E))
        {
            UseEstus();
        }

        // ダッシュ（Left Shift）
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

        // 移動と回転
        if (dir.magnitude >= 0.1f)
        {
            float targetAngle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg + cameraTransform.eulerAngles.y;
            Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;

            controller.Move(moveDir.normalized * currentSpeed * Time.deltaTime);

            Quaternion targetRotation = Quaternion.Euler(0f, targetAngle, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        // ジャンプ（Spaceキー）
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded && currentStamina >= jumpStaminaCost)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            currentStamina -= jumpStaminaCost;
            regenTimer = regenDelay;
        }

        // スタミナ自然回復
        if (!isSprinting && !isRolling)
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

        if (staminaSlider != null)
        {
            staminaSlider.value = currentStamina;
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    private IEnumerator RollRoutine(Vector3 inputDir)
    {
        isRolling = true;
        isInvincible = true;

        currentStamina -= rollStaminaCost;
        regenTimer = regenDelay;

        if (inputDir.magnitude >= 0.1f)
        {
            float targetAngle = Mathf.Atan2(inputDir.x, inputDir.z) * Mathf.Rad2Deg + cameraTransform.eulerAngles.y;
            rollDirection = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
        }
        else
        {
            rollDirection = transform.forward;
        }

        transform.rotation = Quaternion.LookRotation(rollDirection);

        float timer = 0f;
        while (timer < rollDuration)
        {
            timer += Time.deltaTime;

            if (timer >= invincibleDuration)
            {
                isInvincible = false;
            }

            yield return null;
        }

        isRolling = false;
        isInvincible = false;
    }

    private void Attack()
    {
        currentStamina -= attackStaminaCost;
        regenTimer = regenDelay;
        attackTimer = attackCooldown;

        Debug.Log("プレイヤーの攻撃！");

        Vector3 attackCenter = transform.position + transform.forward * (attackRange * 0.5f);
        Collider[] hitColliders = Physics.OverlapSphere(attackCenter, attackRange * 0.5f);

        foreach (var hitCollider in hitColliders)
        {
            BossHealth bossHealth = hitCollider.GetComponent<BossHealth>();
            if (bossHealth != null)
            {
                bossHealth.TakeDamage(attackDamage);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector3 attackCenter = transform.position + transform.forward * (attackRange * 0.5f);
        Gizmos.DrawWireSphere(attackCenter, attackRange * 0.5f);
    }

    private void UseEstus()
    {
        if (currentEstusCount > 0 && currentHealth < maxHealth)
        {
            currentEstusCount--;
            currentHealth += healAmount;
            currentHealth = Mathf.Min(currentHealth, maxHealth);

            UpdateHealthUI();
            UpdateEstusUI();

            Debug.Log("エスト瓶を使用！ HPが " + healAmount + " 回復。（残り: " + currentEstusCount + "）");
        }
    }

    public void TakeDamage(float damage)
    {
        if (isInvincible)
        {
            Debug.Log("ローリング回避！ (無敵時間中)");
            return;
        }

        currentHealth -= damage;
        currentHealth = Mathf.Max(currentHealth, 0f);
        UpdateHealthUI();

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void UpdateHealthUI()
    {
        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHealth;
            healthSlider.value = currentHealth;
        }
    }

    private void UpdateEstusUI()
    {
        if (estusText != null)
        {
            estusText.text = "Potion: " + currentEstusCount;
        }
    }

    private void Die()
    {
        Debug.Log("プレイヤー死亡");
    }
}