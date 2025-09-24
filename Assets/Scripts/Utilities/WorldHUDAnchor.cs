using UnityEngine;

namespace FadedDreams.UI
{
    public class WorldHUDAnchor : MonoBehaviour
    {
        public Transform player;              // 拖 Player
        public Vector3 worldOffset = new Vector3(0, 1.6f, 0.0f);
        public Camera cam;                    // 可不填，自动 Camera.main
        public bool faceCamera = true;
        public float followLerp = 15f;        // 平滑跟随

        void Start()
        {
            if (!cam) cam = Camera.main;
        }

        void LateUpdate()
        {
            if (!player) return;
            // 平滑移动到玩家头顶
            transform.position = Vector3.Lerp(transform.position, player.position + worldOffset, 1 - Mathf.Exp(-followLerp * Time.deltaTime));

            // 朝向相机（世界 UI 可读性）
            if (faceCamera && cam)
            {
                var fwd = (transform.position - cam.transform.position);
                if (fwd.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up); // 2D：保持正朝上
            }
        }
    }
}
