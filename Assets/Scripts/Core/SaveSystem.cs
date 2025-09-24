using UnityEngine;
using System.IO;
using System;
using System.Collections.Generic;

namespace FadedDreams.Core
{
    /// <summary>
    /// JSON save/load using Application.persistentDataPath.
    /// </summary>
    public class SaveSystem : FadedDreams.Utilities.Singleton<SaveSystem>
    {
        private string FilePath => Path.Combine(Application.persistentDataPath, "faded_dreams_save.json");
        private SaveData data = new SaveData();

        protected override void Awake()
        {
            base.Awake();
            DontDestroyOnLoad(gameObject);
            Load();
        }

        public void SaveLastScene(string sceneName)
        {
            data.lastScene = sceneName;
            Save();
        }

        public string LoadLastScene() => data.lastScene;

        public void SaveCheckpoint(string checkpointId)
        {
            if (!string.IsNullOrEmpty(checkpointId))
            {
                data.lastCheckpoint = checkpointId;
                data.discoveredCheckpoints.Add(checkpointId);
                Save();
            }
        }

        // ÐÂÔö£ºÓÃÓÚÓë Checkpoint.cs ¼æÈÝµÄ±ã½Ý·â×°
        public void AddDiscoveredCheckpoint(string sceneName, string checkpointId)
        {
            // ¼ÇÂ¼×î½ü³¡¾°£¨¹© Continue / Reload Ê¹ÓÃ£©
            if (!string.IsNullOrEmpty(sceneName))
                SaveLastScene(sceneName);

            // ÀûÓÃÏÖÓÐµÄ SaveCheckpoint À´Ð´Èë lastCheckpoint + discoveredCheckpoints + Save()
            SaveCheckpoint(checkpointId);
        }

        public string LoadCheckpoint() => data.lastCheckpoint;

        public IEnumerable<string> GetCheckpoints() => data.discoveredCheckpoints;

        public void UnlockChapter(int chapter)
        {
            data.highestChapterUnlocked = Mathf.Max(data.highestChapterUnlocked, chapter);
            Save();
        }

        public int HighestChapterUnlocked() => data.highestChapterUnlocked;

        public void ResetAll()
        {
            data = new SaveData();
               // 同时清空“话语册”的 PlayerPrefs 键，避免新开局残留
            //SayingBook.Clear();
            Save();
        }

        private void Save()
        {
            try
            {
                var json = JsonUtility.ToJson(data, true);
                File.WriteAllText(FilePath, json);
            } catch (Exception e) { Debug.LogError(e); }
        }

        private void Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var json = File.ReadAllText(FilePath);
                    data = JsonUtility.FromJson<SaveData>(json);
                    if (data == null) data = new SaveData();
                }
            } catch (Exception e) { Debug.LogError(e); data = new SaveData(); }
        }
    }
}
