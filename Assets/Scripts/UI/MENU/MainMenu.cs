using UnityEngine;
using FadedDreams.Core;
using FadedDreams.World;   // LightSource2D

namespace FadedDreams.UI
{
    public class MainMenu : MonoBehaviour
    {
        [Header("New Game Defaults")]
        public string firstScene = "Chapter1";
        public string firstCheckpointId = "101"; // 确保场景里有 Id=101 的 Checkpoint

        public void NewGame()
        {
            //// 1) 告诉所有光源：按各自 startOn 恢复并写回持久化
            //LightSource2D.NewGameApplyStartOnDefaults();

            // 2) 清 JSON 存档（检查点/章节集合等）
            SaveSystem.Instance.ResetAll();

            // 3) 预写入新开局默认起点（便于 Continue 按钮）
            SaveSystem.Instance.SaveLastScene(firstScene);
            SaveSystem.Instance.SaveCheckpoint(firstCheckpointId);

            // 4) 进入场景并在指定检查点出生
            SceneLoader.LoadScene(firstScene, firstCheckpointId);
        }

        public void ContinueGame()
        {
            var scene = SaveSystem.Instance.LoadLastScene();
            if (string.IsNullOrEmpty(scene)) scene = firstScene;
            SceneLoader.LoadScene(scene, SaveSystem.Instance.LoadCheckpoint());
        }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
