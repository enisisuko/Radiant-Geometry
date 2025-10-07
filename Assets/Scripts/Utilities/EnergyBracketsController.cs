using UnityEngine;
using FadedDreams.Player;

namespace FadedDreams.UI
{
    [DisallowMultipleComponent]
    public class EnergyBracketsController : MonoBehaviour
    {
        [Header("Prefabs / Refs")]
        public BracketVisual leftBracket;     // 左侧（默认显示红）
        public BracketVisual rightBracket;    // 右侧（默认显示绿）
        public bool leftShowsRed = true;

        [Header("Follow")]
        public Transform followTarget;        // 通常指向 Player 根
        public Vector2 leftOffset = new Vector2(-1.1f, 0.2f);
        public Vector2 rightOffset = new Vector2(1.1f, 0.2f);
        public float followSmooth = 20f;

        [Header("World Topmost")]
        public string sortingLayerName = "UI";
        public int sortingOrder = 5000;

        [Header("Debug")]
        public bool debugLogs = false;
        public float debugInterval = 0.5f;

        private PlayerColorModeController _pcm;
        private float _lastRed, _lastGreen;
        private float _nextDebugTime;

        private void Awake()
        {
            if (!followTarget) followTarget = transform;

            _pcm = GetComponentInParent<PlayerColorModeController>();
            if (_pcm) _pcm.OnEnergyChanged.AddListener(OnEnergyChanged);

            if (debugLogs)
                Debug.Log($"[EBRKT] Awake: followTarget={followTarget?.name}, pcm={(bool)_pcm}, left={(bool)leftBracket}, right={(bool)rightBracket}");

            // 配置左右颜色
            if (leftBracket) leftBracket.ConfigureSide(isRedSide: leftShowsRed);
            if (rightBracket) rightBracket.ConfigureSide(isRedSide: !leftShowsRed);

            // 强制最上层排序
            ApplySorting(leftBracket);
            ApplySorting(rightBracket);
        }

        private void Start()
        {
            if (_pcm)
            {
                ApplyFill(_pcm);
                _lastRed = _pcm.Red01;
                _lastGreen = _pcm.Green01;

                if (debugLogs)
                    Debug.Log($"[EBRKT] Start init fill: red01={_pcm.Red01:F3}, green01={_pcm.Green01:F3}");
            }
            else if (debugLogs)
            {
                Debug.LogWarning("[EBRKT] Start: _pcm is NULL. 请确认 PlayerColorModeController 是否在父物体上。");
            }
        }

        private void Update()
        {
            if (!followTarget) return;

            float dt = Time.deltaTime;

            if (leftBracket)
            {
                Vector3 target = followTarget.position + (Vector3)leftOffset;
                leftBracket.transform.position = Vector3.Lerp(leftBracket.transform.position, target, 1f - Mathf.Exp(-followSmooth * dt));
                leftBracket.TickFill(dt);
            }
            if (rightBracket)
            {
                Vector3 target = followTarget.position + (Vector3)rightOffset;
                rightBracket.transform.position = Vector3.Lerp(rightBracket.transform.position, target, 1f - Mathf.Exp(-followSmooth * dt));
                rightBracket.TickFill(dt);
            }

            if (debugLogs && Time.unscaledTime >= _nextDebugTime)
            {
                _nextDebugTime = Time.unscaledTime + debugInterval;
                Debug.Log($"[EBRKT] Update: red01={_pcm?.Red01:F3}, green01={_pcm?.Green01:F3}, " +
                          $"leftPos={leftBracket?.transform.position}, rightPos={rightBracket?.transform.position}");
            }
        }

        private void ApplySorting(BracketVisual b)
        {
            if (!b) return;
            if (b.shellRenderer) { b.shellRenderer.sortingLayerName = sortingLayerName; b.shellRenderer.sortingOrder = sortingOrder; }
            if (b.liquidRenderer) { b.liquidRenderer.sortingLayerName = sortingLayerName; b.liquidRenderer.sortingOrder = sortingOrder + 1; }
        }

        private void OnEnergyChanged(float r, float rMax, float g, float gMax)
        {
            if (!_pcm) return;

            ApplyFill(_pcm);

            float red01 = _pcm.Red01;
            float green01 = _pcm.Green01;

            if (debugLogs)
                Debug.Log($"[EBRKT] OnEnergyChanged: r={r:F1}/{rMax:F1}({red01:F3}), g={g:F1}/{gMax:F1}({green01:F3})");

            // 红能变化
            if (red01 > _lastRed + 0.001f)
            {
                // 增加：轻微光亮 +（可选）增益FX
                var b = WhichBracket(true);
                if (b) { b.FlashGain(); b.PlayGainFX(); }
            }
            else if (red01 < _lastRed - 0.001f)
            {
                // 使用：脉冲 + 高亮 + 使用FX（新增）
                var b = WhichBracket(true);
                if (b) { b.PulseUse(); b.PlayUseHighlightAndFX(); }
            }

            // 绿能变化
            if (green01 > _lastGreen + 0.001f)
            {
                var b = WhichBracket(false);
                if (b) { b.FlashGain(); b.PlayGainFX(); }
            }
            else if (green01 < _lastGreen - 0.001f)
            {
                var b = WhichBracket(false);
                if (b) { b.PulseUse(); b.PlayUseHighlightAndFX(); }
            }

            _lastRed = red01; _lastGreen = green01;
        }

        private void ApplyFill(PlayerColorModeController pcm)
        {
            var redBracket = WhichBracket(true);
            var greenBracket = WhichBracket(false);

            if (redBracket) redBracket.SetTargetFill01(pcm.Red01);
            if (greenBracket) greenBracket.SetTargetFill01(pcm.Green01);
        }

        private BracketVisual WhichBracket(bool red)
        {
            return leftShowsRed ? (red ? leftBracket : rightBracket)
                                : (red ? rightBracket : leftBracket);
        }
    }
}
