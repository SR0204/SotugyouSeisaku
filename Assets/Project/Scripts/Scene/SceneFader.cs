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
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        CreateFadeCanvas();
        SetAlpha(1f); // 起動瞬間から真っ黒にしておく
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (fadeImage != null)
        {
            StopAllCoroutines();
            SetAlpha(1f); // シーンが切り替わった瞬間、描画前に必ず真っ黒を保証する
            StartCoroutine(FadeIn());
        }
    }

    public void FadeToScene(string sceneName)
    {
        if (isFading) return;
        StopAllCoroutines();
        StartCoroutine(FadeOutAndLoad(sceneName));
    }

    private void SetAlpha(float alpha)
    {
        if (fadeImage == null) return;
        Color c = Color.black;
        c.a = alpha;
        fadeImage.color = c;
    }

    private void CreateFadeCanvas()
    {
        GameObject canvasObj = new GameObject("GlobalFadeCanvas");
        canvasObj.transform.SetParent(transform);

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;

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
        SetAlpha(1f);

        // シーン初期化が落ち着くまで1フレーム真っ黒のまま待つ
        yield return null;

        float timer = 0f;
        while (timer < fadeDuration)
        {
            timer += Time.unscaledDeltaTime;
            SetAlpha(Mathf.Lerp(1f, 0f, timer / fadeDuration));
            yield return null;
        }

        SetAlpha(0f);
        fadeImage.raycastTarget = false;
        isFading = false;
    }

    private IEnumerator FadeOutAndLoad(string sceneName)
    {
        isFading = true;
        fadeImage.raycastTarget = true;

        float timer = 0f;
        while (timer < fadeDuration)
        {
            timer += Time.unscaledDeltaTime;
            SetAlpha(Mathf.Lerp(0f, 1f, timer / fadeDuration));
            yield return null;
        }

        SetAlpha(1f);
        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
    }
}