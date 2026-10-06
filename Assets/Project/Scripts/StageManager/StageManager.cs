using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StageManager : MonoBehaviour
{
    [Header("プレハブ設定")]
    [Tooltip("通常のアリーナプレハブ（ランダム選出用）")]
    public GameObject[] arenaPrefabs;

    [Tooltip("特定ボス専用のアリーナ（例: コロシアムなど）。空欄なら通常アリーナからランダム選出")]
    public GameObject[] bossSpecificArenas; // ★ ボスごとの固定ステージ用（ボスと同じ要素数にする）

    public GameObject restAreaPrefab;           // 休息専用エリア
    public GameObject playerPrefab;
    public GameObject[] bossPrefabs;
    public GameObject uiCanvasPrefab;

    [Header("生成位置設定")]
    public Vector3 stagePosition = new Vector3(0, -0.5f, 0);
    public Vector3 playerPosition = new Vector3(0, 0.1f, 0);
    public Vector3 bossPosition = new Vector3(0, 1f, 10f);

    private GameObject currentStageInstance;
    private GameObject currentBossInstance;
    private GameObject currentPlayerInstance;
    private PlayerController playerCtrl;

    // UI参照用
    private Slider healthSlider;
    private Slider staminaSlider;
    private TextMeshProUGUI estusText;
    private Slider bossHPSlider;
    private GameObject bossSelectPanel;
    private TextMeshProUGUI gameOverText;

    private bool isStageActive = false;

    void Start()
    {
        InitializeStage();
    }

    void InitializeStage()
    {
        // 1. UI Canvas を生成
        if (uiCanvasPrefab != null)
        {
            GameObject canvasObj = Instantiate(uiCanvasPrefab);

            Transform hpObj = canvasObj.transform.Find("HPBar");
            if (hpObj != null) healthSlider = hpObj.GetComponent<Slider>();

            Transform staminaObj = canvasObj.transform.Find("StaminaBar");
            if (staminaObj != null) staminaSlider = staminaObj.GetComponent<Slider>();

            Transform potionObj = canvasObj.transform.Find("Potion");
            if (potionObj != null) estusText = potionObj.GetComponent<TextMeshProUGUI>();

            Transform bossHPObj = canvasObj.transform.Find("BossHPBar");
            if (bossHPObj != null) bossHPSlider = bossHPObj.GetComponent<Slider>();

            Transform gameOverObj = canvasObj.transform.Find("GameOverText");
            if (gameOverObj != null)
            {
                gameOverText = gameOverObj.GetComponent<TextMeshProUGUI>();
                gameOverText.gameObject.SetActive(false);
            }

            Transform lockOnIconObj = canvasObj.transform.Find("LockOnIcon");
            if (lockOnIconObj != null && Camera.main != null)
            {
                ThirdPersonCamera cam = Camera.main.GetComponent<ThirdPersonCamera>();
                if (cam != null)
                {
                    cam.lockOnIcon = lockOnIconObj.GetComponent<Image>();
                }
            }

            // ボス選択ボタン自動登録
            Transform panelObj = canvasObj.transform.Find("BossSelectPanel");
            if (panelObj != null)
            {
                bossSelectPanel = panelObj.gameObject;

                for (int i = 0; i < bossPrefabs.Length; i++)
                {
                    int bossIndex = i;
                    string buttonName = $"Boss{bossIndex + 1}Button";

                    Transform btnObj = panelObj.Find(buttonName);
                    if (btnObj != null)
                    {
                        Button btn = btnObj.GetComponent<Button>();
                        if (btn != null)
                        {
                            btn.onClick.RemoveAllListeners();
                            btn.onClick.AddListener(() => SelectAndStartBoss(bossIndex));
                        }
                    }
                }

                bossSelectPanel.SetActive(false);
            }
        }

        // 2. Player 生成
        SpawnPlayer(healthSlider, staminaSlider, estusText);

        // 3. 最初は休息エリアを表示
        ShowRestArea();
    }

    void SpawnPlayer(Slider hpSlider, Slider stSlider, TextMeshProUGUI estText)
    {
        if (currentPlayerInstance != null) Destroy(currentPlayerInstance);

        if (playerPrefab != null)
        {
            currentPlayerInstance = Instantiate(playerPrefab, playerPosition, Quaternion.identity);
            playerCtrl = currentPlayerInstance.GetComponent<PlayerController>();

            if (playerCtrl != null)
            {
                playerCtrl.healthSlider = hpSlider;
                playerCtrl.staminaSlider = stSlider;
                playerCtrl.estusText = estText;
            }

            if (Camera.main != null)
            {
                ThirdPersonCamera cam = Camera.main.GetComponent<ThirdPersonCamera>();
                if (cam != null) cam.target = currentPlayerInstance.transform;
            }
        }
    }

    private void TeleportPlayer(Vector3 targetPos)
    {
        if (currentPlayerInstance == null) return;

        CharacterController cc = currentPlayerInstance.GetComponent<CharacterController>();
        Rigidbody rb = currentPlayerInstance.GetComponent<Rigidbody>();

        if (cc != null) cc.enabled = false;

        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        currentPlayerInstance.transform.position = targetPos;

        if (cc != null) cc.enabled = true;
    }

    void Update()
    {
        if (isStageActive && currentBossInstance == null)
        {
            OnBossDefeated();
        }
    }

    void OnBossDefeated()
    {
        isStageActive = false;

        if (bossHPSlider != null)
        {
            bossHPSlider.gameObject.SetActive(false);
        }

        Debug.Log("ボス撃破！休息エリアを表示します。");
        ShowRestArea();
    }

    public void ShowRestArea()
    {
        if (currentStageInstance != null)
        {
            Destroy(currentStageInstance);
        }

        if (restAreaPrefab != null)
        {
            currentStageInstance = Instantiate(restAreaPrefab, stagePosition, Quaternion.identity);
        }

        TeleportPlayer(playerPosition);

        RestAndHeal();

        if (bossSelectPanel != null)
        {
            bossSelectPanel.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    // ★ ボス選択＆アリーナ生成（固定アリーナ優先判定）
    public void SelectAndStartBoss(int bossIndex)
    {
        if (bossIndex < 0 || bossIndex >= bossPrefabs.Length) return;

        if (bossSelectPanel != null)
        {
            bossSelectPanel.SetActive(false);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (currentStageInstance != null)
        {
            Destroy(currentStageInstance);
        }

        // ---------------------------------------------------------
        // ★ アリーナの決定ロジック
        // ---------------------------------------------------------
        GameObject selectedArenaPrefab = null;

        // 1. もし「ボス専用アリーナ」が設定されていればそれを優先
        if (bossSpecificArenas != null && bossIndex < bossSpecificArenas.Length && bossSpecificArenas[bossIndex] != null)
        {
            selectedArenaPrefab = bossSpecificArenas[bossIndex];
        }
        // 2. 設定されていなければ、従来通りランダムで選ぶ
        else if (arenaPrefabs != null && arenaPrefabs.Length > 0)
        {
            int randomIndex = Random.Range(0, arenaPrefabs.Length);
            selectedArenaPrefab = arenaPrefabs[randomIndex];
        }

        // アリーナを生成
        if (selectedArenaPrefab != null)
        {
            currentStageInstance = Instantiate(selectedArenaPrefab, stagePosition, Quaternion.identity);
        }
        // ---------------------------------------------------------

        TeleportPlayer(playerPosition);

        if (bossPrefabs[bossIndex] != null)
        {
            currentBossInstance = Instantiate(bossPrefabs[bossIndex], bossPosition, Quaternion.identity);

            BossHealth bossHealth = currentBossInstance.GetComponent<BossHealth>();
            if (bossHealth != null && bossHPSlider != null)
            {
                bossHPSlider.gameObject.SetActive(true);
                bossHealth.bossHPSlider = bossHPSlider;
            }
        }

        isStageActive = true;
    }

    public void RestAndHeal()
    {
        if (playerCtrl != null)
        {
            playerCtrl.currentHealth = playerCtrl.maxHealth;
            playerCtrl.currentEstusCount = playerCtrl.maxEstusCount;
            playerCtrl.TakeDamage(0);
            Debug.Log("休息完了：HPとエスト瓶が全回復しました！");
        }
    }

    public void OnPlayerDied()
    {
        StartCoroutine(GameOverRoutine());
    }

    private IEnumerator GameOverRoutine()
    {
        isStageActive = false;

        if (currentBossInstance != null)
        {
            Destroy(currentBossInstance);
        }

        if (bossHPSlider != null)
        {
            bossHPSlider.gameObject.SetActive(false);
        }

        if (gameOverText != null)
        {
            gameOverText.gameObject.SetActive(true);
        }

        yield return new WaitForSeconds(3.0f);

        if (gameOverText != null)
        {
            gameOverText.gameObject.SetActive(false);
        }

        SpawnPlayer(healthSlider, staminaSlider, estusText);
        ShowRestArea();
    }
}