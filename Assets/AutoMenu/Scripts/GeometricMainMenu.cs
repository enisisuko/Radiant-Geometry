using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public class GeometricMainMenu : MonoBehaviour
{
    [Header("References")]
    public Camera mainCam;
    public Transform coreCrystal;
    public GeometricMenuItem[] items; // 顺序：New, Continue, Coop, DLC, Quit
    public WhiteFlashTransition flash;

    [Header("Raycast")]
    public LayerMask interactMask = ~0;
    public float rayMaxDistance = 100f;

    [Header("Input")]
    public string horizontalAxis = "Horizontal";
    public string verticalAxis   = "Vertical";
    public KeyCode confirmKey    = KeyCode.Return;
    public KeyCode confirmAltKey = KeyCode.Space;
    public KeyCode backKey       = KeyCode.Escape;

    [Header("Actions")]
    public UnityEvent onNewGame;
    public UnityEvent onContinue;
    public UnityEvent onCoop;
    public UnityEvent onDLC;
    public UnityEvent onQuit;

    int _index = 0;
    float _moveCd = 0f;
    const float MoveRepeatDelay = 0.2f;

    void Reset()
    {
        mainCam = Camera.main;
    }

    void Start()
    {
        if (!mainCam) mainCam = Camera.main;

        bool hasSave = PlayerPrefs.GetInt("HasSave", 0) == 1;
        if (items != null && items.Length > 1 && items[1] != null)
        {
            items[1].SetInteractable(hasSave);
        }

        HighlightIndex(_index);
    }

    void Update()
    {
        HandleMouseHover();
        HandleDirectionalSelection();

        if (Input.GetKeyDown(confirmKey) || Input.GetKeyDown(confirmAltKey) || Input.GetButtonDown("Submit"))
        {
            ConfirmCurrent();
        }

        if (Input.GetKeyDown(backKey))
        {
            SetIndex(items.Length - 1);
            ConfirmCurrent();
        }
    }

    void HandleMouseHover()
    {
        if (!mainCam) return;

        Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out var hit, rayMaxDistance, interactMask))
        {
            var item = hit.collider.GetComponentInParent<GeometricMenuItem>();
            if (item)
            {
                int idx = System.Array.IndexOf(items, item);
                if (idx >= 0) SetIndex(idx);

                if (Input.GetMouseButtonDown(0))
                {
                    ConfirmCurrent();
                }
            }
        }

        for (int i = 0; i < items.Length; i++)
        {
            if (!items[i]) continue;
            items[i].SetHovered(i == _index && items[i].interactable);
        }
    }

    void HandleDirectionalSelection()
    {
        _moveCd -= Time.deltaTime;
        if (_moveCd > 0f) return;

        float h = Input.GetAxisRaw(horizontalAxis);
        float v = Input.GetAxisRaw(verticalAxis);

        int delta = 0;
        if (Mathf.Abs(h) > 0.5f) delta = (h > 0f ? 1 : -1);
        else if (Mathf.Abs(v) > 0.5f) delta = (v < 0f ? 1 : -1);

        if (delta != 0)
        {
            int tries = items.Length;
            int next = _index;

            while (tries-- > 0)
            {
                next = (next + delta + items.Length) % items.Length;
                if (items[next] && items[next].interactable) break;
            }

            SetIndex(next);
            _moveCd = MoveRepeatDelay;
        }

        for (int i = 0; i < items.Length; i++)
        {
            if (!items[i]) continue;
            items[i].SetHovered(i == _index && items[i].interactable);
        }
    }

    void SetIndex(int idx)
    {
        _index = Mathf.Clamp(idx, 0, items.Length - 1);
    }

    void HighlightIndex(int idx)
    {
        for (int i = 0; i < items.Length; i++)
        {
            if (!items[i]) continue;
            items[i].SetHovered(i == idx && items[i].interactable);
        }
    }

    void ConfirmCurrent()
    {
        if (_index < 0 || _index >= items.Length) return;
        var item = items[_index];
        if (!item || !item.interactable) return;

        item.TriggerConfirmPulse();

        switch (_index)
        {
            case 0: PlayConfirmAndInvoke(onNewGame);  break;
            case 1: PlayConfirmAndInvoke(onContinue); break;
            case 2: PlayConfirmAndInvoke(onCoop);     break;
            case 3: PlayConfirmAndInvoke(onDLC);      break;
            case 4: PlayConfirmAndInvoke(onQuit);     break;
        }
    }

    void PlayConfirmAndInvoke(UnityEvent e)
    {
        if (flash) flash.Blast(() => { e?.Invoke(); });
        else e?.Invoke();
    }

    public void Action_NewGame_LoadScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    public void Action_Quit()
    {
        Application.Quit();
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }
}
