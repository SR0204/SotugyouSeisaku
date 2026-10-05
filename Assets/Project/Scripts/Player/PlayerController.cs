using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerController : MonoBehaviour
{
    // ==========================================
    // 1. 移動・ダッシュ設定
    // ==========================================
    [Header("=== 1. 移動・ダッシュ設定 ===")]
    [Tooltip("通常移動（歩き）のスピード")]
    public float walkSpeed = 6f;
    [Tooltip("ダッシュ時のスピード")]
    public float sprintSpeed = 10f;
    [Tooltip("回転の補間スピード")]
    public float rotationSpeed = 10f;

    // ==========================================
    // 2. HP・回復（エスト瓶）設定
    // ==========================================
    [Header("=== 2. HP・回復設定 ===")]
    public float maxHealth = 100f;
    [HideInInspector] public float currentHealth;

    [Space(5)]
    [Tooltip("ポーション（エスト瓶）の所持上限数")]
    public int maxEstusCount = 3;
    [HideInInspector] public int currentEstusCount;
    [Tooltip("1回あたりの回復量")]
    public float healAmount = 40f;

    // ==========================================
    // 3. スタミナ設定
    // ==========================================
    [Header("=== 3. スタミナ設定 ===")]
    public float maxStamina = 100f;
    [HideInInspector] public float currentStamina;
    [Tooltip("ダッシュ中の1秒あたりの消費スタミナ")]
    public float staminaDrainRate = 25f;
    [Tooltip("自動回復時の1秒あたりの回復量")]
    public float staminaRegenRate = 15f;
    [Tooltip("スタミナ消費アクション後、回復が始まるまでの待ち時間（秒）")]
    public float regenDelay = 1.0f;

    // ==========================================
    // 4. 攻撃・コンボ設定
    // ==========================================
    [System.Serializable]
    public class ComboSettings
    {
        [Tooltip("攻撃開始から当たり判定・エフェクトが出るまでの時間（秒）")]
        public float hitDelay = 0.2f;
        [Tooltip("全体モーション時間（秒）")]
        public float duration = 0.6f;
        [Tooltip("攻撃の中心となる前方オフセット")]
        public float attackOffset = 1.2f;
        [Tooltip("エフェクト発生高さ（0なら足元、1なら腰/胸）")]
        public float effectOffsetY = 0.05f;
        [Tooltip("チェックを入れるとエフェクトを地面に水平に配置（90度寝かせる）")]
        public bool isGroundEffect = true;
    }

    [Header("=== 4. 攻撃・コンボ設定 ===")]
    [Tooltip("1ヒットあたりの攻撃力")]
    public float attackDamage = 25f;
    [Tooltip("攻撃1回あたりの消費スタミナ")]
    public float attackStaminaCost = 20f;
    [Tooltip("攻撃判定の半径")]
    public float attackRadius = 1.5f;

    [Tooltip("コンボ毎の個別タイミング設定（1段目, 2段目, 3段目）")]
    [SerializeField] private ComboSettings[] comboList = new ComboSettings[3];

    [Header("--- 攻撃エフェクト設定 ---")]
    [Tooltip("攻撃時に発生させるエフェクトプレハブ")]
    [SerializeField] private GameObject attackZonePrefab;
    [Tooltip("エフェクトの表示時間（秒）")]
    [SerializeField] private float zoneDisplayTime = 0.2f;

    [HideInInspector] public bool isAttacking = false;
    private int comboStep = 0;
    private bool canQueueNextCombo = false;
    private bool isNextComboQueued = false;

    // ==========================================
    // 5. 回避（ローリング）・ジャンプ設定
    // ==========================================
    [Header("=== 5. 回避・ジャンプ設定 ===")]
    [Tooltip("ローリング時の移動スピード")]
    public float rollSpeed = 12f;
    [Tooltip("ローリング全体の動作時間（秒）")]
    public float rollDuration = 0.5f;
    [Tooltip("無敵時間（秒）")]
    public float invincibleDuration = 0.3f;
    [Tooltip("ローリングの消費スタミナ")]
    public float rollStaminaCost = 20f;

    [Space(5)]
    [Tooltip("ジャンプの高さ")]
    public float jumpHeight = 1.5f;
    [Tooltip("ジャンプの消費スタミナ")]
    public float jumpStaminaCost = 20f;
    [Tooltip("重力の強さ")]
    public float gravity = -19.62f;

    [HideInInspector] public bool isRolling = false;
    [HideInInspector] public bool isInvincible = false;
    private Vector3 rollDirection;

    // ==========================================
    // 6. UI & コンポーネント参照
    // ==========================================
    [Header("=== 6. UI & 参照設定 ===")]
    public Slider healthSlider;
    public Slider staminaSlider;
    public TextMeshProUGUI estusText;
    [Tooltip("基準とするメインカメラのTransform")]
    public Transform cameraTransform;

    // 内部コンポーネント・変数
    private CharacterController controller;
    private Vector3 velocity;
    private Animator animator;
    private float regenTimer = 0f;
    private bool isSprinting = false;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();

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
        bool isGrounded = controller.isGrounded;
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        // --- 1. ローリング中の処理 ---
        if (isRolling)
        {
            Vector3 rollMove = rollDirection * rollSpeed;
            velocity.y += gravity * Time.deltaTime;
            rollMove.y = velocity.y;

            controller.Move(rollMove * Time.deltaTime);
            return;
        }

        // --- 2. 攻撃中の処理 ---
        if (isAttacking)
        {
            if (animator != null)
            {
                animator.SetFloat("Speed", 0f);
            }

            bool clickingUI = UnityEngine.EventSystems.EventSystem.current != null &&
                              UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
            if (!clickingUI && Input.GetMouseButtonDown(0) && canQueueNextCombo)
            {
                if (currentStamina >= attackStaminaCost)
                {
                    isNextComboQueued = true;
                }
            }

            velocity.y += gravity * Time.deltaTime;
            controller.Move(velocity * Time.deltaTime);
            return;
        }

        // --- 3. 入力取得とアクション判定 ---
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector3 dir = new Vector3(h, 0, v).normalized;

        bool isClickingUI = UnityEngine.EventSystems.EventSystem.current != null &&
                            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();

        // 攻撃発動
        if (!isClickingUI && Input.GetMouseButtonDown(0) && currentStamina >= attackStaminaCost && !isAttacking)
        {
            StartCoroutine(AttackRoutine());
        }

        // 回避発動
        if (Input.GetKeyDown(KeyCode.LeftControl) && currentStamina >= rollStaminaCost && !isAttacking)
        {
            StartCoroutine(RollRoutine(dir));
        }

        // エスト使用
        if (Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.E))
        {
            UseEstus();
        }

        // ダッシュ処理
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

        // ジャンプ処理
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded && currentStamina >= jumpStaminaCost)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            currentStamina -= jumpStaminaCost;
            regenTimer = regenDelay;
        }

        // --- 4. 移動・回転計算 ---
        Vector3 moveVelocity = Vector3.zero;
        float currentSpeed = isSprinting ? sprintSpeed : walkSpeed;

        if (dir.magnitude >= 0.1f && !isAttacking)
        {
            float targetAngle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg + cameraTransform.eulerAngles.y;
            Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;

            moveVelocity = moveDir.normalized * currentSpeed;

            Quaternion targetRotation = Quaternion.Euler(0f, targetAngle, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        // アニメーション更新
        if (animator != null)
        {
            if (dir.magnitude >= 0.1f)
            {
                float animSpeed = isSprinting ? 1.0f : 0.5f;
                animator.SetFloat("Speed", animSpeed);
            }
            else
            {
                animator.SetFloat("Speed", 0f);
            }
        }

        // スタミナ回復
        if (!isSprinting && !isRolling && !isAttacking)
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

        // --- 5. 重力加算とまとめて1回だけMove実行 ---
        velocity.y += gravity * Time.deltaTime;

        Vector3 finalMove = moveVelocity;
        finalMove.y = velocity.y;

        controller.Move(finalMove * Time.deltaTime);
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

    private IEnumerator AttackRoutine()
    {
        isAttacking = true;
        comboStep = 1;

        while (comboStep <= comboList.Length)
        {
            currentStamina -= attackStaminaCost;
            currentStamina = Mathf.Max(currentStamina, 0f);
            regenTimer = regenDelay;

            canQueueNextCombo = false;
            isNextComboQueued = false;

            ComboSettings currentCombo = comboList[comboStep - 1];

            if (animator != null)
            {
                animator.SetInteger("ComboStep", comboStep);

                if (comboStep == 1)
                {
                    animator.SetTrigger("Attack");
                }
            }

            // コンボごとの発生遅延
            yield return new WaitForSeconds(currentCombo.hitDelay);

            // 攻撃判定とエフェクトの実行
            ExecuteAttackHit(currentCombo);

            canQueueNextCombo = true;

            // 残りモーション時間の待機
            float remainingTime = currentCombo.duration - currentCombo.hitDelay;
            if (remainingTime > 0f)
            {
                yield return new WaitForSeconds(remainingTime);
            }

            canQueueNextCombo = false;

            if (isNextComboQueued && comboStep < comboList.Length)
            {
                comboStep++;
            }
            else
            {
                break;
            }
        }

        comboStep = 0;
        canQueueNextCombo = false;
        isNextComboQueued = false;

        if (animator != null)
        {
            animator.SetInteger("ComboStep", 0);
            animator.ResetTrigger("Attack");
        }

        yield return new WaitForSeconds(0.1f);
        isAttacking = false;
    }

    private void ExecuteAttackHit(ComboSettings combo)
    {
        // 攻撃の中心位置
        Vector3 attackCenter = transform.position + transform.forward * combo.attackOffset;

        // 地面用か空中用かで位置と回転を変える
        Vector3 zonePos = attackCenter;
        Quaternion zoneRot;

        if (combo.isGroundEffect)
        {
            zonePos.y = transform.position.y + 0.05f; // 床の少し上
            zoneRot = Quaternion.Euler(90f, transform.eulerAngles.y, 0f); // 床に寝かせる
        }
        else
        {
            zonePos.y = transform.position.y + combo.effectOffsetY; // 胸や腰の高さ
            zoneRot = transform.rotation; // プレイヤーの正面に向ける
        }

        // エフェクト生成
        if (attackZonePrefab != null)
        {
            GameObject zone = Instantiate(attackZonePrefab, zonePos, zoneRot);

            // 床用エフェクトの場合のみスケール調整を適用
            if (combo.isGroundEffect)
            {
                zone.transform.localScale = new Vector3(attackRadius * 2f, attackRadius * 2f, 1f);
            }

            Destroy(zone, zoneDisplayTime);
        }

        // 当たり判定（球体）
        Collider[] hitColliders = Physics.OverlapSphere(attackCenter, attackRadius);
        foreach (var hitCollider in hitColliders)
        {
            BossHealth bossHealth = hitCollider.GetComponent<BossHealth>();
            if (bossHealth != null)
            {
                bossHealth.TakeDamage(attackDamage);
            }

            BossMinionAI minion = hitCollider.GetComponent<BossMinionAI>();
            if (minion != null)
            {
                minion.TakeDamage(999f);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        if (comboList != null && comboList.Length > 0)
        {
            Vector3 attackCenter = transform.position + transform.forward * comboList[0].attackOffset;
            attackCenter.y += comboList[0].effectOffsetY;
            Gizmos.DrawWireSphere(attackCenter, attackRadius);
        }
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
        }
    }

    public void TakeDamage(float damage)
    {
        if (isInvincible) return;

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
        this.enabled = false;

        StageManager stageManager = FindObjectOfType<StageManager>();
        if (stageManager != null)
        {
            stageManager.OnPlayerDied();
        }
    }
}