using UnityEngine;
using UnityEngine.UI;

public class RestAreaManager : MonoBehaviour
{
    [Header("プレハブ設定")]
    public GameObject uiCanvasPrefab;
    public GameObject restAreaPrefab;
    public GameObject playerPrefab;

    [Header("生成位置調整")]
    public Vector3 restAreaSpawnPos = Vector3.zero;
    [Tooltip("プレイヤーの初期位置（円卓の手前）")]
    public Vector3 playerSpawnPos = new Vector3(0, 0.1f, -5.0f);

    [Header("インタラクト設定")]
    [Tooltip("円卓に近づいて調べられる距離")]
    public float interactDistance = 6.0f;

    private GameObject bossSelectPanel;
    private GameObject playerInstance;
    private Transform targetInteractObj; // 判定対象（Table または 親）
    private bool isUIOpen = false;

    void Start()
    {
        // 1. 拠点プレハブを生成
        if (restAreaPrefab != null)
        {
            GameObject restArea = Instantiate(restAreaPrefab, restAreaSpawnPos, Quaternion.identity);

            // "Table" という名前の子オブジェクトがあればそれを判定対象に、なければ全体を対象に
            Transform tableChild = restArea.transform.Find("Table");
            targetInteractObj = (tableChild != null) ? tableChild : restArea.transform;
        }

        Physics.SyncTransforms();

        // 2. プレイヤーを生成
        if (playerPrefab != null)
        {
            playerInstance = Instantiate(playerPrefab, playerSpawnPos, Quaternion.identity);

            if (Camera.main != null)
            {
                ThirdPersonCamera cam = Camera.main.GetComponent<ThirdPersonCamera>();
                if (cam != null)
                {
                    cam.target = playerInstance.transform;
                    cam.InitCameraPosition();
                }
            }
        }

        // 3. UI Canvas を生成
        if (uiCanvasPrefab != null)
        {
            GameObject canvasObj = Instantiate(uiCanvasPrefab);

            // 拠点ではボスHPバーを非表示にする
            Transform bossHPObj = canvasObj.transform.Find("BossHPBar");
            if (bossHPObj != null) bossHPObj.gameObject.SetActive(false);

            Transform panelObj = canvasObj.transform.Find("BossSelectPanel");
            if (panelObj != null)
            {
                bossSelectPanel = panelObj.gameObject;
                bossSelectPanel.SetActive(false); // 初期化時は閉じる

                Button[] buttons = bossSelectPanel.GetComponentsInChildren<Button>();
                for (int i = 0; i < buttons.Length; i++)
                {
                    int bossIndex = i;
                    buttons[i].onClick.RemoveAllListeners();
                    buttons[i].onClick.AddListener(() => OnSelectBoss(bossIndex));
                }
            }
        }

        CloseBossSelectUI();
    }

    void Update()
    {
        if (playerInstance == null || targetInteractObj == null) return;

        // プレイヤーと円卓（テーブル）の水平距離を計算
        Vector3 playerPosXZ = new Vector3(playerInstance.transform.position.x, 0, playerInstance.transform.position.z);
        Vector3 targetPosXZ = new Vector3(targetInteractObj.position.x, 0, targetInteractObj.position.z);
        float dist = Vector3.Distance(playerPosXZ, targetPosXZ);

        if (!isUIOpen)
        {
            // 円卓の近く（指定距離内）で E キーを押すとメニューオープン
            if (dist <= interactDistance && Input.GetKeyDown(KeyCode.E))
            {
                OpenBossSelectUI();
            }
        }
        else
        {
            // UIが開いているときに ESC キーを押すと閉じる
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CloseBossSelectUI();
            }
        }
    }

    public void OpenBossSelectUI()
    {
        if (bossSelectPanel != null) bossSelectPanel.SetActive(true);
        isUIOpen = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void CloseBossSelectUI()
    {
        if (bossSelectPanel != null) bossSelectPanel.SetActive(false);
        isUIOpen = false;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void OnSelectBoss(int bossIndex)
    {
        GameManager.LoadBattleScene(bossIndex);
    }
}