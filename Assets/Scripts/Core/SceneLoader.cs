using UnityEngine;
using UnityEngine.SceneManagement;
using System.Linq;

namespace FadedDreams.Core
{
    public static class SceneLoader
    {
        public static void LoadScene(string sceneName, string checkpointId = "")
        {
            // ��¼���浵�����ڡ�������Ϸ��������ָ�
            SaveSystem.Instance.SaveLastScene(sceneName);
            SaveSystem.Instance.SaveCheckpoint(checkpointId);

            // �þֲ��������ģ������� Unity ���¼�ǩ�������� System.Action��
            void OnSceneLoaded(Scene scene, LoadSceneMode mode)
            {
                // ִֻ��һ�Σ��������˶�����������ظ�����
                SceneManager.sceneLoaded -= OnSceneLoaded;

                // �ҵ�Ŀ�� Checkpoint��
                // 1) ����ʹ�ô���� checkpointId
                // 2) ��Ϊ�գ����Ҽ�����㣨������ Checkpoint �� public bool activateOnStart �ֶΣ�
                var cps = Object.FindObjectsByType<Checkpoint>(FindObjectsSortMode.None);

                Checkpoint target = null;
                if (!string.IsNullOrEmpty(checkpointId))
                {
                    target = cps.FirstOrDefault(c => c.Id == checkpointId);
                }
                else
                {
                    // �����ҡ���ʼ���㡱
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
                // �� target ��ȻΪ�գ��ͱ���Ĭ�ϳ����㣨����������ؿ�������
            }

            SceneManager.sceneLoaded += OnSceneLoaded; // ��ȷ���¼�ǩ��
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
