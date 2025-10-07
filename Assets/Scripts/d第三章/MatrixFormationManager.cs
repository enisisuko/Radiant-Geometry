// 📋 代码总览: 请先阅读 Assets/Scripts/CODE_OVERVIEW.md 了解完整项目结构
// 🚀 开发指南: 参考 Assets/Scripts/DEVELOPMENT_GUIDE.md 进行开发

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using FD.Bosses.C3;
using UnityEngine.Rendering.Universal;

namespace FadedDreams.Bosses
{
    /// <summary>
    /// 三圈七层大阵管理器 - 实现华丽的大阵系统
    /// </summary>
    public class MatrixFormationManager : MonoBehaviour
    {
        [Header("Matrix Configuration")]
        [SerializeField] private float baseRadius = 8f;
        [SerializeField] private float[] layerRadii = { 1f, 1.4f, 1.8f, 2.2f, 2.6f }; // R1-R5 比例
        [SerializeField] private int motherCount = 6;
        [SerializeField] private int petalsPerMother = 5;
        [SerializeField] private int starsPerFlower = 8;
        [SerializeField] private int outerMarkerCount = 60;
        
        [Header("Rhythm System")]
        [SerializeField] private float beatDuration = 0.5f; // 每拍持续时间
        [SerializeField] private int totalBeats = 12; // 12拍循环
        
        [Header("Visual Effects")]
        [SerializeField] private GameObject motherPrefab;
        [SerializeField] private GameObject petalPrefab;
        [SerializeField] private GameObject starPrefab;
        [SerializeField] private GameObject arcPrefab;
        [SerializeField] private GameObject markerPrefab;
        [SerializeField] private GameObject groundGlyphPrefab;
        [SerializeField] private MatrixVisualEffects visualEffects;
        
        [Header("Colors")]
        [SerializeField] private Color redColor = new Color(1f, 0.25f, 0.25f, 1f);
        [SerializeField] private Color greenColor = new Color(0.25f, 1f, 0.25f, 1f);
        [SerializeField] private Color goldColor = new Color(1f, 0.84f, 0f, 1f);
        [SerializeField] private Color whiteColor = new Color(1f, 1f, 1f, 1f);
        
        // 大阵层级结构
        private Transform matrixRoot;
        private List<Transform> mothers = new List<Transform>();
        private List<List<Transform>> petals = new List<List<Transform>>();
        private List<List<Transform>> stars = new List<List<Transform>>();
        private List<Transform> arcs = new List<Transform>();
        private List<Transform> outerMarkers = new List<Transform>();
        private Transform groundGlyph;
        
        // 节拍系统
        private int currentBeat = 0;
        private float beatTimer = 0f;
        private bool isActive = false;
        
        // 相位和动画
        private float globalPhase = 0f;
        private float[] motherPhases;
        private float[] petalPhases;
        private float[] starPhases;
        
        // 颜色状态
        private BossColor currentBossColor = BossColor.Red;
        private FadedDreams.Player.ColorMode currentPlayerMode = FadedDreams.Player.ColorMode.Red;
        
        // 事件
        public System.Action<int> OnBeatChanged;
        public System.Action OnMatrixComplete;
        public System.Action OnMatrixReset;
        
        private void Awake()
        {
            // 初始化视觉效果系统
            if (visualEffects == null)
            {
                visualEffects = gameObject.AddComponent<MatrixVisualEffects>();
            }
            
            InitializeMatrix();
        }
        
        private void Start()
        {
            // 确保大阵跟随BOSS位置
            UpdateMatrixPosition();
        }
        
        private void Update()
        {
            if (isActive)
            {
                UpdateRhythm();
                UpdateMatrixAnimation();
                UpdateMatrixPosition(); // 持续更新大阵位置
            }
        }
        
        /// <summary>
        /// 更新大阵位置，使其跟随BOSS
        /// </summary>
        private void UpdateMatrixPosition()
        {
            if (matrixRoot != null)
            {
                // 大阵根节点跟随BOSS位置
                matrixRoot.position = transform.position;
                
                // 确保所有层级都正确跟随
                UpdateAllLayerPositions();
            }
        }
        
