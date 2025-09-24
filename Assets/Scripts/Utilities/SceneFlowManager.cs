using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

[DisallowMultipleComponent]
public class SceneFlowManager : MonoBehaviour
{
    public static SceneFlowManager Instance { get; private set; }

    [Header("Settings")]
    public string storySceneName = "STORY1";
    [Range(0f, 5f)] public float fadeDuration = 0.8f;

    string pendingNextScene;        // 剧情后要去的场景
    bool storyFinishedFlag = false; // STORY1 发回的结束信号
    bool busy = false;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        // 确保有淡入淡出器
        if (FadeScreen.Instance == null)
        {
            var go = new GameObject("FadeScreen", typeof(FadeScreen));
            DontDestroyOnLoad(go);
        }
    }

    /// <summary>
    /// 外部入口：先切剧情，再去 nextScene
    /// </summary>
    public void BeginStoryThenLoad(string nextScene)
    {
        if (busy) return;
        pendingNextScene = nextScene;
        storyFinishedFlag = false;
        StartCoroutine(Flow_Co());
    }

    /// <summary>
    /// 由 STORY1 场景中的 StoryController 调用，表示剧情结束
    /// </summary>
    public void NotifyStoryFinished()
    {
        storyFinishedFlag = true;
    }

    IEnumerator Flow_Co()
    {
        busy = true;

        // 先黑出
        yield return FadeScreen.Instance.FadeOut(fadeDuration);

        // 进 STORY1（单场景加载，保持整洁）
        yield return SceneManager.LoadSceneAsync(storySceneName, LoadSceneMode.Single);

        // 淡入，播放剧情
        yield return FadeScreen.Instance.FadeIn(fadeDuration);

        // 等待 STORY1 发结束信号
        while (!storyFinishedFlag) yield return null;

        // 剧情结束：黑出→加载目标场景→淡入
        yield return FadeScreen.Instance.FadeOut(fadeDuration);
        if (!string.IsNullOrEmpty(pendingNextScene))
            yield return SceneManager.LoadSceneAsync(pendingNextScene, LoadSceneMode.Single);
        yield return FadeScreen.Instance.FadeIn(fadeDuration);

        busy = false;
    }
}
