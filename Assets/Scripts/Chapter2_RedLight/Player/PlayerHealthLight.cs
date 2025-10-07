using UnityEngine;

namespace FadedDreams.Player
{
    /// <summary>
    /// ͳһ��������ڣ�������˼��ۡ���ǰģʽ����������
    /// ����� TakeDamage(dmg)��
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerHealthLight : MonoBehaviour
    {
        [Tooltip("����ʱ��ѡ����С���������һ֡������е���")]
        public float hurtIFrame = 0.05f;

        private float _lastHurtTime = -999f;
        private PlayerColorModeController _mode;

        private void Awake()
        {
            _mode = GetComponent<PlayerColorModeController>();
            if (!_mode) _mode = GetComponentInParent<PlayerColorModeController>();
        }

        public void TakeDamage(float amount)
        {
            if (Time.time - _lastHurtTime < hurtIFrame) return;
            _lastHurtTime = Time.time;

            if (!_mode) return;
            // �ۡ���ǰģʽ��������ֵ
            _mode.SpendEnergy(_mode.Mode, Mathf.Max(0f, amount));
            // �������Ҫ������Ļ��Ч/��Ч�����������ﹳ
        }
    }
}
