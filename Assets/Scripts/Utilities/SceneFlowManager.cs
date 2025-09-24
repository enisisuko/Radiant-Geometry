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

    string pendingNextScene;        // �����Ҫȥ�ĳ���
    bool storyFinishedFlag = false; // STORY1 ���صĽ����ź�
    bool busy = false;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        // ȷ���е��뵭����
        if (FadeScreen.Instance == null)
        {
            var go = new GameObject("FadeScreen", typeof(FadeScreen));
            DontDestroyOnLoad(go);
        }
    }

    /// <summary>
    /// �ⲿ��ڣ����о��飬��ȥ nextScene
    /// </summary>
    public void BeginStoryThenLoad(string nextScene)
    {
        if (busy) return;
        pendingNextScene = nextScene;
        storyFinishedFlag = false;
        StartCoroutine(Flow_Co());
    }

    /// <summary>
    /// �� STORY1 �����е� StoryController ���ã���ʾ�������
    /// </summary>
    public void NotifyStoryFinished()
    {
        storyFinishedFlag = true;
    }

    IEnumerator Flow_Co()
    {
        busy = true;

        // �Ⱥڳ�
        yield return FadeScreen.Instance.FadeOut(fadeDuration);

        // �� STORY1�����������أ��������ࣩ
        yield return SceneManager.LoadSceneAsync(storySceneName, LoadSceneMode.Single);

        // ���룬���ž���
        yield return FadeScreen.Instance.FadeIn(fadeDuration);

        // �ȴ� STORY1 �������ź�
        while (!storyFinishedFlag) yield return null;

        // ����������ڳ�������Ŀ�곡��������
        yield return FadeScreen.Instance.FadeOut(fadeDuration);
        if (!string.IsNullOrEmpty(pendingNextScene))
            yield return SceneManager.LoadSceneAsync(pendingNextScene, LoadSceneMode.Single);
        yield return FadeScreen.Instance.FadeIn(fadeDuration);

        busy = false;
    }
}
