// Assets/Scripts/Bosses/Chapter2/GreenCurtainSimple.cs
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FadedDreams.Bosses
{
    public class GreenCurtainSimple : MonoBehaviour
    {
        public static void Go(string nextScene, float fadeIn, float hold, float fadeOut)
        {
            var go = new GameObject("~GreenCurtainSimple");
            DontDestroyOnLoad(go);
            var g = go.AddComponent<GreenCurtainSimple>();
            g.StartCoroutine(g.CoGo(nextScene, fadeIn, hold, fadeOut));
        }

        private IEnumerator CoGo(string nextScene, float fadeIn, float hold, float fadeOut)
        {
            // 生成全屏Canvas+Image（绿色）
            var canvasGO = new GameObject("GreenCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            DontDestroyOnLoad(canvasGO);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;

            var imgGO = new GameObject("GreenOverlay", typeof(Image));
            imgGO.transform.SetParent(canvasGO.transform, false);
            var img = imgGO.GetComponent<Image>();
            img.color = new Color(0.1f, 0.9f, 0.4f, 0f); // 绿色，初始透明

            var rt = img.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            // 淡入
            float t = 0f;
            while (t < fadeIn)
            {
                t += Time.unscaledDeltaTime;
                float a = Mathf.Clamp01(t / Mathf.Max(0.01f, fadeIn));
                img.color = new Color(img.color.r, img.color.g, img.color.b, a);
                yield return null;
            }
            img.color = new Color(img.color.r, img.color.g, img.color.b, 1f);

            // 切换场景
            if (!string.IsNullOrEmpty(nextScene))
                SceneManager.LoadScene(nextScene);

            if (hold > 0f) yield return new WaitForSecondsRealtime(hold);

            // 淡出
            t = 0f;
            while (t < fadeOut)
            {
                t += Time.unscaledDeltaTime;
                float k = 1f - Mathf.Clamp01(t / Mathf.Max(0.01f, fadeOut));
                img.color = new Color(img.color.r, img.color.g, img.color.b, k);
                yield return null;
            }

            Destroy(canvasGO);
            Destroy(gameObject);
        }
    }
}
