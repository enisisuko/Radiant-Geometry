using UnityEngine;

public class TriggerLoadOnTouch : MonoBehaviour
{
    [Tooltip("只响应该 Tag 的碰撞体（默认 Player）")]
    public string requiredTag = "Player";

    [Tooltip("剧情结束后要去的目标场景名")]
    public string nextSceneName = "Level_02";

    [Tooltip("同一帧多次进入的防抖")]
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
            // 若当前场景还没放 SceneFlowManager，则临时放一个
            var go = new GameObject("SceneFlowManager", typeof(SceneFlowManager));
            DontDestroyOnLoad(go);
        }
        SceneFlowManager.Instance.BeginStoryThenLoad(nextSceneName);
        used = true;
    }
}
