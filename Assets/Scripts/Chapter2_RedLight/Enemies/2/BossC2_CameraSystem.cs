// BossC2_CameraSystem.cs
// 相机系统 - 负责BOSS战相机应用、相机拉远和FOV调整
// Unity 2021+ / Unity 6.2 兼容（URP/HDRP均可）

using System;
using System.Collections;
using UnityEngine;

namespace FadedDreams.Bosses
{
    /// <summary>
    /// BossC2相机系统 - 负责BOSS战相机应用、相机拉远和FOV调整
    /// </summary>
    [DisallowMultipleComponent]
    public class BossC2_CameraSystem : MonoBehaviour
    {
        [Header("== Camera Settings ==")]
        public Camera targetCamera;
        public float bossFov = 60f;
        public float bossOrthoSize = 8f;
        public float transitionDuration = 1f;
        public AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("== Boss Fight Camera ==")]
        public float bossFightFov = 45f;
        public float bossFightOrthoSize = 12f;
        public Vector3 bossFightOffset = new Vector3(0, 2, 0);
        public float bossFightSmoothTime = 0.3f;

        [Header("== Player Priority ==")]
        public bool playerPriority = true;
        public float playerPriorityWeight = 0.7f;
        public float bossPriorityWeight = 0.3f;

        [Header("== Anchor Following ==")]
        public bool enableAnchorFollowing = true;
        public Transform anchorPoint;
        public float anchorSmoothTime = 0.5f;
        public float anchorMaxDistance = 15f;

        [Header("== Debug ==")]
        public bool verboseLogs = true;
        public bool drawGizmos = true;

        // 组件引用
        private BossC2_Core core;

        // 相机状态
        private bool _isBossCameraActive = false;
        private bool _isTransitioning = false;
        private Coroutine _cameraTransitionCR;
        private Coroutine _cameraFollowCR;

        // 原始相机设置
        private float _originalFov;
        private float _originalOrthoSize;
        private Vector3 _originalPosition;

        // 相机跟随状态
        private Vector3 _cameraVelocity;
        private Vector3 _anchorVelocity;

        // 事件
        public event Action OnBossCameraActivated;
        public event Action OnBossCameraDeactivated;
        public event Action OnCameraTransitionStarted;
        public event Action OnCameraTransitionCompleted;

        #region Unity Lifecycle

