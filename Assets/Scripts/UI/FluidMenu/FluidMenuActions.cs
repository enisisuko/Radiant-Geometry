// FluidMenuActions.cs
// 菜单动作 - 负责菜单项功能执行、场景切换和游戏状态管理
// Unity 2021+ / Unity 6.2 兼容（URP/HDRP均可）

using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

namespace FadedDreams.UI
{
    /// <summary>
    /// 流体菜单动作 - 负责菜单项功能执行、场景切换和游戏状态管理
    /// </summary>
    public class FluidMenuActions : MonoBehaviour
    {
        [Header("== 场景配置 ==")]
        public string newGameScene = "STORY0";
        public string firstCheckpointId = "101";

        [Header("== 调试 ==")]
        public bool verboseLogs = true;

        // 组件引用
        private FluidMenuCore core;

        // 状态管理
        private bool isActionInProgress = false;

        // 事件
        public event System.Action OnNewGameStarted;
        public event System.Action OnContinueGameStarted;
        public event System.Action OnCoopModeStarted;
        public event System.Action OnQuitGameStarted;
        public event System.Action OnSupportMeStarted;
        public event System.Action OnActionCompleted;

        #region Unity Lifecycle

        private void Awake()
        {
            core = GetComponent<FluidMenuCore>();
        }

        #endregion

        #region Menu Actions

        /// <summary>
        /// 新游戏
        /// </summary>
        public void NewGame()
        {
            if (isActionInProgress) return;

            if (verboseLogs)
                Debug.Log("[FluidMenuActions] Starting new game");

            isActionInProgress = true;
            OnNewGameStarted?.Invoke();

            // 停止背景音乐
            if (core != null)
            {
                core.StopBackgroundMusic();
            }

            // 加载新游戏场景
            StartCoroutine(LoadSceneCoroutine(newGameScene));
        }

        /// <summary>
        /// 继续游戏
        /// </summary>
        public void ContinueGame()
        {
            if (isActionInProgress) return;

            if (verboseLogs)
                Debug.Log("[FluidMenuActions] Continuing game");

            isActionInProgress = true;
            OnContinueGameStarted?.Invoke();

            // 停止背景音乐
            if (core != null)
            {
                core.StopBackgroundMusic();
            }

            // 这里应该加载保存的游戏状态
            // 暂时加载新游戏场景
            StartCoroutine(LoadSceneCoroutine(newGameScene));
        }

        /// <summary>
        /// 双人模式
        /// </summary>
        public void CoopMode()
        {
            if (isActionInProgress) return;

            if (verboseLogs)
                Debug.Log("[FluidMenuActions] Starting coop mode");

            isActionInProgress = true;
            OnCoopModeStarted?.Invoke();

            // 停止背景音乐
            if (core != null)
            {
                core.StopBackgroundMusic();
            }

            // 这里应该加载双人模式场景
            // 暂时加载新游戏场景
            StartCoroutine(LoadSceneCoroutine(newGameScene));
        }

        /// <summary>
        /// 退出游戏
        /// </summary>
        public void QuitGame()
        {
            if (isActionInProgress) return;

            if (verboseLogs)
                Debug.Log("[FluidMenuActions] Quitting game");

            isActionInProgress = true;
            OnQuitGameStarted?.Invoke();

            // 停止背景音乐
            if (core != null)
            {
                core.StopBackgroundMusic();
            }

            // 退出游戏
            StartCoroutine(QuitGameCoroutine());
        }

        /// <summary>
        /// 支持我
        /// </summary>
        public void SupportMe()
        {
            if (isActionInProgress) return;

            if (verboseLogs)
                Debug.Log("[FluidMenuActions] Opening support page");

            isActionInProgress = true;
            OnSupportMeStarted?.Invoke();

            // 打开支持页面
            StartCoroutine(OpenSupportPageCoroutine());
        }

        #endregion

        #region Coroutines

        /// <summary>
        /// 加载场景协程
        /// </summary>
        private IEnumerator LoadSceneCoroutine(string sceneName)
        {
            // 等待一帧确保UI更新
            yield return null;

            try
            {
                // 加载场景
                SceneManager.LoadScene(sceneName);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[FluidMenuActions] Failed to load scene {sceneName}: {e.Message}");
            }

            isActionInProgress = false;
            OnActionCompleted?.Invoke();
        }

        /// <summary>
        /// 退出游戏协程
        /// </summary>
        private IEnumerator QuitGameCoroutine()
        {
            // 等待一帧确保UI更新
            yield return null;

            try
            {
                // 在编辑器中停止播放
                #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
                #else
                // 在构建版本中退出应用
                Application.Quit();
                #endif
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[FluidMenuActions] Failed to quit game: {e.Message}");
            }

            isActionInProgress = false;
            OnActionCompleted?.Invoke();
        }

