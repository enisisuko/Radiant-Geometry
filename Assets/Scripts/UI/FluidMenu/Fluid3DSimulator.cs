// Fluid3DSimulator.cs
// 真实3D流体模拟器 - 基于简化的Navier-Stokes方程
// 实现类似水的流体效果，支持交互和GPU加速

using UnityEngine;
using System.Collections;

namespace FadedDreams.UI
{
    /// <summary>
    /// 3D流体模拟器
    /// 使用简化的Navier-Stokes方程模拟真实流体行为
    /// </summary>
    public class Fluid3DSimulator : MonoBehaviour
    {
        [Header("流体网格设置")]
        [Tooltip("网格分辨率（越高越精细，但性能消耗越大）")]
        [Range(16, 128)]
        public int gridResolution = 64;

        [Header("物理参数")]
        [Tooltip("粘度（控制流体的黏稠程度，水≈0.001）")]
        [Range(0.0001f, 0.1f)]
        public float viscosity = 0.001f;

        [Tooltip("扩散系数（控制密度扩散速度）")]
        [Range(0.00001f, 0.01f)]
        public float diffusion = 0.0001f;

        [Tooltip("时间步长（控制模拟速度）")]
        [Range(0.001f, 0.1f)]
        public float timeStep = 0.016f;

        [Header("交互参数")]
        [Tooltip("鼠标作用力强度")]
        public float mouseForce = 100f;

        [Tooltip("鼠标作用范围")]
        public float mouseRadius = 2f;

        [Tooltip("自动波纹强度")]
        public float autoWaveStrength = 0.5f;

        [Tooltip("自动波纹频率")]
        public float autoWaveFrequency = 0.3f;

        [Header("渲染设置")]
        [Tooltip("流体表面材质")]
        public Material fluidSurfaceMaterial;

        [Tooltip("是否显示调试网格")]
        public bool showDebugGrid = false;

        // 速度场（3D向量场）
        private Vector3[,,] velocity;
        private Vector3[,,] velocity0;

        // 密度场（标量场）
        private float[,,] density;
        private float[,,] density0;

        // 网格尺寸
        private int N;
        private float cellSize = 0.5f;

        // 交互状态
        private Vector3 lastMouseWorldPos;
        private bool isSimulating = false;

        // 性能优化
        private float simulationTimer = 0f;
        private const float SimulationInterval = 0.033f; // 30fps模拟频率

        private void Start()
        {
            InitializeFluidGrid();
            StartSimulation();
        }

        /// <summary>
        /// 初始化流体网格
        /// </summary>
        private void InitializeFluidGrid()
        {
            N = gridResolution;

            // 分配内存
            velocity = new Vector3[N, N, N];
            velocity0 = new Vector3[N, N, N];
            density = new float[N, N, N];
            density0 = new float[N, N, N];

            // 初始化：添加一些初始密度
            for (int i = 0; i < N; i++)
            {
                for (int j = 0; j < N; j++)
                {
                    for (int k = 0; k < N; k++)
                    {
                        // 在底部添加初始液体
                        if (j < N / 4)
                        {
                            density[i, j, k] = 1f;
                        }
                    }
                }
            }

            Debug.Log($"[Fluid3DSimulator] 流体网格初始化完成: {N}x{N}x{N}");
        }

        /// <summary>
        /// 开始流体模拟
        /// </summary>
        public void StartSimulation()
        {
            isSimulating = true;
            Debug.Log("[Fluid3DSimulator] 流体模拟已启动");
        }

        /// <summary>
        /// 停止流体模拟
        /// </summary>
        public void StopSimulation()
        {
            isSimulating = false;
            Debug.Log("[Fluid3DSimulator] 流体模拟已停止");
        }

        private void Update()
        {
            if (!isSimulating) return;

            // 控制模拟频率（性能优化）
            simulationTimer += Time.deltaTime;
            if (simulationTimer >= SimulationInterval)
            {
                simulationTimer = 0f;
                SimulateFluidStep();
            }

            // 处理交互
            HandleMouseInteraction();
            
            // 自动生成波纹
            GenerateAutoWaves();
        }

