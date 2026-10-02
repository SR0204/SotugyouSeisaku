using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StageManager : MonoBehaviour
{
    [Header("プレハブ設定")]
    public GameObject arenaPrefab;
    public GameObject playerPrefab;
    public GameObject[] bossPrefabs;
    public GameObject uiCanvasPrefab;
    public GameObject restAreaPrefab;

    [Header("生成位置")]
    public Vector3 arenaPosition = new Vector3(0, -0.5f, 0);
    public Vector3 playerPosition = new Vector3(0, 1f, 0);
    public Vector3 bossPosition = new Vector3(0, 1f, 10f);
    public Vector3 restPointPosition = new Vector3(0, 1f, 3f);

    private GameObject currentBossInstance;
    private GameObject currentRestAreaInstance;
    private GameObject currentPlayerInstance;
    private PlayerController playerCtrl;
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
        Slider healthSlider = null;
        Slider staminaSlider = null;
        TextMeshProUGUI estusText = null;

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

            // GameOverText の参照取得
            Transform gameOverObj = canvasObj.transform.Find("GameOverText");
            if (gameOverObj != null)
            {
                gameOverText = gameOverObj.GetComponent<TextMeshProUGUI>();
                gameOverText.gameObject.SetActive(false);
            }

            // ★ LockOnIcon を探してカメラに渡す
            Transform lockOnIconObj = canvasObj.transform.Find("LockOnIcon");
            if (lockOnIconObj != null && Camera.main != null)
            {
                ThirdPersonCamera cam = Camera.main.GetComponent<ThirdPersonCamera>();
                if (cam != null)
                {
                    cam.lockOnIcon = lockOnIconObj.GetComponent<Image>();
                }
            }

            // ボス選択パネルとボタンの自動紐付け
            Transform panelObj = canvasObj.transform.Find("BossSelectPanel");
            if (panelObj != null)
            {
                bossSelectPanel = panelObj.gameObject;

                Transform btn1Obj = panelObj.Find("Boss1Button");
                if (btn1Obj != null)
                {
                    Button btn1 = btn1Obj.GetComponent<Button>();
                    if (btn1 != null)
                    {
                        btn1.onClick.RemoveAllListeners();
                        btn1.onClick.AddListener(() => SelectAndStartBoss(0));
                    }
                }

                bossSelectPanel.SetActive(false);
            }
        }

        // 2. Arena 生成
        if (arenaPrefab != null)
        {
            Instantiate(arenaPrefab, arenaPosition, Quaternion.identity);
        }

        // 3. Player 生成
        SpawnPlayer(healthSlider, staminaSlider, estusText);

        // 最初の休息モード（ボス選択画面）を開く
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

        Debug.Log("ボス撃破！休息エリアを開放します。");
        ShowRestArea();
    }

    public void ShowRestArea()
    {
        if (restAreaPrefab != null && currentRestAreaInstance == null)
        {
            currentRestAreaInstance = Instantiate(restAreaPrefab, restPointPosition, Quaternion.identity);
        }

        if (bossSelectPanel != null)
        {
            bossSelectPanel.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    public void SelectAndStartBoss(int bossIndex)
    {
        if (bossIndex < 0 || bossIndex >= bossPrefabs.Length) return;

        if (bossSelectPanel != null)
        {
            bossSelectPanel.SetActive(false);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (currentRestAreaInstance != null)
        {
            Destroy(currentRestAreaInstance);
        }

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

    // ★ プレイヤー死亡時の処理（YOU DIED演出＆リトライ）
    public void OnPlayerDied()
    {
        StartCoroutine(GameOverRoutine());
    }

    private IEnumerator GameOverRoutine()
    {
        isStageActive = false;

        // ボスを消去
        if (currentBossInstance != null)
        {
            Destroy(currentBossInstance);
        }

        if (bossHPSlider != null)
        {
            bossHPSlider.gameObject.SetActive(false);
        }

        // 「YOU DIED」を表示
        if (gameOverText != null)
        {
            gameOverText.gameObject.SetActive(true);
        }

        // 3秒待機
        yield return new WaitForSeconds(3.0f);

        // テキスト非表示
        if (gameOverText != null)
        {
            gameOverText.gameObject.SetActive(false);
        }

        // プレイヤー再生成＆回復
        if (playerCtrl != null)
        {
            Destroy(currentPlayerInstance);
        }

        // 再度プレイヤーを生成して初期化
        Slider hpSlider = playerCtrl != null ? playerCtrl.healthSlider : null;
        Slider stSlider = playerCtrl != null ? playerCtrl.staminaSlider : null;
        TextMeshProUGUI estText = playerCtrl != null ? playerCtrl.estusText : null;

        SpawnPlayer(hpSlider, stSlider, estText);

        // 休息エリア（ボス選択）に戻す
        ShowRestArea();
    }
}