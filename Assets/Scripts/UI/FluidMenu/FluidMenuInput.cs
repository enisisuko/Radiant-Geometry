using UnityEngine;
using UnityEngine.EventSystems;

namespace FadedDreams.UI
{
    /// <summary>
    /// 流体菜单输入处理系统
    /// 负责处理鼠标、键盘和触摸输入
    /// </summary>
    public class FluidMenuInput : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [Header("输入设置")]
        public float mouseSensitivity = 1f;
        public float keyboardRepeatDelay = 0.2f;
        public float touchSensitivity = 1f;
        
        [Header("射线检测")]
        public LayerMask blockLayerMask = -1;
        public float raycastDistance = 100f;
        
        [Header("引用")]
        public FluidMenuManager menuManager;
        public Camera menuCamera;
        
        // 输入状态
        private bool isMouseOverUI = false;
        private float lastKeyboardInputTime = 0f;
        private int lastKeyboardInput = -1;
        
        // 触摸支持
        private bool touchSupported = false;
        private Vector2 lastTouchPosition;
        
        void Start()
        {
            InitializeInput();
        }
        
        void Update()
        {
            if (menuManager == null || menuManager.IsTransitioning()) return;
            
            HandleMouseInput();
            HandleKeyboardInput();
            HandleTouchInput();
        }
        
        void InitializeInput()
        {
            // 自动获取组件引用
            if (menuManager == null) menuManager = GetComponent<FluidMenuManager>();
            if (menuCamera == null) menuCamera = Camera.main;
            
            // 检测触摸支持
            touchSupported = Input.touchSupported;
            
            // 设置事件系统
            if (EventSystem.current == null)
            {
                GameObject eventSystemGO = new GameObject("EventSystem");
                eventSystemGO.AddComponent<EventSystem>();
                eventSystemGO.AddComponent<StandaloneInputModule>();
            }
        }
        
        void HandleMouseInput()
        {
            // 检查鼠标是否在UI上
            isMouseOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            
            if (isMouseOverUI) return;
            
            // 处理鼠标悬停
            HandleMouseHover();
            
            // 处理鼠标点击
            if (Input.GetMouseButtonDown(0))
            {
                HandleMouseClick();
            }
        }
        
        void HandleMouseHover()
        {
            Ray ray = menuCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            
            int hoveredIndex = -1;
            
            if (Physics.Raycast(ray, out hit, raycastDistance, blockLayerMask))
            {
                // 检查是否击中了色块
                hoveredIndex = GetBlockIndexFromHit(hit);
            }
            
            // 通知菜单管理器悬停状态变化
            if (hoveredIndex != menuManager.GetCurrentHoveredIndex())
            {
                OnHoverChanged(hoveredIndex);
            }
        }
        
        void HandleMouseClick()
        {
            Ray ray = menuCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            
            if (Physics.Raycast(ray, out hit, raycastDistance, blockLayerMask))
            {
                int clickedIndex = GetBlockIndexFromHit(hit);
                if (clickedIndex >= 0)
                {
                    OnBlockClicked(clickedIndex);
                }
            }
        }
        
