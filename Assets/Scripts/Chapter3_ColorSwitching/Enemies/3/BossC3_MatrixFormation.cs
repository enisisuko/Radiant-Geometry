// BossC3_MatrixFormation.cs
// 大阵系统 - 负责三圈七层大阵管理、几何体生成和节拍系统
// Unity 2021+ / Unity 6.2 兼容（URP/HDRP均可）

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FD.Bosses.C3;
using FadedDreams.Bosses;

namespace FD.Bosses.C3
{
    /// <summary>
    /// BossC3大阵系统 - 负责三圈七层大阵管理、几何体生成和节拍系统
    /// </summary>
    [DisallowMultipleComponent]
    public class BossC3_MatrixFormation : MonoBehaviour
    {
        [Header("== Matrix Formation ==")]
        public int circleCount = 3;           // 圈数
        public int layerCount = 7;            // 层数
        public float baseRadius = 5f;         // 基础半径
        public float layerSpacing = 1.5f;     // 层间距
        public float circleSpacing = 2f;      // 圈间距

        [Header("== Beat System ==")]
        public float beatInterval = 0.5f;     // 节拍间隔（秒）
        public int beatsPerCycle = 12;        // 每周期节拍数
        public bool autoStartBeat = true;

        [Header("== Geometry Generation ==")]
        public GameObject geometryPrefab;     // 几何体预制体
        public Material geometryMaterial;     // 几何体材质
        public float geometryScale = 1f;      // 几何体缩放
        public float geometryLifetime = 5f;   // 几何体生命周期

        [Header("== Animation ==")]
        public AnimationCurve formationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        public float formationDuration = 2f;  // 形成动画时长
        public float rotationSpeed = 10f;     // 旋转速度
        public bool autoRotate = true;

        [Header("== Visual Effects ==")]
        public GameObject formationVfx;       // 形成特效
        public GameObject beatVfx;            // 节拍特效
        public Color formationColor = Color.cyan;
        public float emissionIntensity = 2f;

        [Header("== Debug ==")]
        public bool verboseLogs = true;
        public bool drawGizmos = true;

        // 大阵管理器
        private MatrixFormationManager _formationManager;
        private List<GameObject> _activeGeometries = new List<GameObject>();
        private List<Transform> _formationPoints = new List<Transform>();

        // 节拍系统
        private Coroutine _beatCoroutine;
        private int _currentBeat = 0;
        private bool _isBeatActive = false;

        // 动画状态
        private bool _isForming = false;
        private float _formationProgress = 0f;
        private Coroutine _formationCoroutine;

        // 旋转状态
        private float _currentRotation = 0f;

        // 事件
        public event Action OnFormationStarted;
        public event Action OnFormationCompleted;
        public event Action<int> OnBeatTriggered;
        public event Action<GameObject> OnGeometrySpawned;

        #region Unity Lifecycle

        private void Awake()
        {
            _formationManager = GetComponent<MatrixFormationManager>();
            if (_formationManager == null)
            {
                _formationManager = gameObject.AddComponent<MatrixFormationManager>();
            }
        }

        private void Start()
        {
            InitializeFormation();
            
            if (autoStartBeat)
            {
                StartBeatSystem();
            }
        }

        private void Update()
        {
            UpdateFormationAnimation();
            UpdateRotation();
        }

        #endregion

        #region Formation Management

        /// <summary>
        /// 初始化大阵
        /// </summary>
        private void InitializeFormation()
        {
            if (verboseLogs)
                Debug.Log($"[BossC3_MatrixFormation] Initializing formation: {circleCount} circles, {layerCount} layers");

            // 计算大阵点位置
            CalculateFormationPoints();
            
            // 设置大阵管理器
            if (_formationManager != null)
            {
                _formationManager.SetupFormation(circleCount, layerCount, baseRadius, layerSpacing, circleSpacing);
            }

            OnFormationStarted?.Invoke();
        }

