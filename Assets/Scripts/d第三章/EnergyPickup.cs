using UnityEngine;

namespace FadedDreams.World
{
    /// <summary>
    /// �����������䣺���ݵ���������Ӫ���ɶ�Ӧ��ɫ�Ĳ�����
    /// ���д�������ҽ��������뾶���Զ������Ʈȥ���Ӵ���ָ��������Ի١�
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class EnergyPickup : MonoBehaviour
    {
        public FadedDreams.Player.ColorMode energyColor = FadedDreams.Player.ColorMode.Red;
        public float amount = 20f;
        public float life = 12f;

        [Header("Magnet")]
        public float attractRadius = 6f;
        public float absorbRadius = 0.35f;   // �Ӵ��뾶������������Ķ��ⰲȫ���գ�
        public float flySpeed = 8f;

        private Transform _player;

        private void Start()
        {
            Destroy(gameObject, life);
            var col = GetComponent<Collider2D>();
            col.isTrigger = true;
            _player = FindObjectOfType<FadedDreams.Player.PlayerColorModeController>()?.transform;
        }

        private void Update()
        {
            if (!_player) return;

            float d = Vector2.Distance(transform.position, _player.position);
            if (d <= attractRadius)
            {
                // ����ҷ�ȥ�����٣�
                Vector3 dir = (_player.position - transform.position).normalized;
                transform.position += dir * flySpeed * Time.deltaTime;
            }

            // ����ġ����հ뾶�����ף���ʹû������ײ��Ҳ�ܳԵ�
            if (d <= absorbRadius)
                AbsorbToPlayer();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            AbsorbToPlayer();
        }

        private void AbsorbToPlayer()
        {
            var pcm = FindObjectOfType<FadedDreams.Player.PlayerColorModeController>();
            if (pcm) pcm.AddEnergy(energyColor, amount);
            Destroy(gameObject);
        }
    }
}