        /// <summary>
        /// 更新所有层级的位置，确保它们相对于BOSS正确定位
        /// </summary>
        private void UpdateAllLayerPositions()
        {
            // 更新母体位置（使用本地位置）
            for (int i = 0; i < mothers.Count; i++)
            {
                if (mothers[i] == null) continue;
                
                float angle = motherPhases[i] * Mathf.Deg2Rad;
                Vector3 pos = new Vector3(
                    Mathf.Cos(angle) * baseRadius * layerRadii[0],
                    Mathf.Sin(angle) * baseRadius * layerRadii[0],
                    0f
                );
                mothers[i].localPosition = pos;
            }
            
            // 更新花瓣位置（使用本地位置）
            for (int i = 0; i < petals.Count; i++)
            {
                for (int j = 0; j < petals[i].Count; j++)
                {
                    if (petals[i][j] == null) continue;
                    
                    int index = i * petalsPerMother + j;
                    float angle = petalPhases[index] * Mathf.Deg2Rad;
                    Vector3 pos = new Vector3(
                        Mathf.Cos(angle) * baseRadius * layerRadii[1],
                        Mathf.Sin(angle) * baseRadius * layerRadii[1],
                        0f
                    );
                    petals[i][j].localPosition = pos;
                }
            }
            
            // 更新星曜位置
            for (int i = 0; i < stars.Count; i++)
            {
                for (int j = 0; j < stars[i].Count; j++)
                {
                    if (stars[i][j] == null) continue;
                    
                    int index = i * starsPerFlower + j;
                    float angle = starPhases[index] * Mathf.Deg2Rad;
                    float breathRadius = layerRadii[2] + 0.03f * Mathf.Sin(Time.time * 3f + index * 0.3f);
                    Vector3 pos = new Vector3(
                        Mathf.Cos(angle) * baseRadius * breathRadius,
                        Mathf.Sin(angle) * baseRadius * breathRadius,
                        0f
                    );
                    stars[i][j].localPosition = pos;
                }
            }
            
            // 更新外轮刻位置
            for (int i = 0; i < outerMarkers.Count; i++)
            {
                if (outerMarkers[i] == null) continue;
                
                float angle = (360f / outerMarkerCount) * i;
                Vector3 pos = new Vector3(
                    Mathf.Cos(angle * Mathf.Deg2Rad) * baseRadius * layerRadii[4],
                    Mathf.Sin(angle * Mathf.Deg2Rad) * baseRadius * layerRadii[4],
                    0f
                );
                outerMarkers[i].localPosition = pos;
            }
            
            // 更新地纹位置
            if (groundGlyph != null)
            {
                groundGlyph.localPosition = Vector3.zero;
            }
        }
        
        /// <summary>
        /// 初始化大阵结构
        /// </summary>
        private void InitializeMatrix()
        {
            // 创建大阵根节点
            if (matrixRoot == null)
            {
                matrixRoot = new GameObject("MatrixFormation").transform;
                matrixRoot.SetParent(transform);
            }
            
            // 初始化相位数组
            motherPhases = new float[motherCount];
            petalPhases = new float[motherCount * petalsPerMother];
            starPhases = new float[motherCount * starsPerFlower];
            
            // 创建各层级
            CreateMothers();
            CreatePetals();
            CreateStars();
            CreateArcs();
            CreateOuterMarkers();
            CreateGroundGlyph();
        }
        
        /// <summary>
        /// 创建母体轨道单元（Layer A）
        /// </summary>
        private void CreateMothers()
        {
            for (int i = 0; i < motherCount; i++)
            {
                GameObject mother = motherPrefab ? Instantiate(motherPrefab) : CreateDefaultMother();
                mother.name = $"Mother_{i}";
                mother.transform.SetParent(matrixRoot);
                mothers.Add(mother.transform);
                
                // 设置初始位置
                float angle = (360f / motherCount) * i;
                Vector3 pos = new Vector3(
                    Mathf.Cos(angle * Mathf.Deg2Rad) * baseRadius * layerRadii[0],
                    Mathf.Sin(angle * Mathf.Deg2Rad) * baseRadius * layerRadii[0],
                    0f
                );
                mother.transform.position = pos;
                
                // 初始化相位
                motherPhases[i] = angle;
            }
        }
        
