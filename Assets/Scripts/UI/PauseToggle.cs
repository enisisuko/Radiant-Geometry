using FadedDreams.Core;
using UnityEngine;

public class PauseToggle : MonoBehaviour
{
    [Tooltip("限制仅在 1~4 章可用")]
    public bool restrictToCh1To4 = true;

    PauseMenu pauseMenu;
    GameManager gameManager; // 如有维护当前章节则优先取这里

    void Start()
    {
        pauseMenu = FindFirstObjectByType<PauseMenu>();
        gameManager = FindFirstObjectByType<GameManager>();
    }

    bool IsChapterAllowed()
    {
        if (!restrictToCh1To4) return true;

        int ch = 0;
        if (gameManager != null) ch = Mathf.Max(ch, gameManager.CurrentChapter);
        // 静态方法需用类型名调用
        ch = Mathf.Max(ch, ChapterManager.ChapterFromScene());
        return ch >= 1 && ch <= 4;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (!IsChapterAllowed()) return;

            if (pauseMenu == null)
            {
                pauseMenu = FindFirstObjectByType<PauseMenu>();
                if (pauseMenu == null)
                {
                    Debug.LogWarning("PauseToggle: PauseMenu not found in scene.");
                    return;
                }
            }
            pauseMenu.Toggle();
        }
    }
}
