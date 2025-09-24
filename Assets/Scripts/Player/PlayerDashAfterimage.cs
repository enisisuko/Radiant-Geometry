using UnityEngine;
using FadedDreams.VFX;


namespace FadedDreams.Player
{
    /// <summary>
    /// ��ҳ����Ч���������Χ����һ������Ӱ��������������һ����Ӱ��
    /// �����ţ�ֻ͸������ʱ��˥����
    /// </summary>
    [RequireComponent(typeof(AfterimageTrail2D))]
    public class PlayerDashAfterimage : MonoBehaviour
    {
        public bool autoEmitDuringDash = false; // ����� dash �г����Σ����� dash �ڼ� BeginEmit/StopEmit
        public float ghostHoldSeconds = 0.08f;  // �����㡰������Ӱ����ͣ��ʱ��

        private AfterimageTrail2D _trail;

        private void Awake()
        {
            _trail = GetComponent<AfterimageTrail2D>();
        }

        /// <summary> ����� Dash ��������� </summary>
        public void PlayDashVFX(Vector2 dashDir)
        {
            if (!_trail) return;
            // 1) ��Ӱ�������̴�һ�� Burst��
            _trail.BurstOnce();
            // 2) ��ҡ�������Ӱ��= ��ǰλ�ô�һ�ſ��ղ���ʱ���٣��� Afterimage �ĵ�һ�ż��ɣ�
            //    ��Ϊ�˼򵥣���Ȼ���� Afterimage��һ֡�����Ϳ�ʼ������
            // ��� dash ��һ�γ�������Ҳ���ԣ�
            // if (autoEmitDuringDash) _trail.BeginEmit();  �� dash ����ʱ StopEmit();
        }

        // ��ѡ����¶��������� Dash Э����
        public void BeginContinuous() => _trail?.BeginEmit();
        public void EndContinuous() => _trail?.StopEmit();
    }
}