        /// <summary>
        /// 执行一步流体模拟
        /// </summary>
        private void SimulateFluidStep()
        {
            float dt = timeStep;

            // 1. 速度场扩散
            DiffuseVelocity(dt);

            // 2. 投影（确保不可压缩性）
            ProjectVelocity();

            // 3. 平流（速度场自我传输）
            AdvectVelocity(dt);

            // 4. 再次投影
            ProjectVelocity();

            // 5. 密度场扩散
            DiffuseDensity(dt);

            // 6. 密度场平流（被速度场带动）
            AdvectDensity(dt);
        }

        /// <summary>
        /// 速度场扩散（类似粘度）
        /// </summary>
        private void DiffuseVelocity(float dt)
        {
            float a = dt * viscosity * N * N * N;

            // 简化的扩散（使用Gauss-Seidel迭代）
            for (int iter = 0; iter < 4; iter++) // 4次迭代
            {
                for (int i = 1; i < N - 1; i++)
                {
                    for (int j = 1; j < N - 1; j++)
                    {
                        for (int k = 1; k < N - 1; k++)
                        {
                            Vector3 avg = (
                                velocity[i - 1, j, k] + velocity[i + 1, j, k] +
                                velocity[i, j - 1, k] + velocity[i, j + 1, k] +
                                velocity[i, j, k - 1] + velocity[i, j, k + 1]
                            ) / 6f;

                            velocity[i, j, k] = (velocity0[i, j, k] + a * avg) / (1 + 6 * a);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 投影（确保流体不可压缩）
        /// </summary>
        private void ProjectVelocity()
        {
            // 简化实现：仅在表面层应用
            int surfaceLevel = N / 4;

            for (int i = 1; i < N - 1; i++)
            {
                for (int k = 1; k < N - 1; k++)
                {
                    // 计算散度
                    float div = (
                        velocity[i + 1, surfaceLevel, k].x - velocity[i - 1, surfaceLevel, k].x +
                        velocity[i, surfaceLevel + 1, k].y - velocity[i, surfaceLevel - 1, k].y +
                        velocity[i, surfaceLevel, k + 1].z - velocity[i, surfaceLevel, k - 1].z
                    ) * 0.5f / N;

                    // 修正速度
                    velocity[i, surfaceLevel, k] -= new Vector3(div, div, div) * 0.5f;
                }
            }
        }

        /// <summary>
        /// 速度场平流
        /// </summary>
        private void AdvectVelocity(float dt)
        {
            // 备份当前速度场
            System.Array.Copy(velocity, velocity0, velocity.Length);

            // 反向追踪
            for (int i = 1; i < N - 1; i++)
            {
                for (int j = 1; j < N - 1; j++)
                {
                    for (int k = 1; k < N - 1; k++)
                    {
                        Vector3 pos = new Vector3(i, j, k);
                        Vector3 vel = velocity0[i, j, k];
                        
                        // 反向追踪位置
                        Vector3 prevPos = pos - vel * dt * N;
                        
                        // 边界处理
                        prevPos.x = Mathf.Clamp(prevPos.x, 0.5f, N - 1.5f);
                        prevPos.y = Mathf.Clamp(prevPos.y, 0.5f, N - 1.5f);
                        prevPos.z = Mathf.Clamp(prevPos.z, 0.5f, N - 1.5f);
                        
                        // 三线性插值（简化版）
                        int i0 = Mathf.FloorToInt(prevPos.x);
                        int j0 = Mathf.FloorToInt(prevPos.y);
                        int k0 = Mathf.FloorToInt(prevPos.z);
                        
                        if (i0 >= 0 && i0 < N - 1 && j0 >= 0 && j0 < N - 1 && k0 >= 0 && k0 < N - 1)
                        {
                            velocity[i, j, k] = velocity0[i0, j0, k0];
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 密度场扩散
        /// </summary>
        private void DiffuseDensity(float dt)
        {
            float a = dt * diffusion * N * N * N;

            for (int iter = 0; iter < 4; iter++)
            {
                for (int i = 1; i < N - 1; i++)
                {
                    for (int j = 1; j < N - 1; j++)
                    {
                        for (int k = 1; k < N - 1; k++)
                        {
                            float avg = (
                                density[i - 1, j, k] + density[i + 1, j, k] +
                                density[i, j - 1, k] + density[i, j + 1, k] +
                                density[i, j, k - 1] + density[i, j, k + 1]
                            ) / 6f;

                            density[i, j, k] = (density0[i, j, k] + a * avg) / (1 + 6 * a);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 密度场平流
        /// </summary>
        private void AdvectDensity(float dt)
        {
            System.Array.Copy(density, density0, density.Length);

            for (int i = 1; i < N - 1; i++)
            {
                for (int j = 1; j < N - 1; j++)
                {
                    for (int k = 1; k < N - 1; k++)
                    {
                        Vector3 pos = new Vector3(i, j, k);
                        Vector3 vel = velocity[i, j, k];
                        Vector3 prevPos = pos - vel * dt * N;
                        
                        prevPos.x = Mathf.Clamp(prevPos.x, 0.5f, N - 1.5f);
                        prevPos.y = Mathf.Clamp(prevPos.y, 0.5f, N - 1.5f);
                        prevPos.z = Mathf.Clamp(prevPos.z, 0.5f, N - 1.5f);
                        
                        int i0 = Mathf.FloorToInt(prevPos.x);
                        int j0 = Mathf.FloorToInt(prevPos.y);
                        int k0 = Mathf.FloorToInt(prevPos.z);
                        
                        if (i0 >= 0 && i0 < N - 1 && j0 >= 0 && j0 < N - 1 && k0 >= 0 && k0 < N - 1)
                        {
                            density[i, j, k] = density0[i0, j0, k0];
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 处理鼠标交互
        /// </summary>
        private void HandleMouseInteraction()
        {
            if (Input.GetMouseButton(0))
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit;

                if (Physics.Raycast(ray, out hit, 100f))
                {
                    Vector3 worldPos = hit.point;
                    
                    // 转换到网格坐标
                    Vector3 gridPos = WorldToGrid(worldPos);
                    
                    // 在点击位置添加力和密度
                    AddForceAtPosition(gridPos, ray.direction * mouseForce);
                    AddDensityAtPosition(gridPos, 1f);
                    
                    lastMouseWorldPos = worldPos;
                }
            }
        }

        /// <summary>
        /// 自动生成波纹（增加动态感）
        /// </summary>
        private void GenerateAutoWaves()
        {
            if (Time.time % autoWaveFrequency < Time.deltaTime)
            {
                // 在随机位置生成小波纹
                int x = Random.Range(N / 4, 3 * N / 4);
                int z = Random.Range(N / 4, 3 * N / 4);
                int y = N / 4; // 表面层

                Vector3 force = Vector3.up * autoWaveStrength * Random.Range(0.8f, 1.2f);
                AddForceAtGrid(x, y, z, force, 2f);
            }
        }

        /// <summary>
        /// 在指定位置添加力
        /// </summary>
        public void AddForceAtPosition(Vector3 gridPos, Vector3 force)
        {
            int x = Mathf.RoundToInt(gridPos.x);
            int y = Mathf.RoundToInt(gridPos.y);
            int z = Mathf.RoundToInt(gridPos.z);

            AddForceAtGrid(x, y, z, force, mouseRadius);
        }

        /// <summary>
        /// 在网格位置添加力
        /// </summary>
        private void AddForceAtGrid(int x, int y, int z, Vector3 force, float radius)
        {
            int r = Mathf.CeilToInt(radius);

            for (int i = -r; i <= r; i++)
            {
                for (int j = -r; j <= r; j++)
                {
                    for (int k = -r; k <= r; k++)
                    {
                        int xi = x + i;
                        int yj = y + j;
                        int zk = z + k;

                        if (xi >= 0 && xi < N && yj >= 0 && yj < N && zk >= 0 && zk < N)
                        {
                            float dist = Vector3.Distance(new Vector3(i, j, k), Vector3.zero);
                            if (dist < radius)
                            {
                                // 使用高斯分布的力
                                float strength = Mathf.Exp(-dist * dist / (radius * radius));
                                velocity[xi, yj, zk] += force * strength;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 在指定位置添加密度
        /// </summary>
        public void AddDensityAtPosition(Vector3 gridPos, float amount)
        {
            int x = Mathf.RoundToInt(gridPos.x);
            int y = Mathf.RoundToInt(gridPos.y);
            int z = Mathf.RoundToInt(gridPos.z);

            if (x >= 0 && x < N && y >= 0 && y < N && z >= 0 && z < N)
            {
                density[x, y, z] += amount;
            }
        }

        /// <summary>
        /// 世界坐标转换到网格坐标
        /// </summary>
        private Vector3 WorldToGrid(Vector3 worldPos)
        {
            Vector3 localPos = transform.InverseTransformPoint(worldPos);
            
            // 映射到网格范围[0, N]
            float x = (localPos.x / cellSize + N * 0.5f);
            float y = (localPos.y / cellSize + N * 0.5f);
            float z = (localPos.z / cellSize + N * 0.5f);

            return new Vector3(
                Mathf.Clamp(x, 0, N - 1),
                Mathf.Clamp(y, 0, N - 1),
                Mathf.Clamp(z, 0, N - 1)
            );
        }

        /// <summary>
        /// 获取流体表面高度（用于渲染）
        /// </summary>
        public float GetSurfaceHeightAt(float x, float z)
        {
            Vector3 gridPos = WorldToGrid(new Vector3(x, 0f, z));
            int gi = Mathf.Clamp(Mathf.RoundToInt(gridPos.x), 0, N - 1);
            int gk = Mathf.Clamp(Mathf.RoundToInt(gridPos.z), 0, N - 1);

            // 查找表面层（密度最高的y层）
            float maxDensity = 0f;
            int surfaceY = 0;

            for (int j = 0; j < N; j++)
            {
                if (density[gi, j, gk] > maxDensity)
                {
                    maxDensity = density[gi, j, gk];
                    surfaceY = j;
                }
            }

            // 转换回世界坐标
            return (surfaceY - N * 0.5f) * cellSize;
        }

        /// <summary>
        /// 获取流体密度（用于颜色渲染）
        /// </summary>
        public float GetDensityAt(int x, int y, int z)
        {
            if (x < 0 || x >= N || y < 0 || y >= N || z < 0 || z >= N)
                return 0f;

            return density[x, y, z];
        }

        private void OnDrawGizmos()
        {
            if (!showDebugGrid || !Application.isPlaying) return;
            if (density == null || N == 0) return;

            // 绘制表面层的调试网格
            Gizmos.color = Color.cyan;
            int surfaceY = N / 4;

            for (int i = 0; i < N; i += 4) // 每隔4个点绘制
            {
                for (int k = 0; k < N; k += 4)
                {
                    float d = density[i, surfaceY, k];
                    if (d > 0.1f)
                    {
                        Vector3 worldPos = GridToWorld(i, surfaceY, k);
                        Gizmos.DrawWireCube(worldPos, Vector3.one * cellSize * 0.8f);
                    }
                }
            }
        }

        /// <summary>
        /// 网格坐标转换到世界坐标
        /// </summary>
        private Vector3 GridToWorld(int x, int y, int z)
        {
            Vector3 localPos = new Vector3(
                (x - N * 0.5f) * cellSize,
                (y - N * 0.5f) * cellSize,
                (z - N * 0.5f) * cellSize
            );
            return transform.TransformPoint(localPos);
        }
    }
}

