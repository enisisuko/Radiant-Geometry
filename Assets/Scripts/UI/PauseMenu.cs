using FadedDreams.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PauseMenu : MonoBehaviour
{
    [Header("Wiring")]
    public GameObject panel;          // ������ť�ĸ��ڵ�
    public Button btnResume;
    public Button btnContinueFromSave;
    public Button btnBackToMain;

    bool isOpen = false;
    float cachedTimescale = 1f;

    // �����������ǰ�����״̬
    CursorLockMode _prevLock;
    bool _prevVisible;

    void Awake()
    {
        if (panel != null) panel.SetActive(false);
        if (btnResume != null) btnResume.onClick.AddListener(OnResume);
        if (btnContinueFromSave != null) btnContinueFromSave.onClick.AddListener(OnContinueFromSave);
        if (btnBackToMain != null) btnBackToMain.onClick.AddListener(OnBackToMain);

        // ȷ�������� EventSystem��û�����Զ�������
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

        // �Ȼ���ԭ״̬�����л�Ϊ�˵��õ�״̬
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

        // �ָ�����ǰ�����״̬��������ǿ������/������
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
        // �ȹرղ˵��ָ�״̬���ٶ���
        Close();
        Time.timeScale = 1f;
        SceneLoader.ReloadAtLastCheckpoint();
    }

    void OnBackToMain()
    {
        // �ȹرղ˵��ָ�״̬���ٻ����˵�
        Close();
        Time.timeScale = 1f;
        SceneLoader.LoadScene("MainMenu", null);
    }
}