        private void Awake()
        {
            core = GetComponent<BossC2_Core>();

            // 查找目标相机
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
                if (targetCamera == null)
                {
                    targetCamera = FindObjectOfType<Camera>();
                }
            }
        }

        private void Start()
        {
            // 保存原始相机设置
            if (targetCamera != null)
            {
                _originalFov = targetCamera.fieldOfView;
                _originalOrthoSize = targetCamera.orthographicSize;
                _originalPosition = targetCamera.transform.position;
            }

            // 订阅核心事件
            if (core != null)
            {
                core.OnAggroStarted += ActivateBossCamera;
                core.OnAggroEnded += DeactivateBossCamera;
                core.OnDeath += DeactivateBossCamera;
            }
        }

        private void OnDestroy()
        {
            // 取消订阅事件
            if (core != null)
            {
                core.OnAggroStarted -= ActivateBossCamera;
                core.OnAggroEnded -= DeactivateBossCamera;
                core.OnDeath -= DeactivateBossCamera;
            }

            // 停止协程
            if (_cameraTransitionCR != null)
            {
                StopCoroutine(_cameraTransitionCR);
            }

            if (_cameraFollowCR != null)
            {
                StopCoroutine(_cameraFollowCR);
            }
        }

        #endregion

        #region Boss Camera Management

        /// <summary>
        /// 激活BOSS相机
        /// </summary>
        public void ActivateBossCamera()
        {
            if (_isBossCameraActive || _isTransitioning) return;

            if (verboseLogs)
                Debug.Log("[BossC2_CameraSystem] Activating boss camera");

            _isBossCameraActive = true;
            OnBossCameraActivated?.Invoke();

            // 开始相机过渡
            StartCameraTransition(true);

            // 开始相机跟随
            if (enableAnchorFollowing)
            {
                StartCameraFollowing();
            }
        }

        /// <summary>
        /// 停用BOSS相机
        /// </summary>
        public void DeactivateBossCamera()
        {
            if (!_isBossCameraActive || _isTransitioning) return;

            if (verboseLogs)
                Debug.Log("[BossC2_CameraSystem] Deactivating boss camera");

            _isBossCameraActive = false;
            OnBossCameraDeactivated?.Invoke();

            // 开始相机过渡
            StartCameraTransition(false);

            // 停止相机跟随
            StopCameraFollowing();
        }

        #endregion

        #region Camera Transition

        /// <summary>
        /// 开始相机过渡
        /// </summary>
        private void StartCameraTransition(bool toBossCamera)
        {
            if (_cameraTransitionCR != null)
            {
                StopCoroutine(_cameraTransitionCR);
            }

            _cameraTransitionCR = StartCoroutine(CameraTransitionCoroutine(toBossCamera));
        }

        /// <summary>
        /// 相机过渡协程
        /// </summary>
        private IEnumerator CameraTransitionCoroutine(bool toBossCamera)
        {
            _isTransitioning = true;
            OnCameraTransitionStarted?.Invoke();

            float elapsed = 0f;
            float duration = transitionDuration;

            // 获取起始和结束值
            float startFov = targetCamera.fieldOfView;
            float startOrthoSize = targetCamera.orthographicSize;
            float endFov = toBossCamera ? bossFightFov : _originalFov;
            float endOrthoSize = toBossCamera ? bossFightOrthoSize : _originalOrthoSize;

            while (elapsed < duration)
            {
                float t = elapsed / duration;
                float curveValue = transitionCurve.Evaluate(t);

                // 插值FOV和正交大小
                targetCamera.fieldOfView = Mathf.Lerp(startFov, endFov, curveValue);
                targetCamera.orthographicSize = Mathf.Lerp(startOrthoSize, endOrthoSize, curveValue);

                elapsed += Time.deltaTime;
                yield return null;
            }

            // 确保最终值正确
            targetCamera.fieldOfView = endFov;
            targetCamera.orthographicSize = endOrthoSize;

            _isTransitioning = false;
            OnCameraTransitionCompleted?.Invoke();

            if (verboseLogs)
                Debug.Log($"[BossC2_CameraSystem] Camera transition completed: {(toBossCamera ? "Boss" : "Normal")}");
        }

        #endregion

        #region Camera Following

        /// <summary>
        /// 开始相机跟随
        /// </summary>
        private void StartCameraFollowing()
        {
            if (_cameraFollowCR != null)
            {
                StopCoroutine(_cameraFollowCR);
            }

            _cameraFollowCR = StartCoroutine(CameraFollowCoroutine());
        }

        /// <summary>
        /// 停止相机跟随
        /// </summary>
        private void StopCameraFollowing()
        {
            if (_cameraFollowCR != null)
            {
                StopCoroutine(_cameraFollowCR);
                _cameraFollowCR = null;
            }
        }

        /// <summary>
        /// 相机跟随协程
        /// </summary>
        private IEnumerator CameraFollowCoroutine()
        {
            while (_isBossCameraActive && enableAnchorFollowing)
            {
                UpdateCameraPosition();
                yield return null;
            }
        }

        /// <summary>
        /// 更新相机位置
        /// </summary>
        private void UpdateCameraPosition()
        {
            if (targetCamera == null) return;

            Vector3 targetPosition = CalculateTargetCameraPosition();
            Vector3 currentPosition = targetCamera.transform.position;

            // 平滑移动到目标位置
            targetCamera.transform.position = Vector3.SmoothDamp(
                currentPosition,
                targetPosition,
                ref _cameraVelocity,
                bossFightSmoothTime
            );
        }

        /// <summary>
        /// 计算目标相机位置
        /// </summary>
        private Vector3 CalculateTargetCameraPosition()
        {
            Vector3 targetPosition = Vector3.zero;

            if (playerPriority && core.GetPlayer() != null)
            {
                // 玩家优先模式
                Vector3 playerPos = core.GetPlayer().position;
                Vector3 bossPos = transform.position;

                // 计算加权中心点
                targetPosition = Vector3.Lerp(
                    playerPos,
                    bossPos,
                    bossPriorityWeight
                );
            }
            else
            {
                // BOSS中心模式
                targetPosition = transform.position;
            }

            // 添加偏移
            targetPosition += bossFightOffset;

            // 限制距离
            if (anchorPoint != null)
            {
                float distance = Vector3.Distance(targetPosition, anchorPoint.position);
                if (distance > anchorMaxDistance)
                {
                    Vector3 direction = (targetPosition - anchorPoint.position).normalized;
                    targetPosition = anchorPoint.position + direction * anchorMaxDistance;
                }
            }

            return targetPosition;
        }

        #endregion

        #region Anchor Management

        /// <summary>
        /// 设置锚点
        /// </summary>
        public void SetAnchorPoint(Transform anchor)
        {
            anchorPoint = anchor;

            if (verboseLogs)
                Debug.Log($"[BossC2_CameraSystem] Anchor point set: {(anchor != null ? anchor.name : "null")}");
        }

        /// <summary>
        /// 清除锚点
        /// </summary>
        public void ClearAnchorPoint()
        {
            anchorPoint = null;

            if (verboseLogs)
                Debug.Log("[BossC2_CameraSystem] Anchor point cleared");
        }

        #endregion

        #region Public API

        /// <summary>
        /// 获取BOSS相机是否激活
        /// </summary>
        public bool IsBossCameraActive() => _isBossCameraActive;

        /// <summary>
        /// 获取是否正在过渡
        /// </summary>
        public bool IsTransitioning() => _isTransitioning;

        /// <summary>
        /// 获取目标相机
        /// </summary>
        public Camera GetTargetCamera() => targetCamera;

        /// <summary>
        /// 设置目标相机
        /// </summary>
        public void SetTargetCamera(Camera camera)
        {
            targetCamera = camera;

            if (camera != null)
            {
                _originalFov = camera.fieldOfView;
                _originalOrthoSize = camera.orthographicSize;
                _originalPosition = camera.transform.position;
            }
        }

        /// <summary>
        /// 设置BOSS战FOV
        /// </summary>
        public void SetBossFightFov(float fov)
        {
            bossFightFov = fov;
        }

        /// <summary>
        /// 设置BOSS战正交大小
        /// </summary>
        public void SetBossFightOrthoSize(float size)
        {
            bossFightOrthoSize = size;
        }

        /// <summary>
        /// 设置过渡时间
        /// </summary>
        public void SetTransitionDuration(float duration)
        {
            transitionDuration = duration;
        }

        /// <summary>
        /// 强制恢复原始相机设置
        /// </summary>
        public void RestoreOriginalCamera()
        {
            if (targetCamera != null)
            {
                targetCamera.fieldOfView = _originalFov;
                targetCamera.orthographicSize = _originalOrthoSize;
                targetCamera.transform.position = _originalPosition;
            }

            _isBossCameraActive = false;
            _isTransitioning = false;

            if (verboseLogs)
                Debug.Log("[BossC2_CameraSystem] Original camera settings restored");
        }

        /// <summary>
        /// 重置相机系统
        /// </summary>
        public void ResetCameraSystem()
        {
            StopCameraFollowing();
            RestoreOriginalCamera();

            if (verboseLogs)
                Debug.Log("[BossC2_CameraSystem] Camera system reset");
        }

        #endregion

        #region Debug

        /// <summary>
        /// 获取调试信息
        /// </summary>
        public string GetDebugInfo()
        {
            return $"Boss Camera: {_isBossCameraActive}, Transitioning: {_isTransitioning}, Following: {(_cameraFollowCR != null)}";
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmos()
        {
            if (!drawGizmos) return;

            // 绘制锚点范围
            if (anchorPoint != null)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(anchorPoint.position, anchorMaxDistance);
            }

            // 绘制相机目标位置
            if (_isBossCameraActive)
            {
                Vector3 targetPos = CalculateTargetCameraPosition();
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(targetPos, 0.5f);
                Gizmos.DrawLine(transform.position, targetPos);
            }
        }

        #endregion
    }
}