        /// <summary>
        /// 计算大阵点位置
        /// </summary>
        private void CalculateFormationPoints()
        {
            _formationPoints.Clear();

            for (int circle = 0; circle < circleCount; circle++)
            {
                float circleRadius = baseRadius + circle * circleSpacing;
                int pointsInCircle = layerCount + circle * 2; // 每圈增加点数

                for (int layer = 0; layer < layerCount; layer++)
                {
                    float layerHeight = layer * layerSpacing;
                    
                    for (int point = 0; point < pointsInCircle; point++)
                    {
                        float angle = (360f / pointsInCircle) * point * Mathf.Deg2Rad;
                        Vector3 position = transform.position + new Vector3(
                            Mathf.Cos(angle) * circleRadius,
                            layerHeight,
                            Mathf.Sin(angle) * circleRadius
                        );

                        // 创建大阵点
                        GameObject pointObj = new GameObject($"FormationPoint_C{circle}_L{layer}_P{point}");
                        pointObj.transform.position = position;
                        pointObj.transform.SetParent(transform);
                        _formationPoints.Add(pointObj.transform);
                    }
                }
            }

            if (verboseLogs)
                Debug.Log($"[BossC3_MatrixFormation] Created {_formationPoints.Count} formation points");
        }

        /// <summary>
        /// 开始形成大阵
        /// </summary>
        public void StartFormation()
        {
            if (_isForming) return;

            if (verboseLogs)
                Debug.Log("[BossC3_MatrixFormation] Starting formation");

            _isForming = true;
            _formationProgress = 0f;

            if (_formationCoroutine != null)
            {
                StopCoroutine(_formationCoroutine);
            }

            _formationCoroutine = StartCoroutine(FormationCoroutine());
        }

        /// <summary>
        /// 大阵形成协程
        /// </summary>
        private IEnumerator FormationCoroutine()
        {
            float elapsed = 0f;
            float duration = formationDuration;

            // 播放形成特效
            if (formationVfx != null)
            {
                GameObject vfx = Instantiate(formationVfx, transform.position, Quaternion.identity);
                Destroy(vfx, duration);
            }

            while (elapsed < duration)
            {
                _formationProgress = elapsed / duration;
                float curveValue = formationCurve.Evaluate(_formationProgress);

                // 更新大阵点位置
                UpdateFormationPoints(curveValue);

                elapsed += Time.deltaTime;
                yield return null;
            }

            // 完成形成
            _formationProgress = 1f;
            _isForming = false;
            OnFormationCompleted?.Invoke();

            if (verboseLogs)
                Debug.Log("[BossC3_MatrixFormation] Formation completed");
        }

        /// <summary>
        /// 更新大阵点位置
        /// </summary>
        private void UpdateFormationPoints(float progress)
        {
            for (int i = 0; i < _formationPoints.Count; i++)
            {
                if (_formationPoints[i] == null) continue;

                // 计算目标位置
                Vector3 targetPosition = CalculateFormationPointPosition(i);
                Vector3 startPosition = transform.position;
                
                // 插值位置
                _formationPoints[i].position = Vector3.Lerp(startPosition, targetPosition, progress);
            }
        }

        /// <summary>
        /// 计算大阵点位置
        /// </summary>
        private Vector3 CalculateFormationPointPosition(int index)
        {
            int currentIndex = 0;
            
            for (int circle = 0; circle < circleCount; circle++)
            {
                int pointsInCircle = layerCount + circle * 2;
                
                for (int layer = 0; layer < layerCount; layer++)
                {
                    for (int point = 0; point < pointsInCircle; point++)
                    {
                        if (currentIndex == index)
                        {
                            float circleRadius = baseRadius + circle * circleSpacing;
                            float layerHeight = layer * layerSpacing;
                            float angle = (360f / pointsInCircle) * point * Mathf.Deg2Rad;
                            
                            return transform.position + new Vector3(
                                Mathf.Cos(angle) * circleRadius,
                                layerHeight,
                                Mathf.Sin(angle) * circleRadius
                            );
                        }
                        currentIndex++;
                    }
                }
            }

            return transform.position;
        }