        /// <summary>
        /// 创建花瓣阵（Layer B）
        /// </summary>
        private void CreatePetals()
        {
            for (int i = 0; i < motherCount; i++)
            {
                var petalGroup = new List<Transform>();
                Transform mother = mothers[i];
                
                for (int j = 0; j < petalsPerMother; j++)
                {
                    GameObject petal = petalPrefab ? Instantiate(petalPrefab) : CreateDefaultPetal();
                    petal.name = $"Petal_{i}_{j}";
                    petal.transform.SetParent(matrixRoot);
                    petalGroup.Add(petal.transform);
                    
                    // 黄金分割角度
                    float petalAngle = motherPhases[i] + j * (360f / petalsPerMother);
                    Vector3 pos = new Vector3(
                        Mathf.Cos(petalAngle * Mathf.Deg2Rad) * baseRadius * layerRadii[1],
                        Mathf.Sin(petalAngle * Mathf.Deg2Rad) * baseRadius * layerRadii[1],
                        0f
                    );
                    petal.transform.localPosition = pos;
                    
                    // 初始化相位
                    petalPhases[i * petalsPerMother + j] = petalAngle;
                }
                
                petals.Add(petalGroup);
            }
        }
        
        /// <summary>
        /// 创建星曜阵（Layer C）
        /// </summary>
        private void CreateStars()
        {
            for (int i = 0; i < motherCount; i++)
            {
                var starGroup = new List<Transform>();
                Transform mother = mothers[i];
                
                for (int j = 0; j < starsPerFlower; j++)
                {
                    GameObject star = starPrefab ? Instantiate(starPrefab) : CreateDefaultStar();
                    star.name = $"Star_{i}_{j}";
                    star.transform.SetParent(matrixRoot);
                    starGroup.Add(star.transform);
                    
                    // 星曜角度
                    float starAngle = motherPhases[i] + j * (360f / starsPerFlower);
                    Vector3 pos = new Vector3(
                        Mathf.Cos(starAngle * Mathf.Deg2Rad) * baseRadius * layerRadii[2],
                        Mathf.Sin(starAngle * Mathf.Deg2Rad) * baseRadius * layerRadii[2],
                        0f
                    );
                    star.transform.position = pos;
                    
                    // 初始化相位
                    starPhases[i * starsPerFlower + j] = starAngle;
                }
                
                stars.Add(starGroup);
            }
        }
        
        /// <summary>
        /// 创建拱弧阵（Layer D）
        /// </summary>
        private void CreateArcs()
        {
            for (int i = 0; i < motherCount; i++)
            {
                GameObject arc = arcPrefab ? Instantiate(arcPrefab) : CreateDefaultArc();
                arc.name = $"Arc_{i}";
                arc.transform.SetParent(matrixRoot);
                arcs.Add(arc.transform);
                
                // 连接相邻母体
                int nextIndex = (i + 1) % motherCount;
                Vector3 startPos = mothers[i].position;
                Vector3 endPos = mothers[nextIndex].position;
                
                // 创建贝塞尔弧线
                CreateBezierArc(arc.transform, startPos, endPos, baseRadius * layerRadii[3]);
            }
        }
        
        /// <summary>
        /// 创建外轮刻（Layer E）
        /// </summary>
        private void CreateOuterMarkers()
        {
            for (int i = 0; i < outerMarkerCount; i++)
            {
                GameObject marker = markerPrefab ? Instantiate(markerPrefab) : CreateDefaultMarker();
                marker.name = $"Marker_{i}";
                marker.transform.SetParent(matrixRoot);
                outerMarkers.Add(marker.transform);
                
                // 60个刻度均匀分布
                float angle = (360f / outerMarkerCount) * i;
                Vector3 pos = new Vector3(
                    Mathf.Cos(angle * Mathf.Deg2Rad) * baseRadius * layerRadii[4],
                    Mathf.Sin(angle * Mathf.Deg2Rad) * baseRadius * layerRadii[4],
                    0f
                );
                marker.transform.position = pos;
            }
        }
        
        /// <summary>
        /// 创建地纹（Ground Glyph）
        /// </summary>
        private void CreateGroundGlyph()
        {
            GameObject glyph = groundGlyphPrefab ? Instantiate(groundGlyphPrefab) : CreateDefaultGroundGlyph();
            glyph.name = "GroundGlyph";
            glyph.transform.SetParent(matrixRoot);
            glyph.transform.position = transform.position;
            groundGlyph = glyph.transform;
        }
        
        /// <summary>
        /// 更新节拍系统
        /// </summary>
        private void UpdateRhythm()
        {
            beatTimer += Time.deltaTime;
            
            if (beatTimer >= beatDuration)
            {
                beatTimer = 0f;
                currentBeat = (currentBeat + 1) % totalBeats;
                OnBeatChanged?.Invoke(currentBeat);
                
                // 处理特殊节拍
                HandleSpecialBeats();
            }
        }
        