        /// <summary>
        /// 打开支持页面协程
        /// </summary>
        private IEnumerator OpenSupportPageCoroutine()
        {
            // 等待一帧确保UI更新
            yield return null;

            try
            {
                // 这里可以打开支持页面或链接
                // 例如：Application.OpenURL("https://your-support-page.com");
                
                if (verboseLogs)
                    Debug.Log("[FluidMenuActions] Support page opened");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[FluidMenuActions] Failed to open support page: {e.Message}");
            }

            isActionInProgress = false;
            OnActionCompleted?.Invoke();
        }

        #endregion

        #region Game State Management

        /// <summary>
        /// 保存游戏状态
        /// </summary>
        public void SaveGameState()
        {
            try
            {
                // 这里应该保存游戏状态
                // 例如：PlayerPrefs.SetString("GameState", gameStateJson);
                
                if (verboseLogs)
                    Debug.Log("[FluidMenuActions] Game state saved");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[FluidMenuActions] Failed to save game state: {e.Message}");
            }
        }

        /// <summary>
        /// 加载游戏状态
        /// </summary>
        public void LoadGameState()
        {
            try
            {
                // 这里应该加载游戏状态
                // 例如：string gameStateJson = PlayerPrefs.GetString("GameState", "");
                
                if (verboseLogs)
                    Debug.Log("[FluidMenuActions] Game state loaded");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[FluidMenuActions] Failed to load game state: {e.Message}");
            }
        }

        /// <summary>
        /// 检查是否有保存的游戏
        /// </summary>
        public bool HasSaveGame()
        {
            try
            {
                // 这里应该检查是否有保存的游戏
                // 例如：return PlayerPrefs.HasKey("GameState");
                return false; // 暂时返回false
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[FluidMenuActions] Failed to check save game: {e.Message}");
                return false;
            }
        }

        #endregion

        #region Scene Management

        /// <summary>
        /// 加载场景
        /// </summary>
        public void LoadScene(string sceneName)
        {
            if (isActionInProgress) return;

            if (verboseLogs)
                Debug.Log($"[FluidMenuActions] Loading scene: {sceneName}");

            isActionInProgress = true;

            // 停止背景音乐
            if (core != null)
            {
                core.StopBackgroundMusic();
            }

            StartCoroutine(LoadSceneCoroutine(sceneName));
        }

        /// <summary>
        /// 重新加载当前场景
        /// </summary>
        public void ReloadCurrentScene()
        {
            string currentScene = SceneManager.GetActiveScene().name;
            LoadScene(currentScene);
        }

        #endregion

        #region Public API

        /// <summary>
        /// 检查是否有动作正在进行
        /// </summary>
        public bool IsActionInProgress()
        {
            return isActionInProgress;
        }

        /// <summary>
        /// 获取新游戏场景
        /// </summary>
        public string GetNewGameScene()
        {
            return newGameScene;
        }

        /// <summary>
        /// 获取第一个检查点ID
        /// </summary>
        public string GetFirstCheckpointId()
        {
            return firstCheckpointId;
        }

        /// <summary>
        /// 设置新游戏场景
        /// </summary>
        public void SetNewGameScene(string sceneName)
        {
            newGameScene = sceneName;

            if (verboseLogs)
                Debug.Log($"[FluidMenuActions] New game scene set to: {sceneName}");
        }

        /// <summary>
        /// 设置第一个检查点ID
        /// </summary>
        public void SetFirstCheckpointId(string checkpointId)
        {
            firstCheckpointId = checkpointId;

            if (verboseLogs)
                Debug.Log($"[FluidMenuActions] First checkpoint ID set to: {checkpointId}");
        }

        /// <summary>
        /// 取消当前动作
        /// </summary>
        public void CancelCurrentAction()
        {
            if (isActionInProgress)
            {
                isActionInProgress = false;
                OnActionCompleted?.Invoke();

                if (verboseLogs)
                    Debug.Log("[FluidMenuActions] Current action cancelled");
            }
        }

        /// <summary>
        /// 重置动作系统
        /// </summary>
        public void ResetActions()
        {
            isActionInProgress = false;

            if (verboseLogs)
                Debug.Log("[FluidMenuActions] Actions reset");
        }

        #endregion

        #region Debug

        /// <summary>
        /// 获取调试信息
        /// </summary>
        public string GetDebugInfo()
        {
            return $"Action In Progress: {isActionInProgress}, New Game Scene: {newGameScene}, Checkpoint ID: {firstCheckpointId}";
        }

        #endregion
    }
}
