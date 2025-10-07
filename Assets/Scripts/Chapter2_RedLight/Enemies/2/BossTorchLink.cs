// Assets/Scripts/Bosses/Chapter2/BossTorchLink.cs
using UnityEngine;
using System;
using FadedDreams.World.Light;  // Torch
using FadedDreams.Player;       // RedLightController

namespace FadedDreams.Bosses
{
    [DisallowMultipleComponent]
    public class BossTorchLink : MonoBehaviour
    {
        public Torch torch;

        // 当“被点燃”时（= Torch.selfRed 从0→>0）发出通知
        public event Action<BossTorchLink> onIgnited;

        private void Reset()
        {
            if (!torch) torch = GetComponent<Torch>();
        }

        private void Awake()
        {
            if (!torch) torch = GetComponent<Torch>();
        }

        private void OnEnable()
        {
            if (torch && torch.selfRed != null)
            {
                // Torch.cs 的 selfRed.onRelit 在“从0恢复到>0”时触发
                torch.selfRed.onRelit.AddListener(HandleRelit);
            }
        }

        private void OnDisable()
        {
            if (torch && torch.selfRed != null)
            {
                torch.selfRed.onRelit.RemoveListener(HandleRelit);
            }
        }

        private void HandleRelit()
        {
            onIgnited?.Invoke(this);
        }
    }
}
