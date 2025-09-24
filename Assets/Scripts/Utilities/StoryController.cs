using UnityEngine;

public class StoryController : MonoBehaviour
{
    [Tooltip("是否在进入场景后自动等待若干秒再结束（可用于测试）")]
    public bool autoEndForTest = false;
    public float autoEndDelay = 3f;

    void Start()
    {
        if (autoEndForTest) Invoke(nameof(EndStory), autoEndDelay);
    }

    /// <summary>
    /// 在剧情真正结束时调用；可由 Timeline Signal、对话回调、按钮等触发
    /// </summary>
    public void EndStory()
    {
        if (SceneFlowManager.Instance != null)
            SceneFlowManager.Instance.NotifyStoryFinished();
        else
            Debug.LogWarning("SceneFlowManager not found. Make sure it exists in bootstrap scene.");
    }
}
