// Assets/Scripts/Core/SceneLoader.cs
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Linq;

namespace FadedDreams.Core
{
    // 场景加载与检查点重生的统一入口
    public static class SceneLoader
    {
        /// <summary>
        /// 加载指定场景；若提供 checkpointId，则在场景加载完成后把玩家放到该检查点。
        /// 仅当 checkpointId 非空时才写入存档，避免把空串覆盖掉 lastCheckpoint。
        /// </summary>
        public static void LoadScene(string sceneName, string checkpointId = "")
        {
            SaveSystem.Instance.SaveLastScene(sceneName);
            if (!string.IsNullOrEmpty(checkpointId))
                SaveSystem.Instance.SaveCheckpoint(checkpointId); // 只有非空才写

            void OnSceneLoaded(Scene scene, LoadSceneMode mode)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;

                var cps = Object.FindObjectsByType<Checkpoint>(FindObjectsSortMode.None);

                Checkpoint target = null;
                if (!string.IsNullOrEmpty(checkpointId))
                {
                    target = cps.FirstOrDefault(c => c.Id == checkpointId);
                }
                else
                {
                    // 若未指定 cp，优先找打了“起始检查点”的
                    foreach (var c in cps)
                    {
                        if (c.activateOnStart) { target = c; break; }
                    }
                    // 兜底：都没有时，用场景中的第一个检查点（若存在）
                    if (target == null && cps.Length > 0)
                        target = cps[0];
                }

                if (target != null)
                    target.SpawnPlayerHere();
                // 若仍为 null，则保持默认出生点
            }

            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.LoadScene(sceneName);
        }

        /// <summary>
        /// 从存档读取“最后场景+最后检查点”并重载。
        /// </summary>
        public static void ReloadAtLastCheckpoint()
        {
            var lastScene = SaveSystem.Instance.LoadLastScene();
            var cp = SaveSystem.Instance.LoadCheckpoint();

            if (string.IsNullOrEmpty(lastScene))
                lastScene = SceneManager.GetActiveScene().name;

            LoadScene(lastScene, cp);
        }
    }
}