        #endregion

        #region Beat System

        /// <summary>
        /// 开始节拍系统
        /// </summary>
        public void StartBeatSystem()
        {
            if (_isBeatActive) return;

            if (verboseLogs)
                Debug.Log("[BossC3_MatrixFormation] Starting beat system");

            _isBeatActive = true;
            _currentBeat = 0;

            if (_beatCoroutine != null)
            {
                StopCoroutine(_beatCoroutine);
            }

            _beatCoroutine = StartCoroutine(BeatCoroutine());
        }

        /// <summary>
        /// 停止节拍系统
        /// </summary>
        public void StopBeatSystem()
        {
            if (!_isBeatActive) return;

            if (verboseLogs)
                Debug.Log("[BossC3_MatrixFormation] Stopping beat system");

            _isBeatActive = false;

            if (_beatCoroutine != null)
            {
                StopCoroutine(_beatCoroutine);
                _beatCoroutine = null;
            }
        }

        /// <summary>
        /// 节拍协程
        /// </summary>
        private IEnumerator BeatCoroutine()
        {
            while (_isBeatActive)
            {
                // 触发节拍
                OnBeatTriggered?.Invoke(_currentBeat);
                
                // 播放节拍特效
                if (beatVfx != null)
                {
                    GameObject vfx = Instantiate(beatVfx, transform.position, Quaternion.identity);
                    Destroy(vfx, beatInterval);
                }

                // 更新节拍计数
                _currentBeat = (_currentBeat + 1) % beatsPerCycle;

                // 等待下一个节拍
                yield return new WaitForSeconds(beatInterval);
            }
        }

        /// <summary>
        /// 设置节拍间隔
        /// </summary>
        public void SetBeatInterval(float interval)
        {
            beatInterval = interval;
        }

        /// <summary>
        /// 获取当前节拍
        /// </summary>
        public int GetCurrentBeat()
        {
            return _currentBeat;
        }

        /// <summary>
        /// 检查节拍系统是否活跃
        /// </summary>
        public bool IsBeatActive()
        {
            return _isBeatActive;
        }

        #endregion

        #region Geometry Generation

        /// <summary>
        /// 在指定位置生成几何体
        /// </summary>
        public GameObject SpawnGeometry(Vector3 position, Quaternion rotation, float scale = 1f)
        {
            if (geometryPrefab == null) return null;

            GameObject geometry = Instantiate(geometryPrefab, position, rotation);
            geometry.transform.localScale = Vector3.one * geometryScale * scale;
            
            // 设置材质
            if (geometryMaterial != null)
            {
                Renderer renderer = geometry.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material = geometryMaterial;
                }
            }

            // 设置发光效果
            Renderer geoRenderer = geometry.GetComponent<Renderer>();
            if (geoRenderer != null)
            {
                geoRenderer.material.SetColor("_EmissionColor", formationColor * emissionIntensity);
                geoRenderer.material.EnableKeyword("_EMISSION");
            }

            _activeGeometries.Add(geometry);
            OnGeometrySpawned?.Invoke(geometry);

            // 设置生命周期
            StartCoroutine(DestroyGeometryAfterTime(geometry, geometryLifetime));

            if (verboseLogs)
                Debug.Log($"[BossC3_MatrixFormation] Spawned geometry at {position}");

            return geometry;
        }

        /// <summary>
        /// 在大阵点上生成几何体
        /// </summary>
        public void SpawnGeometryAtFormationPoint(int pointIndex, float scale = 1f)
        {
            if (pointIndex < 0 || pointIndex >= _formationPoints.Count) return;

            Transform point = _formationPoints[pointIndex];
            if (point == null) return;

            SpawnGeometry(point.position, point.rotation, scale);
        }

        /// <summary>
        /// 在随机大阵点生成几何体
        /// </summary>
        public void SpawnGeometryAtRandomPoint(float scale = 1f)
        {
            if (_formationPoints.Count == 0) return;

            int randomIndex = UnityEngine.Random.Range(0, _formationPoints.Count);
            SpawnGeometryAtFormationPoint(randomIndex, scale);
        }

