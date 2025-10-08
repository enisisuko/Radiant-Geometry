// BossC2_TorchSystem.cs
// 火炬系统 - 负责火炬窗口管理、火炬点燃回调和分身系统
// Unity 2021+ / Unity 6.2 兼容（URP/HDRP均可）

using FadedDreams.World.Light;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FadedDreams.Bosses
{
    /// <summary>
    /// BossC2火炬系统 - 负责火炬窗口管理、火炬点燃回调和分身系统
    /// </summary>
    [DisallowMultipleComponent]
    public class BossC2_TorchSystem : MonoBehaviour
    {
        [Header("== Torch Settings ==")]
        public float torchWindowDuration = 8f;
        public float torchCooldown = 15f;
        public float energyGainPerTorch = 50f;
        public float maxEnergy = 100f;

        [Header("== Phase 1 Torches ==")]
        public BossTorchLink[] phase1Torches = new BossTorchLink[4];

        [Header("== Phase 2 Torches ==")]
        public BossTorchLink[] phase2Torches = new BossTorchLink[4];

        [Header("== Clone System ==")]
        public BossC2Clone clonePrefab;
        public float cloneDuration = 5f;
        public int maxClones = 2;

        [Header("== Ambient Heating ==")]
        public bool enableAmbientHeating = true;
        public float ambientHeatingRate = 2f;
        public float ambientHeatingInterval = 1f;

        [Header("== Debug ==")]
        public bool verboseLogs = true;

        // 组件引用
        private BossC2_Core core;
        private BossC2_PhaseSystem phaseSystem;

        // 火炬状态
        private bool _torchWindowActive = false;
        private float _torchWindowStartTime = 0f;
        private float _lastTorchTime = 0f;
        private float _currentEnergy = 0f;
        private Coroutine _torchWindowCR;
        private Coroutine _ambientHeatingCR;

        // 分身状态
        private int _activeClones = 0;
        private List<BossC2Clone> _cloneList = new List<BossC2Clone>();

        // 事件
        public event Action OnTorchWindowOpened;
        public event Action OnTorchWindowClosed;
        public event Action<BossTorchLink> OnTorchIgnited;
        public event Action<float> OnEnergyGained;
        public event Action OnEnergyFull;
        public event Action<BossC2Clone> OnCloneCreated;
        public event Action<BossC2Clone> OnCloneDestroyed;

        #region Unity Lifecycle

        private void Awake()
        {
            core = GetComponent<BossC2_Core>();
            phaseSystem = GetComponent<BossC2_PhaseSystem>();
        }

        private void Start()
        {
            // 订阅阶段事件
            if (phaseSystem != null)
            {
                phaseSystem.OnPhase1Started += OnPhase1Started;
                phaseSystem.OnPhase2Started += OnPhase2Started;
            }

            // 启动环境加热
            if (enableAmbientHeating)
            {
                _ambientHeatingCR = StartCoroutine(AmbientHeatingCoroutine());
            }
        }

        private void OnDestroy()
        {
            // 取消订阅事件
            if (phaseSystem != null)
            {
                phaseSystem.OnPhase1Started -= OnPhase1Started;
                phaseSystem.OnPhase2Started -= OnPhase2Started;
            }

            // 停止协程
            if (_torchWindowCR != null)
            {
                StopCoroutine(_torchWindowCR);
            }

            if (_ambientHeatingCR != null)
            {
                StopCoroutine(_ambientHeatingCR);
            }

            // 清理分身
            ClearAllClones();
        }

        #endregion

        #region Torch Window Management

        /// <summary>
        /// 打开火炬窗口
        /// </summary>
        public void OpenTorchWindow()
        {
            if (_torchWindowActive) return;

            if (verboseLogs)
                Debug.Log("[BossC2_TorchSystem] Opening torch window");

            _torchWindowActive = true;
            _torchWindowStartTime = Time.time;

            // 激活当前阶段的火炬
            ActivateCurrentPhaseTorches();

            // 启动火炬窗口协程
            if (_torchWindowCR != null)
            {
                StopCoroutine(_torchWindowCR);
            }
            _torchWindowCR = StartCoroutine(TorchWindowCoroutine());

            OnTorchWindowOpened?.Invoke();
        }

        /// <summary>
        /// 关闭火炬窗口
        /// </summary>
        public void CloseTorchWindow()
        {
            if (!_torchWindowActive) return;

            if (verboseLogs)
                Debug.Log("[BossC2_TorchSystem] Closing torch window");

            _torchWindowActive = false;

            // 停用所有火炬
            DeactivateAllTorches();

            // 停止火炬窗口协程
            if (_torchWindowCR != null)
            {
                StopCoroutine(_torchWindowCR);
                _torchWindowCR = null;
            }

            OnTorchWindowClosed?.Invoke();
        }

        /// <summary>
        /// 火炬窗口协程
        /// </summary>
        private IEnumerator TorchWindowCoroutine()
        {
            yield return new WaitForSeconds(torchWindowDuration);
            CloseTorchWindow();
        }

        /// <summary>
        /// 激活当前阶段的火炬
        /// </summary>
        private void ActivateCurrentPhaseTorches()
        {
            int currentPhase = phaseSystem != null ? phaseSystem.GetCurrentPhase() : 1;
            BossTorchLink[] torches = (currentPhase == 1) ? phase1Torches : phase2Torches;

            foreach (BossTorchLink torch in torches)
            {
                if (torch != null)
                {
                    torch.SetActive(true);
                    torch.OnTorchIgnited += HandleTorchIgnited;
                }
            }
        }

        /// <summary>
        /// 停用所有火炬
        /// </summary>
        private void DeactivateAllTorches()
        {
            // 停用阶段1火炬
            foreach (BossTorchLink torch in phase1Torches)
            {
                if (torch != null)
                {
                    torch.SetActive(false);
                    torch.OnTorchIgnited -= HandleTorchIgnited;
                }
            }

            // 停用阶段2火炬
            foreach (BossTorchLink torch in phase2Torches)
            {
                if (torch != null)
                {
                    torch.SetActive(false);
                    torch.OnTorchIgnited -= HandleTorchIgnited;
                }
            }
        }

        #endregion

        #region Torch Events

        /// <summary>
        /// 火炬点燃回调
        /// </summary>
        private void HandleTorchIgnited(BossTorchLink link)
        {
            if (verboseLogs)
                Debug.Log($"[BossC2_TorchSystem] Torch ignited: {link.name}");

            // 获得能量
            GainEnergy(energyGainPerTorch);

            // 记录火炬时间
            _lastTorchTime = Time.time;

            // 触发事件
            OnTorchIgnited?.Invoke(link);

            // 检查是否可以创建分身
            if (_currentEnergy >= maxEnergy)
            {
                CreateClone();
            }
        }

        #endregion

        #region Energy Management

        /// <summary>
        /// 获得能量
        /// </summary>
        private void GainEnergy(float amount)
        {
            float oldEnergy = _currentEnergy;
            _currentEnergy = Mathf.Min(maxEnergy, _currentEnergy + amount);

            if (_currentEnergy != oldEnergy)
            {
                OnEnergyGained?.Invoke(_currentEnergy - oldEnergy);

                if (verboseLogs)
                    Debug.Log($"[BossC2_TorchSystem] Energy gained: {_currentEnergy - oldEnergy}. Total: {_currentEnergy}/{maxEnergy}");

                // 检查是否满能量
                if (_currentEnergy >= maxEnergy)
                {
                    OnEnergyFull?.Invoke();
                    if (verboseLogs)
                        Debug.Log("[BossC2_TorchSystem] Energy is full!");
                }
            }
        }

        /// <summary>
        /// 消耗能量
        /// </summary>
        public void ConsumeEnergy(float amount)
        {
            _currentEnergy = Mathf.Max(0f, _currentEnergy - amount);

            if (verboseLogs)
                Debug.Log($"[BossC2_TorchSystem] Energy consumed: {amount}. Remaining: {_currentEnergy}/{maxEnergy}");
        }

        /// <summary>
        /// 设置能量
        /// </summary>
        public void SetEnergy(float energy)
        {
            _currentEnergy = Mathf.Clamp(energy, 0f, maxEnergy);

            if (verboseLogs)
                Debug.Log($"[BossC2_TorchSystem] Energy set to: {_currentEnergy}/{maxEnergy}");
        }

        #endregion

        #region Clone System

        /// <summary>
        /// 创建分身
        /// </summary>
        public void CreateClone()
        {
            if (_activeClones >= maxClones || clonePrefab == null) return;

            if (verboseLogs)
                Debug.Log($"[BossC2_TorchSystem] Creating clone ({_activeClones + 1}/{maxClones})");

            // 创建分身
            BossC2Clone clone = Instantiate(clonePrefab, transform.position, transform.rotation);
            clone.Setup(transform, cloneDuration);

            // 添加到列表
            _cloneList.Add(clone);
            _activeClones++;

            // 订阅分身事件
            clone.OnCloneDestroyed += HandleCloneDestroyed;

            // 消耗能量
            ConsumeEnergy(maxEnergy);

            OnCloneCreated?.Invoke(clone);
        }

        /// <summary>
        /// 分身被销毁的回调
        /// </summary>
        private void HandleCloneDestroyed(BossC2Clone clone)
        {
            if (_cloneList.Contains(clone))
            {
                _cloneList.Remove(clone);
                _activeClones--;

                if (verboseLogs)
                    Debug.Log($"[BossC2_TorchSystem] Clone destroyed. Remaining: {_activeClones}");

                OnCloneDestroyed?.Invoke(clone);
            }
        }

        /// <summary>
        /// 清理所有分身
        /// </summary>
        public void ClearAllClones()
        {
            foreach (BossC2Clone clone in _cloneList)
            {
                if (clone != null)
                {
                    clone.OnCloneDestroyed -= HandleCloneDestroyed;
                    Destroy(clone.gameObject);
                }
            }

            _cloneList.Clear();
            _activeClones = 0;

            if (verboseLogs)
                Debug.Log("[BossC2_TorchSystem] All clones cleared");
        }

        #endregion

        #region Ambient Heating

        /// <summary>
        /// 环境加热协程
        /// </summary>
        private IEnumerator AmbientHeatingCoroutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(ambientHeatingInterval);

                if (enableAmbientHeating && _currentEnergy < maxEnergy)
                {
                    GainEnergy(ambientHeatingRate);
                }
            }
        }

        /// <summary>
        /// 设置环境加热
        /// </summary>
        public void SetAmbientHeating(bool enabled)
        {
            enableAmbientHeating = enabled;

            if (enabled && _ambientHeatingCR == null)
            {
                _ambientHeatingCR = StartCoroutine(AmbientHeatingCoroutine());
            }
            else if (!enabled && _ambientHeatingCR != null)
            {
                StopCoroutine(_ambientHeatingCR);
                _ambientHeatingCR = null;
            }
        }

        #endregion

        #region Phase Events

        /// <summary>
        /// 阶段1开始
        /// </summary>
        private void OnPhase1Started()
        {
            if (verboseLogs)
                Debug.Log("[BossC2_TorchSystem] Phase 1 started");

            // 可以在这里添加阶段1特定的火炬逻辑
        }

        /// <summary>
        /// 阶段2开始
        /// </summary>
        private void OnPhase2Started()
        {
            if (verboseLogs)
                Debug.Log("[BossC2_TorchSystem] Phase 2 started");

            // 可以在这里添加阶段2特定的火炬逻辑
        }

        #endregion

        #region Public API

        /// <summary>
        /// 获取火炬窗口是否激活
        /// </summary>
        public bool IsTorchWindowActive() => _torchWindowActive;

        /// <summary>
        /// 获取当前能量
        /// </summary>
        public float GetCurrentEnergy() => _currentEnergy;

        /// <summary>
        /// 获取最大能量
        /// </summary>
        public float GetMaxEnergy() => maxEnergy;

        /// <summary>
        /// 获取能量百分比
        /// </summary>
        public float GetEnergyPercentage() => maxEnergy > 0f ? _currentEnergy / maxEnergy : 0f;

        /// <summary>
        /// 获取活跃分身数量
        /// </summary>
        public int GetActiveCloneCount() => _activeClones;

        /// <summary>
        /// 获取最大分身数量
        /// </summary>
        public int GetMaxCloneCount() => maxClones;

        /// <summary>
        /// 检查是否可以创建分身
        /// </summary>
        public bool CanCreateClone() => _activeClones < maxClones && _currentEnergy >= maxEnergy;

        /// <summary>
        /// 检查是否可以打开火炬窗口
        /// </summary>
        public bool CanOpenTorchWindow() => !_torchWindowActive && Time.time - _lastTorchTime >= torchCooldown;

        /// <summary>
        /// 重置火炬系统
        /// </summary>
        public void ResetTorchSystem()
        {
            CloseTorchWindow();
            ClearAllClones();
            SetEnergy(0f);
            _lastTorchTime = 0f;

            if (verboseLogs)
                Debug.Log("[BossC2_TorchSystem] Torch system reset");
        }

        #endregion

        #region Debug

        /// <summary>
        /// 获取调试信息
        /// </summary>
        public string GetDebugInfo()
        {
            return $"Torch Window: {_torchWindowActive}, Energy: {_currentEnergy:F1}/{maxEnergy:F1}, Clones: {_activeClones}/{maxClones}";
        }

        #endregion
    }
}
