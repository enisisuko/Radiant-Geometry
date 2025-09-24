using UnityEngine;
using UnityEngine.SceneManagement;
using FadedDreams.Player;

namespace FadedDreams.Core
{
    [RequireComponent(typeof(Collider2D))]
    public class Checkpoint : MonoBehaviour
    {
        [Tooltip("×÷Îª¼ì²éµã Id Ê¹ÓÃ£»Ä¬ÈÏÓÃ¶ÔÏóÃû")]
        [SerializeField] private string idOverride = "";

        [Tooltip("ÊÇ·ñ×÷Îª±¾¹ØÆðÊ¼¼ì²éµã£¨ÔÚÎ´Ìá¹© checkpointId Ê±×÷ÎªÄ¬ÈÏÂäµã£©")]
        public bool activateOnStart = false;

        public string Id => string.IsNullOrEmpty(idOverride) ? name : idOverride;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;

            // ½øÈë¼ì²éµã¾Í¸üÐÂ ¡°×î½ü³¡¾° + ×î½ü¼ì²éµã¡±
            var sceneName = SceneManager.GetActiveScene().name;
            SaveSystem.Instance.SaveLastScene(sceneName);
            SaveSystem.Instance.SaveCheckpoint(Id);

            // Ò²°ÑËü¼ÓÈë¡°±¾³¡¾°ÒÑ·¢ÏÖµÄ¼ì²éµã¼¯ºÏ¡±£¨¹© Continue ²Ëµ¥ÁÐ³ö£©
            SaveSystem.Instance.AddDiscoveredCheckpoint(sceneName, Id);

            // ÕâÀï¿ÉÒÔ¼Ó¸ö UI ÌáÊ¾£º¡°ÒÑµ½´ï¼ì²éµã£ºId¡±
        }

        // ±» SceneLoader µ÷ÓÃ£¬°ÑÍæ¼Ò·Åµ½´Ë´¦
        public void SpawnPlayerHere()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;
            player.transform.position = transform.position;

            // 通知自动阅读系统：完成一次“从检查点重生”
            //var auto = player.GetComponent<AutoReadOnLowEnergy>();
            //            if (auto) auto.OnRespawnedAtCheckpoint();

            //var reader = player.GetComponent<ReadingStateController>();
            //    if (reader) reader.ResetManualCooldownOnRespawn();

        }
    }
}
