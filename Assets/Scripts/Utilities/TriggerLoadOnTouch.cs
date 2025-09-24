using UnityEngine;

public class TriggerLoadOnTouch : MonoBehaviour
{
    [Tooltip("ֻ��Ӧ�� Tag ����ײ�壨Ĭ�� Player��")]
    public string requiredTag = "Player";

    [Tooltip("���������Ҫȥ��Ŀ�곡����")]
    public string nextSceneName = "Level_02";

    [Tooltip("ͬһ֡��ν���ķ���")]
    public bool oneShot = true;

    bool used = false;

    void Reset()
    {
        var col2d = GetComponent<Collider2D>();
        if (col2d) col2d.isTrigger = true;
        var col3d = GetComponent<Collider>();
        if (col3d) col3d.isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (used && oneShot) return;
        if (!string.IsNullOrEmpty(requiredTag) && !other.CompareTag(requiredTag)) return;
        Fire();
    }

    void OnTriggerEnter(Collider other)
    {
        if (used && oneShot) return;
        if (!string.IsNullOrEmpty(requiredTag) && !other.CompareTag(requiredTag)) return;
        Fire();
    }

    void Fire()
    {
        if (SceneFlowManager.Instance == null)
        {
            // ����ǰ������û�� SceneFlowManager������ʱ��һ��
            var go = new GameObject("SceneFlowManager", typeof(SceneFlowManager));
            DontDestroyOnLoad(go);
        }
        SceneFlowManager.Instance.BeginStoryThenLoad(nextSceneName);
        used = true;
    }
}
