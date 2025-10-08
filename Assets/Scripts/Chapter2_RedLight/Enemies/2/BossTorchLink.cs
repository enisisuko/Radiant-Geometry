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

        // ��������ȼ��ʱ��= Torch.selfRed ��0��>0������֪ͨ
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
                // Torch.cs �� selfRed.onRelit �ڡ���0�ָ���>0��ʱ����
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

        public void SetActive(bool active)
        {
            gameObject.SetActive(active);
        }

        public void OnTorchIgnited(System.Action<BossTorchLink> callback)
        {
            if (callback != null)
            {
                onIgnited += callback;
            }
        }
    }
}
