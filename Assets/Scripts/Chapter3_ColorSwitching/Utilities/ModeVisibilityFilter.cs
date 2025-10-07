using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace FadedDreams.World
{
    /// <summary>
    /// ͬɫ���е������㣻��ɫ����͸�� + �е�����㣨���������ײ�����������/���Hitbox��ײ����
    /// Inspector ���á���������д����ֱ�ۣ�����ʱ�Զ�����Ϊ layer ������
    /// </summary>
    public class ModeVisibilityFilter : MonoBehaviour
    {
        [Header("Identity")]
        public FadedDreams.Player.ColorMode objectColor = FadedDreams.Player.ColorMode.Red;
        [Range(0f, 1f)] public float hiddenAlpha = 0.25f;

        [Header("Layers (by name)")]
        [Tooltip("ͬɫ�ɽ���ʱ���ڲ��������� Enemy / Interactable��")]
        public string interactLayerName = "Enemy";
        [Tooltip("��ɫʱ���ڲ��������� EnemyGhost / InteractableGhost��")]
        public string ghostLayerName = "EnemyGhost";

        [Header("Light Link")]
        public Light2D light2D;
        public Color redLightColor = new Color(1f, 0.25f, 0.25f, 1f);
        public Color greenLightColor = new Color(0.25f, 1f, 0.25f, 1f);

        private SpriteRenderer[] _sprites;
        private FadedDreams.Player.PlayerColorModeController _player;

        // �����ã�������� layer ����
        private int _interactLayer = -1;
        private int _ghostLayer = -1;

        private void Reset()
        {
            // Ĭ�ϰѵ�ǰ�㵱�������㣨�������볡�����ã�
            _interactLayer = gameObject.layer;
            var currentName = LayerMask.LayerToName(_interactLayer);
            if (!string.IsNullOrEmpty(currentName))
                interactLayerName = currentName;

            // ��һ��������Ĭ�����������������Ŀ��Ҫ�޸ģ�
            if (string.IsNullOrEmpty(ghostLayerName))
                ghostLayerName = "EnemyGhost";
        }

        private void OnValidate()
        {
            // �ڱ༭���޸��ֶ�ʱ����ʱ��������
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

            // ���׽���һ�Σ���ֹĳЩ������ OnValidate δ������
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

            // ���� �ݹ��� Layer���������������壩
            int targetLayer = interact ? _interactLayer : _ghostLayer;
            SetLayerRecursively(transform, targetLayer);

            // ���� ������͸���Ӿ���������ͣ��ײ�壨���⴩�صȸ����ã�
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
