using UnityEngine;
using UnityEngine.SceneManagement;

namespace FadedDreams.Core
{
    /// <summary>
    /// Handles chapter-specific rules and abilities.
    /// </summary>
    public class ChapterManager : MonoBehaviour
    {
        public static int ChapterFromScene()
        {
            var name = SceneManager.GetActiveScene().name.ToLower();
            if (name.Contains("chapter1")) return 1;
            if (name.Contains("chapter2")) return 2;
            if (name.Contains("chapter3")) return 3;
            if (name.Contains("chapter4")) return 4;
            return 1;
        }
    }
}