        void HandleKeyboardInput()
        {
            // 数字键选择 (1-5)
            for (int i = 1; i <= 5; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha0 + i))
                {
                    int blockIndex = i - 1;
                    if (ShouldProcessKeyboardInput(blockIndex))
                    {
                        OnBlockClicked(blockIndex);
                        lastKeyboardInput = blockIndex;
                        lastKeyboardInputTime = Time.time;
                    }
                }
            }
            
            // ESC键退出
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                OnBlockClicked(3); // 退出游戏
            }
            
            // 方向键导航
            HandleDirectionalInput();
        }
        
        void HandleDirectionalInput()
        {
            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");
            
            if (Mathf.Abs(horizontal) > 0.1f || Mathf.Abs(vertical) > 0.1f)
            {
                int currentHovered = menuManager.GetCurrentHoveredIndex();
                int newHovered = GetNextHoveredIndex(currentHovered, horizontal, vertical);
                
                if (newHovered != currentHovered)
                {
                    OnHoverChanged(newHovered);
                }
            }
        }
        
        int GetNextHoveredIndex(int currentIndex, float horizontal, float vertical)
        {
            // 基于当前悬停的色块和输入方向计算下一个悬停的色块
            // 布局：左上(0) 中心(1) 右上(2) 右下(3) 左下(4)
            
            if (currentIndex < 0) return 1; // 默认选中中心
            
            switch (currentIndex)
            {
                case 0: // 左上
                    if (horizontal > 0.1f) return 1; // 右 -> 中心
                    if (vertical < -0.1f) return 4; // 下 -> 左下
                    break;
                case 1: // 中心
                    if (horizontal < -0.1f) return 0; // 左 -> 左上
                    if (horizontal > 0.1f) return 2; // 右 -> 右上
                    if (vertical > 0.1f) return 0; // 上 -> 左上
                    if (vertical < -0.1f) return 4; // 下 -> 左下
                    break;
                case 2: // 右上
                    if (horizontal < -0.1f) return 1; // 左 -> 中心
                    if (vertical < -0.1f) return 3; // 下 -> 右下
                    break;
                case 3: // 右下
                    if (horizontal < -0.1f) return 4; // 左 -> 左下
                    if (vertical > 0.1f) return 2; // 上 -> 右上
                    break;
                case 4: // 左下
                    if (horizontal > 0.1f) return 1; // 右 -> 中心
                    if (vertical > 0.1f) return 0; // 上 -> 左上
                    break;
            }
            
            return currentIndex; // 没有有效移动，保持当前
        }
        
        void HandleTouchInput()
        {
            if (!touchSupported) return;
            
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                
                switch (touch.phase)
                {
                    case TouchPhase.Began:
                        lastTouchPosition = touch.position;
                        HandleTouchHover(touch.position);
                        break;
                        
                    case TouchPhase.Moved:
                        HandleTouchHover(touch.position);
                        break;
                        
                    case TouchPhase.Ended:
                        if (Vector2.Distance(touch.position, lastTouchPosition) < 50f)
                        {
                            HandleTouchClick(touch.position);
                        }
                        break;
                }
            }
        }
        
        void HandleTouchHover(Vector2 screenPosition)
        {
            Ray ray = menuCamera.ScreenPointToRay(screenPosition);
            RaycastHit hit;
            
            int hoveredIndex = -1;
            
            if (Physics.Raycast(ray, out hit, raycastDistance, blockLayerMask))
            {
                hoveredIndex = GetBlockIndexFromHit(hit);
            }
            
            if (hoveredIndex != menuManager.GetCurrentHoveredIndex())
            {
                OnHoverChanged(hoveredIndex);
            }
        }
        
        void HandleTouchClick(Vector2 screenPosition)
        {
            Ray ray = menuCamera.ScreenPointToRay(screenPosition);
            RaycastHit hit;
            
            if (Physics.Raycast(ray, out hit, raycastDistance, blockLayerMask))
            {
                int clickedIndex = GetBlockIndexFromHit(hit);
                if (clickedIndex >= 0)
                {
                    OnBlockClicked(clickedIndex);
                }
            }
        }
        
        int GetBlockIndexFromHit(RaycastHit hit)
        {
            // 通过碰撞体获取色块索引
            FluidColorBlock colorBlock = hit.collider.GetComponent<FluidColorBlock>();
            if (colorBlock != null)
            {
                return colorBlock.GetBlockIndex();
            }
            
            // 或者通过名称匹配
            string colliderName = hit.collider.name;
            if (colliderName.Contains("Block"))
            {
                // 假设命名格式为 "Block0", "Block1", etc.
                string indexStr = colliderName.Replace("Block", "");
                if (int.TryParse(indexStr, out int index))
                {
                    return index;
                }
            }
            
            return -1;
        }
        
        bool ShouldProcessKeyboardInput(int blockIndex)
        {
            // 防止键盘输入重复
            if (blockIndex == lastKeyboardInput && 
                Time.time - lastKeyboardInputTime < keyboardRepeatDelay)
            {
                return false;
            }
            
            return true;
        }
        
        void OnHoverChanged(int newHoveredIndex)
        {
            // 这里可以添加悬停变化的音效或其他反馈
            Debug.Log($"悬停变化: {newHoveredIndex}");
        }
        
        void OnBlockClicked(int blockIndex)
        {
            // 这里可以添加点击音效或其他反馈
            Debug.Log($"色块被点击: {blockIndex}");
        }
        
        // UI事件接口实现
        public void OnPointerEnter(PointerEventData eventData)
        {
            isMouseOverUI = true;
        }
        
        public void OnPointerExit(PointerEventData eventData)
        {
            isMouseOverUI = false;
        }
        
        public void OnPointerClick(PointerEventData eventData)
        {
            // 处理UI点击事件
            if (eventData.pointerCurrentRaycast.gameObject != null)
            {
                FluidColorBlock colorBlock = eventData.pointerCurrentRaycast.gameObject.GetComponent<FluidColorBlock>();
                if (colorBlock != null)
                {
                    OnBlockClicked(colorBlock.GetBlockIndex());
                }
            }
        }
        
        // 公共接口
        public void SetMenuManager(FluidMenuManager manager)
        {
            menuManager = manager;
        }
        
        public void SetMenuCamera(Camera camera)
        {
            menuCamera = camera;
        }
        
        public bool IsMouseOverUI()
        {
            return isMouseOverUI;
        }
        
        public bool IsTouchSupported()
        {
            return touchSupported;
        }
        
        public Vector2 GetLastTouchPosition()
        {
            return lastTouchPosition;
        }
    }
}