        /// <summary>
        /// 处理特殊节拍事件
        /// </summary>
        private void HandleSpecialBeats()
        {
            switch (currentBeat)
            {
                case 0: // 拍1-2：聚气
                    HandleGatheringPhase();
                    break;
                case 2: // 拍3：锁扣
                    HandleLockPhase();
                    break;
                case 3: // 拍4-5：花开
                case 4:
                    HandleFlowerPhase();
                    break;
                case 5: // 拍6：齐鸣
                    HandleResonancePhase();
                    break;
                case 6: // 拍7-8：螺旋
                case 7:
                    HandleSpiralPhase();
                    break;
                case 8: // 拍9：再锁扣
                    HandleSecondLockPhase();
                    break;
                case 9: // 拍10-11：回波
                case 10:
                    HandleEchoPhase();
                    break;
                case 11: // 拍12：谢幕/重置
                    HandleFinalPhase();
                    break;
            }
        }
        
        /// <summary>
        /// 更新大阵动画
        /// </summary>
        private void UpdateMatrixAnimation()
        {
            globalPhase += Time.deltaTime;
            
            // 更新母体轨道
            UpdateMotherOrbits();
            
            // 更新花瓣动画
            UpdatePetalAnimation();
            
            // 更新星曜动画
            UpdateStarAnimation();
            
            // 更新拱弧动画
            UpdateArcAnimation();
            
            // 更新外轮刻动画
            UpdateMarkerAnimation();
            
            // 更新地纹动画
            UpdateGroundGlyphAnimation();
        }
        
        /// <summary>
        /// 更新母体轨道
        /// </summary>
        private void UpdateMotherOrbits()
        {
            for (int i = 0; i < mothers.Count; i++)
            {
                if (mothers[i] == null) continue;
                
                // 基础公转
                motherPhases[i] += Time.deltaTime * 30f; // 30度/秒
                
                // 锁扣拍顿挫
                if (IsLockBeat())
                {
                    motherPhases[i] -= Time.deltaTime * 6f; // 减少20%速度
                }
                
                // 位置更新现在由UpdateAllLayerPositions()处理
                // 这里只更新颜色
                UpdateMotherColor(mothers[i], i);
            }
        }
        
        /// <summary>
        /// 更新花瓣动画
        /// </summary>
        private void UpdatePetalAnimation()
        {
            for (int i = 0; i < petals.Count; i++)
            {
                for (int j = 0; j < petals[i].Count; j++)
                {
                    if (petals[i][j] == null) continue;
                    
                    int index = i * petalsPerMother + j;
                    petalPhases[index] += Time.deltaTime * 20f;
                    
                    // 花瓣伸缩
                    float breathScale = 1f + 0.05f * Mathf.Sin(Time.time * 2f + index * 0.5f);
                    petals[i][j].localScale = Vector3.one * breathScale;
                    
                    // 位置更新现在由UpdateAllLayerPositions()处理
                    // 这里只更新颜色
                    UpdatePetalColor(petals[i][j], i, j);
                }
            }
        }
        
        /// <summary>
        /// 更新星曜动画
        /// </summary>
        private void UpdateStarAnimation()
        {
            for (int i = 0; i < stars.Count; i++)
            {
                for (int j = 0; j < stars[i].Count; j++)
                {
                    if (stars[i][j] == null) continue;
                    
                    int index = i * starsPerFlower + j;
                    starPhases[index] += Time.deltaTime * 40f; // 公转
                    
                    // 自旋
                    stars[i][j].Rotate(0, 0, Time.deltaTime * 60f);
                    
                    // 位置更新现在由UpdateAllLayerPositions()处理
                    // 这里只更新颜色
                    UpdateStarColor(stars[i][j], i, j);
                }
            }
        }
        
        /// <summary>
        /// 更新拱弧动画
        /// </summary>
        private void UpdateArcAnimation()
        {
            for (int i = 0; i < arcs.Count; i++)
            {
                if (arcs[i] == null) continue;
                
                // 锁扣拍高亮
                if (IsLockBeat())
                {
                    UpdateArcBrightness(arcs[i], 1f);
                }
                else
                {
                    UpdateArcBrightness(arcs[i], 0.7f);
                }
            }
        }
        
