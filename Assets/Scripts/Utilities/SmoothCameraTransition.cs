// SmoothCameraTransition.cs - 通用的摄像头平滑过渡系统，解决2D项目中的透视摄像头问题
using UnityEngine;
using System.Collections;

namespace FadedDreams.Utilities
{
    /// <summary>
    /// 摄像头平滑过渡系统 - 专门解决2D项目中透视摄像头的拉远/拉近问题
    /// </summary>
    public class SmoothCameraTransition : MonoBehaviour
    {
        [Header("Camera References")]
        public Camera targetCamera;
        public Component cameraFollowScript; // 例如 CameraFollow2D
        
        [Header("Transition Settings")]
        [Tooltip("拉远/拉近的过渡时间（秒）")]
        public float transitionDuration = 2f;
        [Tooltip("拉远/拉近的缓动曲线")]
        public AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        
        [Header("2D Perspective Settings")]
        [Tooltip("2D项目中的拉远方向（通常是Z轴）")]
        public Vector3 pullBackDirection = new Vector3(0, 0, -1);
        [Tooltip("拉远距离")]
        public float pullBackDistance = 6f;
        [Tooltip("是否同时调整FOV")]
        public bool adjustFOV = false;
        [Tooltip("FOV调整倍数")]
        public float fovMultiplier = 1.15f;
        
        [Header("Camera Follow Settings")]
        [Tooltip("是否在战斗中调整跟随目标")]
        public bool adjustFollowTarget = true;
        [Tooltip("战斗中的软区大小")]
        public Vector2 battleSoftZoneSize = new Vector2(4.8f, 4.5f);
        
        // 运行时状态
        private bool _isTransitioning = false;
        private bool _isInBattleMode = false;
        private Coroutine _currentTransition;
        
        // 原始状态缓存
        private Vector3 _originalPosition;
        private float _originalFOV;
        private Transform _originalFollowTarget;
        private Vector2 _originalSoftOffset;
        private Vector2 _originalSoftSize;
        private Transform _battleAnchor;
        
        // 反射缓存（用于动态访问跟随脚本的属性）
        private System.Reflection.PropertyInfo _pTarget;
        private System.Reflection.PropertyInfo _pSoftOffset;
        private System.Reflection.PropertyInfo _pSoftSize;
        
        private void Awake()
        {
            if (!targetCamera) targetCamera = Camera.main;
            
            // 缓存原始状态
            if (targetCamera)
            {
                _originalPosition = targetCamera.transform.position;
                _originalFOV = targetCamera.fieldOfView;
            }
            
            // 设置反射缓存
            if (cameraFollowScript)
            {
                var type = cameraFollowScript.GetType();
                _pTarget = type.GetProperty("target");
                _pSoftOffset = type.GetProperty("softZoneCenterOffset");
                _pSoftSize = type.GetProperty("softZoneSize");
                
                // 缓存原始跟随设置
                if (_pTarget != null) _originalFollowTarget = _pTarget.GetValue(cameraFollowScript) as Transform;
                if (_pSoftOffset != null) _originalSoftOffset = (Vector2)_pSoftOffset.GetValue(cameraFollowScript);
                if (_pSoftSize != null) _originalSoftSize = (Vector2)_pSoftSize.GetValue(cameraFollowScript);
            }
        }
        
        /// <summary>
        /// 开始拉远过渡（进入战斗模式）
        /// </summary>
        public void StartPullBack(Transform playerTransform = null)
        {
            if (_isTransitioning || _isInBattleMode) return;
            
            if (_currentTransition != null)
                StopCoroutine(_currentTransition);
                
            _currentTransition = StartCoroutine(TransitionToBattle(playerTransform));
        }
        
        /// <summary>
        /// 开始拉近过渡（退出战斗模式）
        /// </summary>
        public void StartPullIn()
        {
            if (_isTransitioning || !_isInBattleMode) return;
            
            if (_currentTransition != null)
                StopCoroutine(_currentTransition);
                
            _currentTransition = StartCoroutine(TransitionToNormal());
        }
        
