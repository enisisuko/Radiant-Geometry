using FadedDreams.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PauseMenu : MonoBehaviour
{
    [Header("Wiring")]
    public GameObject panel;          // 包含按钮的根节点
    public Button btnResume;
    public Button btnContinueFromSave;
    public Button btnBackToMain;

    bool isOpen = false;
    float cachedTimescale = 1f;

    // 新增：缓存打开前的鼠标状态
    CursorLockMode _prevLock;
    bool _prevVisible;

    void Awake()
    {
        if (panel != null) panel.SetActive(false);
        if (btnResume != null) btnResume.onClick.AddListener(OnResume);
        if (btnContinueFromSave != null) btnContinueFromSave.onClick.AddListener(OnContinueFromSave);
        if (btnBackToMain != null) btnBackToMain.onClick.AddListener(OnBackToMain);

        // 确保场景有 EventSystem（没有则自动创建）
        if (FindFirstObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            DontDestroyOnLoad(es);
        }
    }

    public bool IsOpen() => isOpen;

    public void Toggle()
    {
        if (isOpen) Close();
        else Open();
    }

    public void Open()
    {
        if (isOpen) return;

        cachedTimescale = Mathf.Max(1f, Time.timeScale);
        Time.timeScale = 0f;

        // 先缓存原状态，再切换为菜单用的状态
        _prevLock = Cursor.lockState;
        _prevVisible = Cursor.visible;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (panel != null) panel.SetActive(true);
        if (btnResume != null) EventSystem.current.SetSelectedGameObject(btnResume.gameObject);

        isOpen = true;
    }

    public void Close()
    {
        if (!isOpen) return;

        Time.timeScale = cachedTimescale;

        // 恢复到打开前的鼠标状态（而不是强行隐藏/锁定）
        Cursor.lockState = _prevLock;
        Cursor.visible = _prevVisible;

        if (panel != null) panel.SetActive(false);
        isOpen = false;
    }

    void OnResume()
    {
        Close();
    }

    void OnContinueFromSave()
    {
        // 先关闭菜单恢复状态，再读档
        Close();
        Time.timeScale = 1f;
        SceneLoader.ReloadAtLastCheckpoint();
    }

    void OnBackToMain()
    {
        // 先关闭菜单恢复状态，再回主菜单
        Close();
        Time.timeScale = 1f;
        SceneLoader.LoadScene("MainMenu", null);
    }
}
