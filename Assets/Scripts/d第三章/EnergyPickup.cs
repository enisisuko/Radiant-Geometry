using UnityEngine;

namespace FadedDreams.World
{
    /// <summary>
    /// 敌人死亡掉落：根据敌人自身阵营生成对应颜色的补给。
    /// 具有磁吸：玩家进入吸附半径后自动向玩家飘去；接触后恢复能量并自毁。
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class EnergyPickup : MonoBehaviour
    {
        public FadedDreams.Player.ColorMode energyColor = FadedDreams.Player.ColorMode.Red;
        public float amount = 20f;
        public float life = 12f;

        [Header("Magnet")]
        public float attractRadius = 6f;
        public float absorbRadius = 0.35f;   // 接触半径（除触发器外的额外安全吸收）
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
                // 朝玩家飞去（匀速）
                Vector3 dir = (_player.position - transform.position).normalized;
                transform.position += dir * flySpeed * Time.deltaTime;
            }

            // 额外的“吸收半径”兜底：即使没触发碰撞，也能吃到
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
