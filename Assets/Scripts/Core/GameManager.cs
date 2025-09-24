using UnityEngine;
using FadedDreams.Utilities;
using FadedDreams.Core;

namespace FadedDreams.Core
{
    /// <summary>
    /// Global entry, keeps references and high-level state.
    /// </summary>
    public class GameManager : Singleton<GameManager>
    {
        public int CurrentChapter { get; private set; } = 1;
        public string CurrentCheckpointId { get; private set; } = "";

        public void SetChapter(int chapter)
        {
            CurrentChapter = Mathf.Clamp(chapter, 1, 4);
        }

        public void SetCheckpoint(string checkpointId)
        {
            CurrentCheckpointId = checkpointId;
            SaveSystem.Instance.SaveCheckpoint(checkpointId);
        }

        public void OnPlayerDeath()
        {
            // Respawn at last checkpoint of current scene
            SceneLoader.ReloadAtLastCheckpoint();
        }
    }
}
