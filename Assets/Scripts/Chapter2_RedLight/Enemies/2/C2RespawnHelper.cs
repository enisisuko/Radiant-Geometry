using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FadedDreams.Bosses
{
    public static class C2RespawnHelper
    {
        private static bool _respawning;

        public static void TryReloadLastCheckpointSafe()
        {
            if (_respawning) return;
            _respawning = true;

            // 1) 反射查找任意类的静态 ReloadAtLastCheckpoint()
            try
            {
                foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    foreach (var t in asm.GetTypes())
                    {
                        var m = t.GetMethod("ReloadAtLastCheckpoint",
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                        if (m != null) { m.Invoke(null, null); _respawning = false; return; }
                    }
                }
            }
            catch { /* ignore */ }

            // 2) 退而求其次：找场景中的“Checkpoint 管理器”实例并尝试调用 ReloadLast()
            try
            {
                var type = System.Type.GetType("CheckpointManager");
                if (type != null)
                {
                    var inst = Object.FindObjectOfType(type);
                    if (inst != null)
                    {
                        var m2 = type.GetMethod("ReloadLast", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (m2 != null) { m2.Invoke(inst, null); _respawning = false; return; }
                    }
                }
            }
            catch { /* ignore */ }

            // 3) 最后兜底：重载当前关卡（至少不会卡死/报错）
            try
            {
                var s = SceneManager.GetActiveScene().name;
                SceneManager.LoadScene(s);
            }
            catch { /* ignore */ }
            finally { _respawning = false; }
        }
    }
}