        private IEnumerator TransitionToBattle(Transform playerTransform)
        {
            _isTransitioning = true;
            _isInBattleMode = true;
            
            // 计算目标位置（2D项目中的正确拉远方向）
            Vector3 targetPosition = _originalPosition + pullBackDirection * pullBackDistance;
            float targetFOV = adjustFOV ? _originalFOV * fovMultiplier : _originalFOV;
            
            // 设置战斗跟随目标
            if (adjustFollowTarget && playerTransform && cameraFollowScript)
            {
                SetupBattleFollowTarget(playerTransform);
            }
            
            float elapsed = 0f;
            Vector3 startPosition = targetCamera.transform.position;
            float startFOV = targetCamera.fieldOfView;
            
            while (elapsed < transitionDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / transitionDuration;
                float curveValue = transitionCurve.Evaluate(t);
                
                // 平滑过渡位置
                targetCamera.transform.position = Vector3.Lerp(startPosition, targetPosition, curveValue);
                
                // 平滑过渡FOV
                if (adjustFOV)
                {
                    targetCamera.fieldOfView = Mathf.Lerp(startFOV, targetFOV, curveValue);
                }
                
                yield return null;
            }
            
            // 确保最终状态
            targetCamera.transform.position = targetPosition;
            if (adjustFOV) targetCamera.fieldOfView = targetFOV;
            
            _isTransitioning = false;
        }
        
        private IEnumerator TransitionToNormal()
        {
            _isTransitioning = true;
            
            Vector3 startPosition = targetCamera.transform.position;
            float startFOV = targetCamera.fieldOfView;
            
            float elapsed = 0f;
            
            while (elapsed < transitionDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / transitionDuration;
                float curveValue = transitionCurve.Evaluate(t);
                
                // 平滑过渡回原始位置
                targetCamera.transform.position = Vector3.Lerp(startPosition, _originalPosition, curveValue);
                
                // 平滑过渡回原始FOV
                if (adjustFOV)
                {
                    targetCamera.fieldOfView = Mathf.Lerp(startFOV, _originalFOV, curveValue);
                }
                
                yield return null;
            }
            
            // 确保最终状态
            targetCamera.transform.position = _originalPosition;
            if (adjustFOV) targetCamera.fieldOfView = _originalFOV;
            
            // 恢复跟随设置
            RestoreFollowSettings();
            
            _isTransitioning = false;
            _isInBattleMode = false;
        }
        
        private void SetupBattleFollowTarget(Transform playerTransform)
        {
            if (!cameraFollowScript || !playerTransform) return;
            
            // 创建战斗锚点
            if (!_battleAnchor)
            {
                var anchorGO = new GameObject("BattleCameraAnchor");
                _battleAnchor = anchorGO.transform;
            }
            
            _battleAnchor.position = playerTransform.position;
            
            // 设置跟随目标
            if (_pTarget != null)
                _pTarget.SetValue(cameraFollowScript, _battleAnchor);
            
            // 设置战斗软区大小
            if (_pSoftSize != null)
                _pSoftSize.SetValue(cameraFollowScript, battleSoftZoneSize);
        }
        
        private void RestoreFollowSettings()
        {
            if (!cameraFollowScript) return;
            
            // 恢复原始跟随目标
            if (_pTarget != null)
                _pTarget.SetValue(cameraFollowScript, _originalFollowTarget);
            
            // 恢复原始软区设置
            if (_pSoftOffset != null)
                _pSoftOffset.SetValue(cameraFollowScript, _originalSoftOffset);
            
            if (_pSoftSize != null)
                _pSoftSize.SetValue(cameraFollowScript, _originalSoftSize);
            
            // 清理战斗锚点
            if (_battleAnchor)
            {
                Destroy(_battleAnchor.gameObject);
                _battleAnchor = null;
            }
        }
        
        /// <summary>
        /// 更新战斗锚点位置（在战斗中持续调用）
        /// </summary>
        public void UpdateBattleAnchor(Transform playerTransform)
        {
            if (_battleAnchor && playerTransform)
            {
                _battleAnchor.position = Vector3.Lerp(_battleAnchor.position, playerTransform.position, Time.deltaTime * 10f);
            }
        }
        
        /// <summary>
        /// 强制立即恢复到正常状态（用于紧急情况）
        /// </summary>
        public void ForceRestore()
        {
            if (_currentTransition != null)
            {
                StopCoroutine(_currentTransition);
                _currentTransition = null;
            }
            
            targetCamera.transform.position = _originalPosition;
            if (adjustFOV) targetCamera.fieldOfView = _originalFOV;
            
            RestoreFollowSettings();
            
            _isTransitioning = false;
            _isInBattleMode = false;
        }
        
        private void OnDestroy()
        {
            if (_battleAnchor)
            {
                Destroy(_battleAnchor.gameObject);
            }
        }
        
        // 调试信息
        private void OnDrawGizmosSelected()
        {
            if (!targetCamera) return;
            
            Gizmos.color = Color.yellow;
            Vector3 targetPos = _originalPosition + pullBackDirection * pullBackDistance;
            Gizmos.DrawLine(_originalPosition, targetPos);
            Gizmos.DrawWireSphere(targetPos, 0.5f);
            
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(_originalPosition, 0.3f);
        }
    }
}