        /// <summary>
        /// 更新外轮刻动画
        /// </summary>
        private void UpdateMarkerAnimation()
        {
            for (int i = 0; i < outerMarkers.Count; i++)
            {
                if (outerMarkers[i] == null) continue;
                
                // 按节拍闪烁
                bool shouldGlow = (i % (outerMarkers.Count / totalBeats)) == currentBeat;
                UpdateMarkerGlow(outerMarkers[i], shouldGlow);
            }
        }
        
        /// <summary>
        /// 更新地纹动画
        /// </summary>
        private void UpdateGroundGlyphAnimation()
        {
            if (groundGlyph == null) return;
            
            // 根据节拍调整亮度
            float intensity = 0.5f + 0.3f * Mathf.Sin(Time.time * 2f);
            UpdateGroundGlyphIntensity(groundGlyph, intensity);
        }
        
        // 特殊节拍处理方法
        private void HandleGatheringPhase()
        {
            // 内环母体亮度与体积缓升
            foreach (var mother in mothers)
            {
                if (mother != null)
                {
                    UpdateMotherBrightness(mother, 0.8f + 0.2f * Mathf.Sin(Time.time * 2f));
                }
            }
        }
        
        private void HandleLockPhase()
        {
            // 拱弧阵高亮，形成6叶光伞
            foreach (var arc in arcs)
            {
                if (arc != null)
                {
                    UpdateArcBrightness(arc, 1f);
                }
            }
            
            // 异色花瓣预警
            HighlightDangerousPetals();
        }
        
        private void HandleFlowerPhase()
        {
            // 花瓣依次伸展
            for (int i = 0; i < petals.Count; i++)
            {
                for (int j = 0; j < petals[i].Count; j++)
                {
                    if (petals[i][j] != null)
                    {
                        float delay = j * 0.1f;
                        StartCoroutine(AnimatePetalBloom(petals[i][j], delay));
                    }
                }
            }
        }
        
        private void HandleResonancePhase()
        {
            // 所有母体同时放光
            foreach (var mother in mothers)
            {
                if (mother != null)
                {
                    StartCoroutine(AnimateMotherPulse(mother));
                }
            }
            
            // 发射攻击
            FireMatrixAttacks();
        }
        
        private void HandleSpiralPhase()
        {
            // 全阵相位缓旋
            globalPhase += Time.deltaTime * 30f;
        }
        
        private void HandleSecondLockPhase()
        {
            // 外轮刻强闪
            foreach (var marker in outerMarkers)
            {
                if (marker != null)
                {
                    StartCoroutine(AnimateMarkerFlash(marker));
                }
            }
        }
        
        private void HandleEchoPhase()
        {
            // 地纹切线流
            UpdateGroundGlyphFlow(groundGlyph, true);
        }
        
        private void HandleFinalPhase()
        {
            // 外轮刻整圈走光
            StartCoroutine(AnimateFullCircleGlow());
            
            // 复制体再生
            if (currentBeat == 11)
            {
                SpawnClones();
            }
        }
        
        // 辅助方法
        private bool IsLockBeat()
        {
            return currentBeat == 2 || currentBeat == 5 || currentBeat == 8 || currentBeat == 11;
        }
        
        private void HighlightDangerousPetals()
        {
            // 高亮与玩家异色的花瓣
            for (int i = 0; i < petals.Count; i++)
            {
                for (int j = 0; j < petals[i].Count; j++)
                {
                    if (petals[i][j] != null)
                    {
                        bool isDangerous = IsPetalDangerous(i, j);
                        UpdatePetalDanger(petals[i][j], isDangerous);
                    }
                }
            }
        }
        
        private bool IsPetalDangerous(int motherIndex, int petalIndex)
        {
            // 判断花瓣是否与玩家当前模式相反
            return (currentBossColor == BossColor.Red && currentPlayerMode == FadedDreams.Player.ColorMode.Green) ||
                   (currentBossColor == BossColor.Green && currentPlayerMode == FadedDreams.Player.ColorMode.Red);
        }
        
        private void FireMatrixAttacks()
        {
            // 在第6拍发射攻击
            if (currentBeat == 5)
            {
                // 母体发射震爆弹
                foreach (var mother in mothers)
                {
                    if (mother != null)
                    {
                        FireShockwaveFromMother(mother);
                    }
                }
                
                // 花瓣发射散弹
                foreach (var petalGroup in petals)
                {
                    foreach (var petal in petalGroup)
                    {
                        if (petal != null)
                        {
                            FireBulletFromPetal(petal);
                        }
                    }
                }
            }
        }
        
