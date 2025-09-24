using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace FadedDreams.World
{
    /// <summary>
    /// 同色：切到交互层；异色：半透明 + 切到幽灵层（仍与地形碰撞，但不与玩家/玩家Hitbox碰撞）。
    /// Inspector 里用【层名】填写，更直观；运行时自动解析为 layer 索引。
    /// </summary>
    public class ModeVisibilityFilter : MonoBehaviour
    {
        [Header("Identity")]
        public FadedDreams.Player.ColorMode objectColor = FadedDreams.Player.ColorMode.Red;
        [Range(0f, 1f)] public float hiddenAlpha = 0.25f;

        [Header("Layers (by name)")]
        [Tooltip("同色可交互时所在层名（例如 Enemy / Interactable）")]
        public string interactLayerName = "Enemy";
        [Tooltip("异色时所在层名（例如 EnemyGhost / InteractableGhost）")]
        public string ghostLayerName = "EnemyGhost";

        [Header("Light Link")]
        public Light2D light2D;
        public Color redLightColor = new Color(1f, 0.25f, 0.25f, 1f);
        public Color greenLightColor = new Color(0.25f, 1f, 0.25f, 1f);

        private SpriteRenderer[] _sprites;
        private FadedDreams.Player.PlayerColorModeController _player;

        // 运行用：解析后的 layer 索引
        private int _interactLayer = -1;
        private int _ghostLayer = -1;

        private void Reset()
        {
            // 默认把当前层当作交互层（便于拖入场景就用）
            _interactLayer = gameObject.layer;
            var currentName = LayerMask.LayerToName(_interactLayer);
            if (!string.IsNullOrEmpty(currentName))
                interactLayerName = currentName;

            // 给一个常见的默认幽灵层名（按你项目需要修改）
            if (string.IsNullOrEmpty(ghostLayerName))
                ghostLayerName = "EnemyGhost";
        }

        private void OnValidate()
        {
            // 在编辑器修改字段时，及时解析层名
            _interactLayer = NameToLayerSafe(interactLayerName, gameObject.layer);
            _ghostLayer = NameToLayerSafe(ghostLayerName, LayerMask.NameToLayer("Default"));
        }

        private static int NameToLayerSafe(string layerName, int fallback)
        {
            if (string.IsNullOrEmpty(layerName)) return fallback;
            int idx = LayerMask.NameToLayer(layerName);
            return (idx < 0) ? fallback : idx;
        }

        private void Awake()
        {
            _sprites = GetComponentsInChildren<SpriteRenderer>(true);
            if (!light2D) light2D = GetComponentInChildren<Light2D>(true);

            // 兜底解析一次（防止某些场景下 OnValidate 未触发）
            if (_interactLayer < 0) _interactLayer = NameToLayerSafe(interactLayerName, gameObject.layer);
            if (_ghostLayer < 0) _ghostLayer = NameToLayerSafe(ghostLayerName, LayerMask.NameToLayer("Default"));

            ApplyOwnLightColor();
        }

        private void OnEnable()
        {
            _player = FindObjectOfType<FadedDreams.Player.PlayerColorModeController>();
            if (_player)
            {
                _player.OnModeChanged.AddListener(ApplyVisibility);
                ApplyVisibility(_player.Mode);
            }
        }

        private void OnDisable()
        {
            if (_player) _player.OnModeChanged.RemoveListener(ApplyVisibility);
        }

        private void ApplyVisibility(FadedDreams.Player.ColorMode playerMode)
        {
            bool interact = (playerMode == objectColor);

            // —— 递归切 Layer（包含所有子物体）
            int targetLayer = interact ? _interactLayer : _ghostLayer;
            SetLayerRecursively(transform, targetLayer);

            // —— 仅做半透明视觉，不再启停碰撞体（避免穿地等副作用）
            foreach (var sr in _sprites)
            {
                var c = sr.color;
                c.a = interact ? 1f : hiddenAlpha;
                sr.color = c;
            }
        }

        private void SetLayerRecursively(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            for (int i = 0; i < t.childCount; i++)
                SetLayerRecursively(t.GetChild(i), layer);
        }

        private void ApplyOwnLightColor()
        {
            if (!light2D) return;
            light2D.color = (objectColor == FadedDreams.Player.ColorMode.Red) ? redLightColor : greenLightColor;
        }
    }
}