        /// <summary>
        /// 销毁几何体
        /// </summary>
        private IEnumerator DestroyGeometryAfterTime(GameObject geometry, float lifetime)
        {
            yield return new WaitForSeconds(lifetime);
            
            if (geometry != null)
            {
                _activeGeometries.Remove(geometry);
                Destroy(geometry);
            }
        }

        /// <summary>
        /// 清理所有几何体
        /// </summary>
        public void ClearAllGeometries()
        {
            foreach (GameObject geometry in _activeGeometries)
            {
                if (geometry != null)
                {
                    Destroy(geometry);
                }
            }
            _activeGeometries.Clear();
        }

        #endregion

        #region Animation Updates

        /// <summary>
        /// 更新大阵动画
        /// </summary>
        private void UpdateFormationAnimation()
        {
            if (_isForming)
            {
                // 大阵形成动画已在协程中处理
                return;
            }

            // 更新大阵点位置（考虑旋转）
            for (int i = 0; i < _formationPoints.Count; i++)
            {
                if (_formationPoints[i] == null) continue;

                Vector3 targetPosition = CalculateFormationPointPosition(i);
                _formationPoints[i].position = targetPosition;
            }
        }

        /// <summary>
        /// 更新旋转
        /// </summary>
        private void UpdateRotation()
        {
            if (!autoRotate) return;

            _currentRotation += rotationSpeed * Time.deltaTime;
            if (_currentRotation >= 360f)
            {
                _currentRotation -= 360f;
            }

            // 应用旋转到所有大阵点
            for (int i = 0; i < _formationPoints.Count; i++)
            {
                if (_formationPoints[i] == null) continue;

                Vector3 localPosition = _formationPoints[i].position - transform.position;
                Vector3 rotatedPosition = Quaternion.AngleAxis(rotationSpeed * Time.deltaTime, Vector3.up) * localPosition;
                _formationPoints[i].position = transform.position + rotatedPosition;
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// 获取大阵点数量
        /// </summary>
        public int GetFormationPointCount()
        {
            return _formationPoints.Count;
        }

        /// <summary>
        /// 获取活跃几何体数量
        /// </summary>
        public int GetActiveGeometryCount()
        {
            return _activeGeometries.Count;
        }

        /// <summary>
        /// 检查大阵是否正在形成
        /// </summary>
        public bool IsForming()
        {
            return _isForming;
        }

        /// <summary>
        /// 获取形成进度
        /// </summary>
        public float GetFormationProgress()
        {
            return _formationProgress;
        }

        /// <summary>
        /// 重置大阵
        /// </summary>
        public void ResetFormation()
        {
            StopBeatSystem();
            ClearAllGeometries();
            
            // 清理大阵点
            foreach (Transform point in _formationPoints)
            {
                if (point != null)
                {
                    Destroy(point.gameObject);
                }
            }
            _formationPoints.Clear();

            // 重置状态
            _isForming = false;
            _formationProgress = 0f;
            _currentBeat = 0;
            _currentRotation = 0f;

            // 重新初始化
            InitializeFormation();
        }

        /// <summary>
        /// 获取调试信息
        /// </summary>
        public string GetDebugInfo()
        {
            return $"Formation: {_formationPoints.Count} points, {_activeGeometries.Count} geometries, Beat: {_currentBeat}, Forming: {_isForming}";
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmos()
        {
            if (!drawGizmos) return;

            // 绘制大阵结构
            Gizmos.color = formationColor;
            
            for (int circle = 0; circle < circleCount; circle++)
            {
                float circleRadius = baseRadius + circle * circleSpacing;
                Gizmos.DrawWireSphere(transform.position, circleRadius);
            }

            // 绘制大阵点
            Gizmos.color = Color.yellow;
            foreach (Transform point in _formationPoints)
            {
                if (point != null)
                {
                    Gizmos.DrawWireSphere(point.position, 0.1f);
                }
            }
        }

        #endregion
    }
}
