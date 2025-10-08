using UnityEngine;

namespace FadedDreams.UI
{
    /// <summary>
    /// 流体动画引擎
    /// 负责控制流体动画的播放、状态管理和动画曲线
    /// </summary>
    public class FluidAnimationEngine : MonoBehaviour
    {
        [Header("动画曲线")]
        public AnimationCurve fluidEaseCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        public AnimationCurve squeezeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        public AnimationCurve expandCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        
        [Header("弹簧参数")]
        public float springConstant = 10f;
        public float damping = 0.8f;
        public float mass = 1f;
        
        // 动画状态
        private bool isAnimating = false;
        private float animationTime = 0f;
        private float animationDuration = 1f;
        private AnimationType currentAnimationType = AnimationType.None;
        private int targetIndex = -1;
        
        // 引用其他系统
        private new FluidParticleSystem particleSystem;
        private FluidPhysicsEngine physicsEngine;
        
        public enum AnimationType
        {
            None,
            Squeeze,
            Expand
        }
        
        void Start()
        {
            particleSystem = GetComponent<FluidParticleSystem>();
            physicsEngine = GetComponent<FluidPhysicsEngine>();
        }
        
        void Update()
        {
            if (isAnimating)
            {
                UpdateAnimation();
            }
        }
        
        public void StartSqueezeAnimation(int hoveredIndex, float duration = 1f)
        {
            if (isAnimating) return;
            
            animationTime = 0f;
            animationDuration = duration;
            isAnimating = true;
            currentAnimationType = AnimationType.Squeeze;
            targetIndex = hoveredIndex;
            
            // 设置挤压目标
            if (particleSystem != null)
            {
                particleSystem.SetSqueezeTargets(hoveredIndex);
            }
        }
        
        public void StartExpandAnimation(int selectedIndex, float duration = 1.5f)
        {
            if (isAnimating) return;
            
            animationTime = 0f;
            animationDuration = duration;
            isAnimating = true;
            currentAnimationType = AnimationType.Expand;
            targetIndex = selectedIndex;
            
            // 设置扩展目标
            if (particleSystem != null)
            {
                particleSystem.SetExpandTargets(selectedIndex);
            }
        }
        
        void UpdateAnimation()
        {
            animationTime += Time.deltaTime;
            float progress = Mathf.Clamp01(animationTime / animationDuration);
            
            // 根据动画类型应用不同的曲线
            float curveValue = GetCurveValue(progress);
            
            // 更新物理模拟
            if (physicsEngine != null)
            {
                physicsEngine.UpdatePhysics(Time.deltaTime, damping);
            }
            
            // 检查动画是否完成
            if (progress >= 1f)
            {
                isAnimating = false;
                OnAnimationComplete();
            }
        }
        
        float GetCurveValue(float progress)
        {
            switch (currentAnimationType)
            {
                case AnimationType.Squeeze:
                    return squeezeCurve.Evaluate(progress);
                case AnimationType.Expand:
                    return expandCurve.Evaluate(progress);
                default:
                    return fluidEaseCurve.Evaluate(progress);
            }
        }
        
        void OnAnimationComplete()
        {
            // 动画完成后的处理
            Debug.Log($"流体动画完成: {currentAnimationType}");
            currentAnimationType = AnimationType.None;
            targetIndex = -1;
        }
        
        public bool IsAnimating()
        {
            return isAnimating;
        }
        
        public float GetAnimationProgress()
        {
            return Mathf.Clamp01(animationTime / animationDuration);
        }
        
        public AnimationType GetCurrentAnimationType()
        {
            return currentAnimationType;
        }
        
        public int GetTargetIndex()
        {
            return targetIndex;
        }
        
        public void ResetAnimation()
        {
            isAnimating = false;
            animationTime = 0f;
            currentAnimationType = AnimationType.None;
            targetIndex = -1;
            
            // 重置所有粒子到初始位置
            if (particleSystem != null)
            {
                particleSystem.ResetParticles();
            }
            
            // 重置物理场
            if (physicsEngine != null)
            {
                physicsEngine.ResetFields();
            }
        }
        
        public void StopAnimation()
        {
            isAnimating = false;
            currentAnimationType = AnimationType.None;
            targetIndex = -1;
        }
        
        public void SetAnimationDuration(float duration)
        {
            animationDuration = duration;
        }
        
        public void SetDamping(float newDamping)
        {
            damping = newDamping;
        }
        
        public void SetSpringConstant(float newSpringConstant)
        {
            springConstant = newSpringConstant;
        }
        
        public void SetMass(float newMass)
        {
            mass = newMass;
        }
        
        public float GetAnimationTime()
        {
            return animationTime;
        }
        
        public float GetAnimationDuration()
        {
            return animationDuration;
        }
        
        public float GetDamping()
        {
            return damping;
        }
        
        public float GetSpringConstant()
        {
            return springConstant;
        }
        
        public float GetMass()
        {
            return mass;
        }
    }
}
