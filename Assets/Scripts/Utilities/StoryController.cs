using UnityEngine;

public class StoryController : MonoBehaviour
{
    [Tooltip("�Ƿ��ڽ��볡�����Զ��ȴ��������ٽ����������ڲ��ԣ�")]
    public bool autoEndForTest = false;
    public float autoEndDelay = 3f;

    void Start()
    {
        if (autoEndForTest) Invoke(nameof(EndStory), autoEndDelay);
    }

    /// <summary>
    /// �ھ�����������ʱ���ã����� Timeline Signal���Ի��ص�����ť�ȴ���
    /// </summary>
    public void EndStory()
    {
        if (SceneFlowManager.Instance != null)
            SceneFlowManager.Instance.NotifyStoryFinished();
        else
            Debug.LogWarning("SceneFlowManager not found. Make sure it exists in bootstrap scene.");
    }
}