        private void SpawnClones()
        {
            // 在第12拍生成复制体
            foreach (var mother in mothers)
            {
                if (mother != null)
                {
                    SpawnClonesAroundMother(mother);
                }
            }
        }
        
        // 创建默认预制体的方法（2D游戏适配，全部使用母体材质）
        private GameObject CreateDefaultMother()
        {
            GameObject mother = new GameObject("Mother");
            // 添加2D Sprite Renderer
            SpriteRenderer sr = mother.AddComponent<SpriteRenderer>();
            sr.sprite = CreateCircleSprite();
            sr.color = redColor;
            mother.transform.localScale = Vector3.one * 0.5f;
            
            // 添加2D光源
            Light2D light = mother.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.intensity = 1f;
            light.color = redColor;
            light.pointLightInnerRadius = 0.5f;
            light.pointLightOuterRadius = 2f;
            
            return mother;
        }
        
        private GameObject CreateDefaultPetal()
        {
            GameObject petal = new GameObject("Petal");
            // 添加2D Sprite Renderer，使用母体材质
            SpriteRenderer sr = petal.AddComponent<SpriteRenderer>();
            sr.sprite = CreatePetalSprite();
            sr.color = greenColor;
            petal.transform.localScale = new Vector3(0.2f, 0.1f, 1f);
            
            // 添加2D光源
            Light2D light = petal.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.intensity = 0.8f;
            light.color = greenColor;
            light.pointLightInnerRadius = 0.2f;
            light.pointLightOuterRadius = 1f;
            
            return petal;
        }
        
        private GameObject CreateDefaultStar()
        {
            GameObject star = new GameObject("Star");
            // 添加2D Sprite Renderer，使用母体材质
            SpriteRenderer sr = star.AddComponent<SpriteRenderer>();
            sr.sprite = CreateStarSprite();
            sr.color = whiteColor;
            star.transform.localScale = Vector3.one * 0.1f;
            
            // 添加2D光源
            Light2D light = star.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.intensity = 0.6f;
            light.color = whiteColor;
            light.pointLightInnerRadius = 0.1f;
            light.pointLightOuterRadius = 0.8f;
            
            return star;
        }
        
        private GameObject CreateDefaultArc()
        {
            GameObject arc = new GameObject("Arc");
            LineRenderer lr = arc.AddComponent<LineRenderer>();
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.material.color = goldColor;
            lr.startWidth = 0.1f;
            lr.endWidth = 0.1f;
            lr.sortingOrder = 1; // 2D排序
            return arc;
        }
        
        private GameObject CreateDefaultMarker()
        {
            GameObject marker = new GameObject("Marker");
            // 添加2D Sprite Renderer，使用母体材质
            SpriteRenderer sr = marker.AddComponent<SpriteRenderer>();
            sr.sprite = CreateCircleSprite();
            sr.color = goldColor;
            marker.transform.localScale = Vector3.one * 0.05f;
            
            // 添加2D光源
            Light2D light = marker.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.intensity = 0.5f;
            light.color = goldColor;
            light.pointLightInnerRadius = 0.05f;
            light.pointLightOuterRadius = 0.5f;
            
            return marker;
        }
        
        private GameObject CreateDefaultGroundGlyph()
        {
            GameObject glyph = new GameObject("GroundGlyph");
            // 添加2D Sprite Renderer，使用母体材质
            SpriteRenderer sr = glyph.AddComponent<SpriteRenderer>();
            sr.sprite = CreateGridSprite();
            sr.color = goldColor * 0.3f;
            sr.sortingOrder = -1; // 在地面层
            
            return glyph;
        }
        
        // 创建2D Sprite的辅助方法
        private Sprite CreateCircleSprite()
        {
            // 创建圆形纹理
            int size = 64;
            Texture2D texture = new Texture2D(size, size);
            Color[] pixels = new Color[size * size];
            
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float radius = size * 0.4f;
            
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 pos = new Vector2(x, y);
                    float distance = Vector2.Distance(pos, center);
                    float alpha = 1f - Mathf.Clamp01((distance - radius) / (size * 0.1f));
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            
            texture.SetPixels(pixels);
            texture.Apply();
            
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }
        
