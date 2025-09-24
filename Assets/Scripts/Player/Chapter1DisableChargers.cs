using UnityEngine;

// 第一章时禁用旧的“长按左键蓄力/发射”相关组件，离开第一章恢复。
public class Chapter1DisableChargers : MonoBehaviour
{
    [SerializeField] private Behaviour[] toDisableInChapter1;

    [SerializeField] private bool onlyChapterOne = true;
    [SerializeField] private int chapterIndex = 1;

    void OnEnable() => Apply();
    void Update() => Apply();

    private void Apply()
    {
        bool inChap1 = true;
        if (onlyChapterOne)
        {
            try
            {
                var gmType = System.Type.GetType("GameManager");
                if (gmType != null)
                {
                    var inst = gmType.GetProperty("Instance")?.GetValue(null);
                    int chap = (int)(gmType.GetProperty("CurrentChapter")?.GetValue(inst) ?? 1);
                    inChap1 = (chap == chapterIndex);
                }
            }
            catch { inChap1 = true; }
        }

        if (toDisableInChapter1 != null)
        {
            foreach (var b in toDisableInChapter1)
            {
                if (!b) continue;
                b.enabled = !inChap1 ? true : false;
            }
        }
    }
}
