using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class TitleManager : MonoBehaviour
{
    [Header("遷移先のシーン名")]
    public string nextSceneName = "RestAreaScene";

    [Header("フェード用パネル（黒いImage）")]
    public Image fadePanel;
    public float fadeDuration = 1.5f;

    private bool isTransitioning = false;

    void Start()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        if (fadePanel != null)
        {
            fadePanel.gameObject.SetActive(true);
            StartCoroutine(FadeIn());
        }
    }

    public void OnClickStart()
    {
        if (isTransitioning) return;
        isTransitioning = true;

        if (fadePanel != null)
        {
            StartCoroutine(FadeOutAndLoad());
        }
        else
        {
            SceneManager.LoadScene(nextSceneName);
        }
    }

    private IEnumerator FadeIn()
    {
        fadePanel.raycastTarget = true;
        Color c = fadePanel.color;
        float timer = 0f;

        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            c.a = Mathf.Lerp(1f, 0f, timer / fadeDuration);
            fadePanel.color = c;
            yield return null;
        }

        c.a = 0f;
        fadePanel.color = c;
        // 透明になった後はボタンのクリックを邪魔しないようにする
        fadePanel.raycastTarget = false;
    }

    private IEnumerator FadeOutAndLoad()
    {
        fadePanel.raycastTarget = true;
        Color c = fadePanel.color;
        float timer = 0f;

        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            c.a = Mathf.Lerp(0f, 1f, timer / fadeDuration);
            fadePanel.color = c;
            yield return null;
        }

        c.a = 1f;
        fadePanel.color = c;
        SceneManager.LoadScene(nextSceneName);
    }
}