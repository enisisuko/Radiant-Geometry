using UnityEngine;

namespace FadedDreams.UI
{
    /// <summary>
    /// 流体场管理器
    /// 负责管理流体场的存储、访问和边界条件
    /// </summary>
    public class FluidFieldManager : MonoBehaviour
    {
        [Header("网格设置")]
        public int gridWidth = 128;
        public int gridHeight = 128;
        public float cellSize = 1.0f;
        
        [Header("流体状态")]
        public float minDensity = 0.01f;
        public float maxDensity = 1.0f;
        public float fluidThreshold = 0.1f;
        
        // 流体场数据
        private float[,] densityField;
        private Vector2[,] velocityField;
        private float[,] pressureField;
        private bool[,] solidField; // 固体边界
        
        // 临时数组用于计算
        private float[,] tempDensity;
        private Vector2[,] tempVelocity;
        private float[,] tempPressure;
        
        void Start()
        {
            InitializeFluidField();
        }
        
        void InitializeFluidField()
        {
            // 初始化数组
            densityField = new float[gridWidth, gridHeight];
            velocityField = new Vector2[gridWidth, gridHeight];
            pressureField = new float[gridWidth, gridHeight];
            solidField = new bool[gridWidth, gridHeight];
            
            tempDensity = new float[gridWidth, gridHeight];
            tempVelocity = new Vector2[gridWidth, gridHeight];
            tempPressure = new float[gridWidth, gridHeight];
            
            // 初始化流体区域
            int centerX = gridWidth / 2;
            int centerY = gridHeight / 2;
            int radius = 25; // 更大的初始半径
            
            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    // 设置边界为固体
                    if (x == 0 || x == gridWidth - 1 || y == 0 || y == gridHeight - 1)
                    {
                        solidField[x, y] = true;
                        densityField[x, y] = 0f;
                    }
                    else
                    {
                        solidField[x, y] = false;
                        
                        // 初始化中心区域的流体
                        float distance = Vector2.Distance(new Vector2(x, y), new Vector2(centerX, centerY));
                        if (distance <= radius)
                        {
                            float normalizedDistance = distance / radius;
                            densityField[x, y] = maxDensity * (1.0f - normalizedDistance * normalizedDistance);
                        }
                        else
                        {
                            densityField[x, y] = 0f;
                        }
                    }
                    
                    velocityField[x, y] = Vector2.zero;
                    pressureField[x, y] = 0f;
                }
            }
        }
        
        // 密度场访问方法
        public float GetDensity(int x, int y)
        {
            if (x >= 0 && x < gridWidth && y >= 0 && y < gridHeight)
                return densityField[x, y];
            return 0f;
        }
        
        public void SetDensity(int x, int y, float density)
        {
            if (x >= 0 && x < gridWidth && y >= 0 && y < gridHeight)
                densityField[x, y] = Mathf.Clamp(density, minDensity, maxDensity);
        }
        
        public void SetTempDensity(int x, int y, float density)
        {
            if (x >= 0 && x < gridWidth && y >= 0 && y < gridHeight)
                tempDensity[x, y] = density;
        }
        
        public void SwapDensityFields()
        {
            var temp = densityField;
            densityField = tempDensity;
            tempDensity = temp;
        }
        
        // 速度场访问方法
        public Vector2 GetVelocity(int x, int y)
        {
            if (x >= 0 && x < gridWidth && y >= 0 && y < gridHeight)
                return velocityField[x, y];
            return Vector2.zero;
        }
        
        public void SetVelocity(int x, int y, Vector2 velocity)
        {
            if (x >= 0 && x < gridWidth && y >= 0 && y < gridHeight)
                velocityField[x, y] = velocity;
        }
        
        public void SetTempVelocity(int x, int y, Vector2 velocity)
        {
            if (x >= 0 && x < gridWidth && y >= 0 && y < gridHeight)
                tempVelocity[x, y] = velocity;
        }
        
        public void SwapVelocityFields()
        {
            var temp = velocityField;
            velocityField = tempVelocity;
            tempVelocity = temp;
        }
        
        // 压力场访问方法
        public float GetPressure(int x, int y)
        {
            if (x >= 0 && x < gridWidth && y >= 0 && y < gridHeight)
                return pressureField[x, y];
            return 0f;
        }
        
        public void SetPressure(int x, int y, float pressure)
        {
            if (x >= 0 && x < gridWidth && y >= 0 && y < gridHeight)
                pressureField[x, y] = pressure;
        }
        
        // 固体场访问方法
        public bool IsSolid(int x, int y)
        {
            if (x >= 0 && x < gridWidth && y >= 0 && y < gridHeight)
                return solidField[x, y];
            return true; // 边界外视为固体
        }
        
        public void SetSolid(int x, int y, bool isSolid)
        {
            if (x >= 0 && x < gridWidth && y >= 0 && y < gridHeight)
                solidField[x, y] = isSolid;
        }
        
        // 网格信息访问方法
        public int GetGridWidth()
        {
            return gridWidth;
        }
        
        public int GetGridHeight()
        {
            return gridHeight;
        }
        
        public float GetCellSize()
        {
            return cellSize;
        }
        
        // 世界坐标转换
        public Vector2Int WorldToGrid(Vector2 worldPos)
        {
            int x = Mathf.RoundToInt(worldPos.x * gridWidth);
            int y = Mathf.RoundToInt(worldPos.y * gridHeight);
            return new Vector2Int(x, y);
        }
        
        public Vector2 GridToWorld(Vector2Int gridPos)
        {
            float x = (float)gridPos.x / gridWidth;
            float y = (float)gridPos.y / gridHeight;
            return new Vector2(x, y);
        }
        
        // 流体操作
        public void AddFluid(Vector2 position, float amount)
        {
            Vector2Int gridPos = WorldToGrid(position);
            int x = gridPos.x;
            int y = gridPos.y;
            
            if (x >= 0 && x < gridWidth && y >= 0 && y < gridHeight && !solidField[x, y])
            {
                densityField[x, y] = Mathf.Min(densityField[x, y] + amount, maxDensity);
            }
        }
        
        public void AddForce(Vector2 position, Vector2 force, float timeStep)
        {
            Vector2Int gridPos = WorldToGrid(position);
            int x = gridPos.x;
            int y = gridPos.y;
            
            if (x >= 0 && x < gridWidth && y >= 0 && y < gridHeight && !solidField[x, y])
            {
                velocityField[x, y] += force * timeStep;
            }
        }
        
        public float GetDensityAt(Vector2 worldPos)
        {
            Vector2Int gridPos = WorldToGrid(worldPos);
            return GetDensity(gridPos.x, gridPos.y);
        }
        
        public Vector2 GetVelocityAt(Vector2 worldPos)
        {
            Vector2Int gridPos = WorldToGrid(worldPos);
            return GetVelocity(gridPos.x, gridPos.y);
        }
        
        public float GetPressureAt(Vector2 worldPos)
        {
            Vector2Int gridPos = WorldToGrid(worldPos);
            return GetPressure(gridPos.x, gridPos.y);
        }
        
        // 重置方法
        public void ResetFields()
        {
            InitializeFluidField();
        }
        
        public void ClearFluid()
        {
            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    if (!solidField[x, y])
                    {
                        densityField[x, y] = 0f;
                        velocityField[x, y] = Vector2.zero;
                        pressureField[x, y] = 0f;
                    }
                }
            }
        }
        
        // 获取场数据用于渲染
        public float[,] GetDensityField()
        {
            return densityField;
        }
        
        public Vector2[,] GetVelocityField()
        {
            return velocityField;
        }
        
        public float[,] GetPressureField()
        {
            return pressureField;
        }
        
        public bool[,] GetSolidField()
        {
            return solidField;
        }
        
        // 设置网格大小
        public void SetGridSize(int width, int height)
        {
            gridWidth = width;
            gridHeight = height;
            InitializeFluidField();
        }
        
        // 设置流体参数
        public void SetFluidParameters(float minDens, float maxDens, float threshold)
        {
            minDensity = minDens;
            maxDensity = maxDens;
            fluidThreshold = threshold;
        }
    }
}
