using UnityEngine;
using UnityEngine.UI;
using FadedDreams.Core;
using System.Linq;

namespace FadedDreams.UI
{
    /// <summary>
    /// Simple list of discovered checkpoints; click to load at that point.
    /// Attach to a ScrollView content and set checkpointButtonPrefab.
    /// </summary>
    public class ContinueMenu : MonoBehaviour
    {
        public Button checkpointButtonPrefab;
        public Transform listParent;
        public string sceneName = "Chapter1";

        private void OnEnable()
        {
            foreach (Transform c in listParent) Destroy(c.gameObject);
            var cps = SaveSystem.Instance.GetCheckpoints().ToList();
            cps.Sort();
            foreach (var id in cps)
            {
                var btn = Instantiate(checkpointButtonPrefab, listParent);
                btn.GetComponentInChildren<TMPro.TextMeshProUGUI>().text = id;
                btn.onClick.AddListener(() => SceneLoader.LoadScene(sceneName, id));
            }
        }
    }
}
