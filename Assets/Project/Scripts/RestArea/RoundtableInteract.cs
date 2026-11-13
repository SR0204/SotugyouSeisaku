using UnityEngine;
using UnityEngine.SceneManagement;

public class RoundtableInteract : MonoBehaviour
{
    [Header("対象設定")]
    public Transform player;              // プレイヤーのTransform
    public float interactDistance = 4.5f; // 反応する距離

    [Header("UI参照")]
    public GameObject interactPrompt;     // 「[E] : 出撃する」のバー
    public GameObject bossSelectPanel;    // ボス選択メニュー画面

    private bool isMenuOpen = false;

    void Start()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }

        // シーン内のCanvasからUIを自動取得
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas != null)
        {
            if (interactPrompt == null)
            {
                Transform t = canvas.transform.Find("InteractPrompt");
                if (t != null) interactPrompt = t.gameObject;
            }
            if (bossSelectPanel == null)
            {
                Transform t = canvas.transform.Find("BossSelectPanel");
                if (t != null) bossSelectPanel = t.gameObject;
            }
        }

        if (interactPrompt != null) interactPrompt.SetActive(false);
        if (bossSelectPanel != null) bossSelectPanel.SetActive(false);
    }

    void Update()
    {
        if (player == null) return;

        float dist = Vector3.Distance(transform.position, player.position);

        if (!isMenuOpen)
        {
            bool canInteract = dist <= interactDistance;
            if (interactPrompt != null) interactPrompt.SetActive(canInteract);

            if (canInteract && Input.GetKeyDown(KeyCode.E))
            {
                OpenMenu();
            }
        }
        else
        {
            // ESCキーで閉じる
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CloseMenu();
            }
        }
    }

    public void OpenMenu()
    {
        isMenuOpen = true;
        if (interactPrompt != null) interactPrompt.SetActive(false);
        if (bossSelectPanel != null) bossSelectPanel.SetActive(true);

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void CloseMenu()
    {
        isMenuOpen = false;
        if (bossSelectPanel != null) bossSelectPanel.SetActive(false);

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    public void LoadBattleScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }
}