        private Sprite CreatePetalSprite()
        {
            // 创建花瓣纹理
            int size = 32;
            Texture2D texture = new Texture2D(size, size);
            Color[] pixels = new Color[size * size];
            
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 pos = new Vector2(x, y);
                    Vector2 dir = (pos - center).normalized;
                    
                    // 花瓣形状：椭圆形
                    float ellipse = Mathf.Pow((x - center.x) / (size * 0.4f), 2) + Mathf.Pow((y - center.y) / (size * 0.2f), 2);
                    float alpha = ellipse <= 1f ? 1f - ellipse : 0f;
                    
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            
            texture.SetPixels(pixels);
            texture.Apply();
            
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }
        
        private Sprite CreateStarSprite()
        {
            // 创建星形纹理
            int size = 16;
            Texture2D texture = new Texture2D(size, size);
            Color[] pixels = new Color[size * size];
            
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float radius = size * 0.4f;
            
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 pos = new Vector2(x, y);
                    float distance = Vector2.Distance(pos, center);
                    float alpha = 1f - Mathf.Clamp01((distance - radius) / (size * 0.1f));
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            
            texture.SetPixels(pixels);
            texture.Apply();
            
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }
        
        private Sprite CreateGridSprite()
        {
            // 创建网格纹理
            int size = 256;
            Texture2D texture = new Texture2D(size, size);
            Color[] pixels = new Color[size * size];
            
            int gridSize = 16;
            
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool isGridLine = (x % gridSize == 0) || (y % gridSize == 0);
                    float alpha = isGridLine ? 0.3f : 0f;
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            
            texture.SetPixels(pixels);
            texture.Apply();
            
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }
        
        // 贝塞尔弧线创建
        private void CreateBezierArc(Transform arc, Vector3 start, Vector3 end, float radius)
        {
            LineRenderer lr = arc.GetComponent<LineRenderer>();
            if (lr == null) return;
            
            Vector3 mid = (start + end) * 0.5f;
            Vector3 control = mid + Vector3.up * radius;
            
            int segments = 20;
            lr.positionCount = segments + 1;
            
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                Vector3 pos = BezierCurve(start, control, end, t);
                lr.SetPosition(i, pos);
            }
        }
        
        private Vector3 BezierCurve(Vector3 p0, Vector3 p1, Vector3 p2, float t)
        {
            float u = 1 - t;
            return u * u * p0 + 2 * u * t * p1 + t * t * p2;
        }
        
        // 动画协程
        private IEnumerator AnimatePetalBloom(Transform petal, float delay)
        {
            yield return new WaitForSeconds(delay);
            
            Vector3 originalScale = petal.localScale;
            Vector3 targetScale = originalScale * 1.2f;
            
            float duration = 0.3f;
            float elapsed = 0f;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                petal.localScale = Vector3.Lerp(originalScale, targetScale, t);
                yield return null;
            }
            
