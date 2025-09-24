using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FadedDreams.UI
{
    public class ProjectedMenuController : MonoBehaviour
    {
        public enum MenuState { Idle, Focus, ContinueSubmenu }

        [Header("Refs")]
        public Camera cam;
        public LayerMask interactMask;
        public Transform itemsRoot;

        [Header("Slide Timing")]
        public float othersSlideOutDist = 12f;
        public float othersSlideOutDur = 0.5f;
        public float othersSlideInDur = 0.45f;
        public AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Focus Camera")]
        public float focusDistance = 3.5f;
        public float focusHeightOffset = 0.15f;
        public float focusMoveDur = 0.6f;
        public AnimationCurve focusEase = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Enter Fade (Leave Scene)")]
        public float enterFadeOutDur = 1.2f;

        // ======================= 新增：入场黑屏与相机拉回 =======================
        [Header("Enter Intro (Fade-In & Camera Pullback)")]
        [Tooltip("进入场景时是否播放 2 秒黑屏淡入并拉回相机的开场动画")]
        public bool playEnterIntro = true;

        [Tooltip("开场时相机相对基准位置向前推进的距离（数值越大开场时越靠近菜单物体）")]
        public float enterPullAheadDist = 1.0f;

        [Tooltip("黑屏淡入与相机拉回的时长")]
        public float enterFadeInDur = 2.0f;

        public AnimationCurve enterEase = AnimationCurve.EaseInOut(0, 0, 1, 1);
        // ====================================================================

        [Header("Orchestrator (optional)")]
        [SerializeField] MainMenuOrchestrator orchestrator;

        [Header("Mouse Spotlight (optional)")]
        [Tooltip("若为空，将在 Awake 中从相机下寻找")]
        public MouseSpotlight mouseSpot;

        private readonly List<ProjectedMenuItem> items = new();
        private ProjectedMenuItem hovered;
        private ProjectedMenuItem focused;
        private MenuState state = MenuState.Idle;

        Vector3 camBasePos;
        Quaternion camBaseRot;
        Coroutine camMoveCo;

        // 新增：入场动画期间屏蔽交互
        bool introPlaying = false;

        void Awake()
        {
            if (!cam) cam = Camera.main;
            camBasePos = cam.transform.position;
            camBaseRot = cam.transform.rotation;

            foreach (Transform t in itemsRoot)
            {
                var item = t.GetComponent<ProjectedMenuItem>();
                if (item) { item.BindController(this); items.Add(item); }
            }

            if (!orchestrator)
            {
#if UNITY_6_0_OR_NEWER
                orchestrator = Object.FindFirstObjectByType<MainMenuOrchestrator>(FindObjectsInactive.Exclude);
#else
                orchestrator = Object.FindObjectOfType<MainMenuOrchestrator>();
#endif
            }

            if (!mouseSpot && cam)
            {
                mouseSpot = cam.GetComponentInChildren<MouseSpotlight>(true);
            }
        }

        // 新增：在 Start 里触发入场动画
        void Start()
        {
            if (playEnterIntro && cam)
                StartCoroutine(CoEnterIntro());
        }

        void Update()
        {
            HandleHover();
            HandleClick();
        }

        void HandleHover()
        {
            // 新增：入场动画时不响应悬停
            if (introPlaying) return;
            if (state != MenuState.Idle) return;

            var newHover = RaycastMouseForItem();
            if (hovered != newHover)
            {
                if (hovered) hovered.SetHover(false);
                hovered = newHover;

                float dx = Input.GetAxisRaw("Mouse X");
                if (newHover) newHover.SetSkewSign(dx);
                if (hovered) hovered.SetHover(true);

                // 告知相机聚光灯当前是否悬停在按钮上
                if (mouseSpot) mouseSpot.SetHoveringUI(hovered != null);
            }
        }

        void HandleClick()
        {
            // 新增：入场动画时不响应点击
            if (introPlaying) return;

            if (!Input.GetMouseButtonDown(0)) return;

            var hitItem = RaycastMouseForItem();

            switch (state)
            {
                case MenuState.Idle:
                    if (!hitItem) return;

                    if (hitItem.actionType == ProjectedMenuItem.ActionType.Continue)
                    {
                        OpenContinueSubmenu(hitItem);
                    }
                    else
                    {
                        EnterFocus(hitItem);
                    }
                    break;

                case MenuState.Focus:
                    if (hitItem == focused)
                    {
                        StartCoroutine(ExecuteFocusedWithFade(focused));
                    }
                    else if (hitItem != null && hitItem != focused)
                    {
                        EnterFocus(hitItem);
                    }
                    else
                    {
                        ExitFocus();
                    }
                    break;

                case MenuState.ContinueSubmenu:
                    if (!hitItem) CloseContinueSubmenu();
                    break;
            }
        }

        ProjectedMenuItem RaycastMouseForItem()
        {
            var ray = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit, 100f, interactMask, QueryTriggerInteraction.Ignore))
            {
                return hit.collider.GetComponent<ProjectedMenuItem>();
            }
            return null;
        }

        // —— 点一次：进入聚焦 —— 
        void EnterFocus(ProjectedMenuItem item)
        {
            if (state == MenuState.ContinueSubmenu) CloseContinueSubmenu();

            foreach (var it in items) it.UnfreezeSlide();

            focused = item;
            focused.EnterSelectedState();
            state = MenuState.Focus;

            SuspendOrchestrator(true);

            Vector3 upXZ = ScreenUpOnGround();
            Vector3 downXZ = -upXZ;

            Vector3 selScreen = cam.WorldToScreenPoint(focused.transform.position);
            foreach (var it in items)
            {
                if (it == focused) continue;
                Vector3 itScreen = cam.WorldToScreenPoint(it.transform.position);
                Vector3 dir = (itScreen.y > selScreen.y) ? upXZ : downXZ;

                Vector3 worldOffset = dir * othersSlideOutDist;
                it.FreezeSlide(worldOffset);
                it.SlideToOffset(worldOffset, othersSlideOutDur, ease);
            }

            MoveCameraToFocus(focused);

            // 聚焦阶段就算不在 Idle，也把相机灯恢复为“未悬停”表现
            if (mouseSpot) mouseSpot.SetHoveringUI(false);
        }

        void ExitFocus()
        {
            if (state != MenuState.Focus) return;

            foreach (var it in items)
            {
                it.UnfreezeSlide();
                it.SlideBack(othersSlideInDur, ease);
            }

            if (focused) focused.ExitSelectedState();
            focused = null;
            state = MenuState.Idle;

            MoveCameraToBase();
            SuspendOrchestrator(false);
        }

        void OpenContinueSubmenu(ProjectedMenuItem continueItem)
        {
            if (state == MenuState.Focus) ExitFocus();

            state = MenuState.ContinueSubmenu;
            SuspendOrchestrator(true);

            Vector3 upXZ = ScreenUpOnGround();
            Vector3 downXZ = -upXZ;

            Vector3 selScreen = cam.WorldToScreenPoint(continueItem.transform.position);
            foreach (var it in items)
            {
                Vector3 itScreen = cam.WorldToScreenPoint(it.transform.position);
                Vector3 dir = (itScreen.y >= selScreen.y) ? upXZ : downXZ;

                Vector3 worldOffset = dir * (othersSlideOutDist * 1.1f);
                it.FreezeSlide(worldOffset);
                it.SlideToOffset(worldOffset, othersSlideOutDur, ease);
            }

            if (mouseSpot) mouseSpot.SetHoveringUI(false);
        }

        void CloseContinueSubmenu()
        {
            if (state != MenuState.ContinueSubmenu) return;

            foreach (var it in items)
            {
                it.UnfreezeSlide();
                it.SlideBack(othersSlideInDur, ease);
            }

            state = MenuState.Idle;
            SuspendOrchestrator(false);
        }

        IEnumerator ExecuteFocusedWithFade(ProjectedMenuItem item)
        {
            item.PlayShatterBrief(0.25f);

            var fader = ScreenFade.Ensure();
            yield return fader.FadeOut(enterFadeOutDur);

            switch (item.actionType)
            {
                case ProjectedMenuItem.ActionType.NewGame: item.mainMenu?.NewGame(); break;
                case ProjectedMenuItem.ActionType.Continue: item.mainMenu?.ContinueGame(); break;
                case ProjectedMenuItem.ActionType.Quit: item.mainMenu?.Quit(); break;
            }
        }

        // 相机工具
        void MoveCameraToFocus(ProjectedMenuItem item)
        {
            StopCamMove();

            Vector3 dir = (cam.transform.position - item.transform.position).normalized;
            Vector3 toPos = item.transform.position + dir * focusDistance + Vector3.up * focusHeightOffset;
            Quaternion toRot = Quaternion.LookRotation((item.transform.position - toPos).normalized, Vector3.up);

            camMoveCo = StartCoroutine(CoMoveCam(cam.transform.position, toPos, cam.transform.rotation, toRot, focusMoveDur, focusEase));
        }

        void MoveCameraToBase()
        {
            StopCamMove();
            camMoveCo = StartCoroutine(CoMoveCam(cam.transform.position, camBasePos, cam.transform.rotation, camBaseRot, 0.5f, focusEase));
        }

        IEnumerator CoMoveCam(Vector3 fromPos, Vector3 toPos, Quaternion fromRot, Quaternion toRot, float dur, AnimationCurve curve)
        {
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = curve.Evaluate(Mathf.Clamp01(t / dur));
                cam.transform.position = Vector3.Lerp(fromPos, toPos, k);
                cam.transform.rotation = Quaternion.Slerp(fromRot, toRot, k);
                yield return null;
            }
            cam.transform.position = toPos;
            cam.transform.rotation = toRot;
        }

        void StopCamMove()
        {
            if (camMoveCo != null) { StopCoroutine(camMoveCo); camMoveCo = null; }
        }

        void SuspendOrchestrator(bool suspend)
        {
            if (orchestrator) orchestrator.enabled = !suspend;
        }

        Vector3 ScreenUpOnGround()
        {
            var v = Vector3.ProjectOnPlane(cam.transform.up, Vector3.up);
            if (v.sqrMagnitude < 1e-4f) v = Vector3.forward;
            return v.normalized;
        }

        // ======================= 新增：入场动画协程 =======================
        IEnumerator CoEnterIntro()
        {
            introPlaying = true;

            // 禁用 orchestrator、防止鼠标聚光灯表现把初始氛围“破功”
            SuspendOrchestrator(true);
            if (mouseSpot) mouseSpot.SetHoveringUI(false);

            // 计算“更靠前”的初始位姿（按基准朝向的 Forward 推进）
            Vector3 aheadPos = camBasePos + (camBaseRot * Vector3.forward) * enterPullAheadDist;
            cam.transform.position = aheadPos;
            cam.transform.rotation = camBaseRot;

            // 先瞬间变黑，再在 enterFadeInDur 秒内淡入；同时相机从 aheadPos 拉回 camBasePos
            var fader = ScreenFade.Ensure();
            // 利用 FadeOut(0) 立即变黑，然后并行动画：FadeIn + 相机拉回
            yield return fader.FadeOut(0f);
            StartCoroutine(fader.FadeIn(enterFadeInDur));

            yield return CoMoveCam(aheadPos, camBasePos, camBaseRot, camBaseRot, enterFadeInDur, enterEase);

            introPlaying = false;
            SuspendOrchestrator(false);
        }
        // ====================================================================
    }
}
