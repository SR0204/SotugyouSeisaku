using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class SceneFader : MonoBehaviour
{
    public static SceneFader Instance { get; private set; }

    [Header("フェード設定")]
    public float fadeDuration = 1.0f;

    private Image fadeImage;
    private bool isFading = false;

    void Awake()
    {
        // シーンをまたいで1つだけ常駐させる（シングルトン）
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        CreateFadeCanvas();
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // どのシーンが開かれた瞬間も、真っ暗な状態から自動でフェードインする
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (fadeImage != null)
        {
            StartCoroutine(FadeIn());
        }
    }

    // 外部（休憩所のボス選択ボタンや死亡時など）から暗転付きでシーン遷移するメソッド
    public void FadeToScene(string sceneName)
    {
        if (isFading) return;
        StartCoroutine(FadeOutAndLoad(sceneName));
    }

    // コードだけで最前面の真っ黒なフェード用Canvasを自動生成する（UIの配置ミス防止）
    private void CreateFadeCanvas()
    {
        GameObject canvasObj = new GameObject("GlobalFadeCanvas");
        canvasObj.transform.SetParent(transform);

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999; // どのUIよりも必ず一番手前に表示する

        canvasObj.AddComponent<CanvasScaler>();

        GameObject imgObj = new GameObject("FadePanel");
        imgObj.transform.SetParent(canvasObj.transform);

        fadeImage = imgObj.AddComponent<Image>();
        fadeImage.color = Color.black;
        fadeImage.raycastTarget = false;

        RectTransform rect = fadeImage.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private IEnumerator FadeIn()
    {
        isFading = true;
        fadeImage.raycastTarget = true;

        Color c = Color.black;
        c.a = 1f;
        fadeImage.color = c;

        float timer = 0f;
        while (timer < fadeDuration)
        {
            timer += Time.unscaledDeltaTime; // ポーズ中(timeScale=0)でも止まらず動く
            c.a = Mathf.Lerp(1f, 0f, timer / fadeDuration);
            fadeImage.color = c;
            yield return null;
        }

        c.a = 0f;
        fadeImage.color = c;
        fadeImage.raycastTarget = false;
        isFading = false;
    }

    private IEnumerator FadeOutAndLoad(string sceneName)
    {
        isFading = true;
        fadeImage.raycastTarget = true;

        Color c = Color.black;
        c.a = 0f;
        fadeImage.color = c;

        float timer = 0f;
        while (timer < fadeDuration)
        {
            timer += Time.unscaledDeltaTime;
            c.a = Mathf.Lerp(0f, 1f, timer / fadeDuration);
            fadeImage.color = c;
            yield return null;
        }

        c.a = 1f;
        fadeImage.color = c;
        Time.timeScale = 1f; // 念のため時間停止を解除してから遷移
        SceneManager.LoadScene(sceneName);
    }
}