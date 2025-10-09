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
        public GameObject bracketPrefab;      // 括号预制体（用于自动创建）
        public bool leftShowsRed = true;
        public bool autoInstantiateIfMissing = true; // 如果找不到就自动实例化

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

            // 自动查找括号对象（如果引用为空）
            if (leftBracket == null || rightBracket == null)
            {
                AutoFindBrackets();
            }

            if (debugLogs)
                Debug.Log($"[EBRKT] Awake: followTarget={followTarget?.name}, pcm={(bool)_pcm}, left={(bool)leftBracket}, right={(bool)rightBracket}");

            // 配置左右颜色
            if (leftBracket) leftBracket.ConfigureSide(isRedSide: leftShowsRed);
            if (rightBracket) rightBracket.ConfigureSide(isRedSide: !leftShowsRed);

            // 强制最上层排序
            ApplySorting(leftBracket);
            ApplySorting(rightBracket);
        }
        
        /// <summary>
        /// 自动查找场景中的Bracket对象
        /// </summary>
        private void AutoFindBrackets()
        {
            BracketVisual[] allBrackets = FindObjectsOfType<BracketVisual>(true);
            
            if (debugLogs)
                Debug.Log($"[EBRKT] AutoFindBrackets: Found {allBrackets.Length} BracketVisual objects in scene");
            
            // 尝试根据名字匹配
            foreach (var bracket in allBrackets)
            {
                string name = bracket.gameObject.name.ToLower();
                
                if (leftBracket == null && name.Contains("left"))
                {
                    leftBracket = bracket;
                    if (debugLogs)
                        Debug.Log($"[EBRKT] Auto-assigned leftBracket: {bracket.gameObject.name}");
                }
                else if (rightBracket == null && name.Contains("right"))
                {
                    rightBracket = bracket;
                    if (debugLogs)
                        Debug.Log($"[EBRKT] Auto-assigned rightBracket: {bracket.gameObject.name}");
                }
            }
            
            // 如果还是找不到，尝试按索引分配
            if (allBrackets.Length >= 2)
            {
                if (leftBracket == null) leftBracket = allBrackets[0];
                if (rightBracket == null) rightBracket = allBrackets[1];
                
                if (debugLogs)
                    Debug.Log($"[EBRKT] Fallback: assigned by index");
            }
            
            // 如果还是找不到，且允许自动实例化，则创建新的
            if (autoInstantiateIfMissing && bracketPrefab != null)
            {
                if (leftBracket == null)
                {
                    GameObject leftObj = Instantiate(bracketPrefab, transform);
                    leftObj.name = "Bracket_Left_Auto";
                    leftBracket = leftObj.GetComponent<BracketVisual>();
                    
                    if (debugLogs)
                        Debug.Log($"[EBRKT] Auto-instantiated leftBracket from prefab");
                }
                
                if (rightBracket == null)
                {
                    GameObject rightObj = Instantiate(bracketPrefab, transform);
                    rightObj.name = "Bracket_Right_Auto";
                    rightBracket = rightObj.GetComponent<BracketVisual>();
                    
                    if (debugLogs)
                        Debug.Log($"[EBRKT] Auto-instantiated rightBracket from prefab");
                }
            }
            
            if (leftBracket == null || rightBracket == null)
            {
                Debug.LogWarning($"[EBRKT] Failed to find or create brackets! Left={(bool)leftBracket}, Right={(bool)rightBracket}. Please assign manually or set bracketPrefab.");
            }
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