            // 回流
            elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                petal.localScale = Vector3.Lerp(targetScale, originalScale, t);
                yield return null;
            }
        }
        
        private IEnumerator AnimateMotherPulse(Transform mother)
        {
            // 母体脉冲动画
            yield return new WaitForSeconds(0.1f);
        }
        
        private IEnumerator AnimateMarkerFlash(Transform marker)
        {
            // 标记闪烁动画
            yield return new WaitForSeconds(0.1f);
        }
        
        private IEnumerator AnimateFullCircleGlow()
        {
            // 整圈走光动画
            yield return new WaitForSeconds(0.5f);
        }
        
        // 颜色更新方法（使用视觉效果系统）
        private void UpdateMotherColor(Transform mother, int index) 
        {
            if (visualEffects != null)
            {
                visualEffects.UpdateMotherColor(mother, index, currentBossColor);
            }
        }
        
        private void UpdatePetalColor(Transform petal, int motherIndex, int petalIndex) 
        {
            if (visualEffects != null)
            {
                visualEffects.UpdatePetalColor(petal, motherIndex, petalIndex, currentBossColor, currentPlayerMode);
            }
        }
        
        private void UpdateStarColor(Transform star, int motherIndex, int starIndex) 
        {
            if (visualEffects != null)
            {
                visualEffects.UpdateStarColor(star, motherIndex, starIndex);
            }
        }
        
        private void UpdateArcBrightness(Transform arc, float brightness) 
        {
            if (visualEffects != null)
            {
                visualEffects.UpdateArcBrightness(arc, brightness);
            }
        }
        
        private void UpdateMarkerGlow(Transform marker, bool glow) 
        {
            if (visualEffects != null)
            {
                visualEffects.UpdateMarkerGlow(marker, glow);
            }
        }
        
        private void UpdateGroundGlyphIntensity(Transform glyph, float intensity) 
        {
            if (visualEffects != null)
            {
                visualEffects.UpdateGroundGlyphIntensity(glyph, intensity);
            }
        }
        
        private void UpdateGroundGlyphFlow(Transform glyph, bool tangentFlow) 
        {
            if (visualEffects != null)
            {
                visualEffects.UpdateGroundGlyphFlow(glyph, tangentFlow);
            }
        }
        
        private void UpdateMotherBrightness(Transform mother, float brightness) 
        {
            if (visualEffects != null)
            {
                visualEffects.UpdateMotherBrightness(mother, brightness);
            }
        }
        
        private void UpdatePetalDanger(Transform petal, bool dangerous) 
        {
            if (visualEffects != null)
            {
                visualEffects.UpdatePetalDanger(petal, dangerous);
            }
        }
        
        // 攻击方法（连接到现有的攻击系统）
        private void FireShockwaveFromMother(Transform mother) 
        {
            if (mother == null) return;
            
            // 获取玩家
            var player = FindObjectOfType<FadedDreams.Player.PlayerController2D>();
            if (player == null) return;
            
            // 计算方向
            Vector3 direction = (player.transform.position - mother.position).normalized;
            
            // 使用能量扣除系统
            var pcm = player.GetComponent<FadedDreams.Player.PlayerColorModeController>();
            if (pcm != null)
            {
                pcm.SpendEnergy(pcm.Mode, 20f); // 震爆弹扣除20能量
            }
            else
            {
                // 备用：使用IDamageable接口
                var damageable = player.GetComponent<FadedDreams.Enemies.IDamageable>();
                if (damageable != null)
                {
                    damageable.TakeDamage(20f);
                }
            }
        }
        
        private void FireBulletFromPetal(Transform petal) 
        {
            if (petal == null) return;
            
            // 获取玩家
            var player = FindObjectOfType<FadedDreams.Player.PlayerController2D>();
            if (player == null) return;
            
            // 计算方向
            Vector3 direction = (player.transform.position - petal.position).normalized;
            
            // 使用能量扣除系统
            var pcm = player.GetComponent<FadedDreams.Player.PlayerColorModeController>();
            if (pcm != null)
            {
                pcm.SpendEnergy(pcm.Mode, 5f); // 子弹扣除5能量
            }
            else
            {
                // 备用：使用IDamageable接口
                var damageable = player.GetComponent<FadedDreams.Enemies.IDamageable>();
                if (damageable != null)
                {
                    damageable.TakeDamage(5f);
                }
            }
        }
        
        private void SpawnClonesAroundMother(Transform mother) 
        {
            if (mother == null) return;
            
            // 生成复制体围绕母体
            int cloneCount = 3;
            for (int i = 0; i < cloneCount; i++)
            {
                GameObject clone = Instantiate(mother.gameObject, mother.position, mother.rotation, matrixRoot);
                clone.name = $"Clone_{mother.name}_{i}";
                
                // 设置复制体位置（围绕母体）
                float angle = (360f / cloneCount) * i;
                float radius = 2f;
                Vector3 offset = new Vector3(
                    Mathf.Cos(angle * Mathf.Deg2Rad) * radius,
                    Mathf.Sin(angle * Mathf.Deg2Rad) * radius,
                    0f
                );
                clone.transform.position = mother.position + offset;
                
                // 设置复制体为攻击模式
                var cloneAgent = clone.GetComponent<BossC3_AllInOne.OrbAgent>();
                if (cloneAgent != null)
                {
                    cloneAgent.SetBumperMode(true, true, currentBossColor);
                }
            }
        }
        
        // 公共接口
        public void StartMatrix()
        {
            isActive = true;
            currentBeat = 0;
            beatTimer = 0f;
        }
        
        public void StopMatrix()
        {
            isActive = false;
            OnMatrixReset?.Invoke();
        }
        
        public void SetBossColor(BossColor color)
        {
            currentBossColor = color;
        }
        
        public void SetPlayerMode(FadedDreams.Player.ColorMode mode)
        {
            currentPlayerMode = mode;
        }
        
        public int GetCurrentBeat() => currentBeat;
        public bool IsMatrixActive() => isActive;
    }
}
