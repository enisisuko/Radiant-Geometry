using UnityEngine;
using UnityEngine.SceneManagement;
using System.Linq;

namespace FadedDreams.Core
{
    public static class SceneLoader
    {
        public static void LoadScene(string sceneName, string checkpointId = "")
        {
            // 记录到存档，便于“继续游戏”与崩溃恢复
            SaveSystem.Instance.SaveLastScene(sceneName);
            SaveSystem.Instance.SaveCheckpoint(checkpointId);

            // 用局部方法订阅，类型是 Unity 的事件签名（无需 System.Action）
            void OnSceneLoaded(Scene scene, LoadSceneMode mode)
            {
                // 只执行一次：进来就退订，避免后续重复触发
                SceneManager.sceneLoaded -= OnSceneLoaded;

                // 找到目标 Checkpoint：
                // 1) 优先使用传入的 checkpointId
                // 2) 若为空，则找激活起点（如果你的 Checkpoint 有 public bool activateOnStart 字段）
                var cps = Object.FindObjectsByType<Checkpoint>(FindObjectsSortMode.None);

                Checkpoint target = null;
                if (!string.IsNullOrEmpty(checkpointId))
                {
                    target = cps.FirstOrDefault(c => c.Id == checkpointId);
                }
                else
                {
                    // 尝试找“起始检查点”
                    foreach (var c in cps)
                    {
                        var t = c.GetType();
                        var f = t.GetField("activateOnStart");
                        if (f != null && f.FieldType == typeof(bool) && (bool)f.GetValue(c))
                        {
                            target = c;
                            break;
                        }
                    }
                }

                if (target != null)
                {
                    target.SpawnPlayerHere();
                }
                // 若 target 仍然为空，就保持默认出生点（不报错，方便关卡开发）
            }

            SceneManager.sceneLoaded += OnSceneLoaded; // 正确的事件签名
            SceneManager.LoadScene(sceneName);
        }

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
