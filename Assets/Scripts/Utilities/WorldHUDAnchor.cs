using UnityEngine;

namespace FadedDreams.UI
{
    public class WorldHUDAnchor : MonoBehaviour
    {
        public Transform player;              // �� Player
        public Vector3 worldOffset = new Vector3(0, 1.6f, 0.0f);
        public Camera cam;                    // �ɲ���Զ� Camera.main
        public bool faceCamera = true;
        public float followLerp = 15f;        // ƽ������

        void Start()
        {
            if (!cam) cam = Camera.main;
        }

        void LateUpdate()
        {
            if (!player) return;
            // ƽ���ƶ������ͷ��
            transform.position = Vector3.Lerp(transform.position, player.position + worldOffset, 1 - Mathf.Exp(-followLerp * Time.deltaTime));

            // ������������� UI �ɶ��ԣ�
            if (faceCamera && cam)
            {
                var fwd = (transform.position - cam.transform.position);
                if (fwd.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up); // 2D������������
            }
        }
    }
}
