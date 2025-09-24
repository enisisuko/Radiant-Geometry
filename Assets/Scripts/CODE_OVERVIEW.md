核心模块（Scripts/Core）
GameManager.cs

功能概述： 全局单例管理器，维护游戏全局状态，如当前章节和检查点等。继承自泛型单例基类Singleton<GameManager>，确保全局只有一个实例。

类说明： GameManager，继承自Singleton<GameManager>和MonoBehaviour。作为单例，可以通过GameManager.Instance访问。

字段/属性：

public int CurrentChapter { get; private set; } = 1; —— 记录当前章节号（默认1），私有设置。

public string CurrentCheckpointId { get; private set; } = ""; —— 记录当前检查点ID（字符串）。

主要方法：

public void SetChapter(int chapter) —— 设置当前章节号，将输入值限制在1到4之间。对外接口，用于更改章节状态。

public void SetCheckpoint(string checkpointId) —— 设置当前检查点ID，并调用SaveSystem.Instance.SaveCheckpoint(checkpointId)将其保存到存档。对外接口，在玩家到达新检查点时调用。

public void OnPlayerDeath() —— 玩家死亡时调用的接口。它调用SceneLoader.ReloadAtLastCheckpoint()重载到上一次保存的场景和检查点，实现复活功能。

生命周期/事件： 此脚本没有使用Start()、Update()等Unity生命周期方法，主要通过其他脚本调用其公有方法。

对外接口： 公开的方法SetChapter、SetCheckpoint、OnPlayerDeath即构成对外接口，供其他脚本（如存档系统、检查点等）调用。

调用关系：

在SetCheckpoint中，调用了存档系统SaveSystem来保存检查点。

在OnPlayerDeath中，调用了场景加载器SceneLoader的ReloadAtLastCheckpoint方法。

其他模块（如存档系统）也可能读取或更新GameManager.CurrentChapter。

ChapterManager.cs

功能概述： 静态工具类，根据当前场景名称判断所属章节号等。

类说明： ChapterManager，继承MonoBehaviour（但实际上仅包含静态方法）。

字段/属性： 无字段，仅静态方法。

主要方法：

public static int ChapterFromScene() —— 静态方法，通过SceneManager.GetActiveScene().name获取当前场景名称（转为小写），检查名称中是否包含“chapter1”、“chapter2”、“chapter3”、“chapter4”，返回对应的章节号（1-4）。若均不匹配，则默认返回1。对外静态接口，便于其他脚本根据场景判断当前章节。

生命周期/事件： 无。

对外接口： 静态方法ChapterFromScene。

调用关系： 可能被菜单控制器、场景加载逻辑、UI等调用，以确定当前章节编号。

Checkpoint.cs

功能概述： 场景内检查点组件，附加在带触发器（Collider2D）的物体上。当玩家进入触发器时，记录检查点并可使玩家从此位置复活。

类说明： Checkpoint，继承自MonoBehaviour。使用[RequireComponent(typeof(Collider2D))]确保附着物体有2D碰撞器。

字段/属性：

[SerializeField] private string idOverride; —— （私有）可选的检查点ID覆盖值；为空时使用GameObject.name作为ID。

public bool activateOnStart; —— 如果为true，进入场景时会自动激活此检查点（默认为目前场景第一个检查点）。

public string Id { get; } —— 只读属性：如果idOverride非空，则返回idOverride，否则返回当前物体名称。用于统一获取检查点ID。

主要方法/Unity事件：

void Start() —— 如果activateOnStart为真，则在游戏开始时将玩家位置移动到此检查点，并通过GameManager.Instance.SetCheckpoint(Id)和SaveSystem.Instance.AddDiscoveredCheckpoint(...)记录此检查点。这样就相当于玩家一进入场景就自动置于此点。

private void OnTriggerEnter2D(Collider2D other) —— Unity碰撞触发方法。当有其他2D碰撞体进入触发器时调用。

内部逻辑：检测进入物体是否有PlayerController2D组件（即玩家）；如果是，首先将玩家位置移动到此检查点（transform.position），并调用SaveSystem.Instance.AddDiscoveredCheckpoint(sceneName, Id)将当前场景名和检查点ID存入存档系统。此时玩家从该检查点重生。还可能触发自动对话阅读等扩展逻辑（注释掉的示例）。

事件/协程： 该脚本使用了OnTriggerEnter2D，无需协程或额外事件系统。

对外接口： Id属性（只读）对外提供检查点ID。主要由场景起始逻辑或玩家碰撞触发调用内部方法。

调用关系：

调用了存档系统（SaveSystem.Instance.AddDiscoveredCheckpoint）记录已达检查点。

调用了GameManager（SetCheckpoint）更新当前检查点ID。

（隐式）控制玩家位置，使玩家“复活”在此点。

SaveData.cs

功能概述： 存档数据结构，使用[Serializable]标记，可通过JSON序列化写入磁盘。

类说明： SaveData为一个数据类，不继承MonoBehaviour。

字段：

public string lastScene; —— 最后保存的场景名称。

public string lastCheckpoint; —— 最后保存的检查点ID。

public int highestChapterUnlocked = 1; —— 已解锁的最高章节号，初始为1。

public HashSet<string> discoveredCheckpoints = new HashSet<string>(); —— 已发现（触发过）的所有检查点ID（带场景名前缀或唯一ID），用于“继续游戏”菜单列出选项。

用途： 由SaveSystem序列化读写，保存游戏进度。

SaveSystem.cs

功能概述： 全局存档管理单例，负责将SaveData读写为JSON并保存在磁盘上（路径为Application.persistentDataPath/faded_dreams_save.json）。继承Singleton<SaveSystem>。

类说明： SaveSystem : Singleton<SaveSystem>。在Awake()中初始化并加载已有存档或创建默认存档。

字段/属性：

private string FilePath（只读属性）—— 存档文件完整路径。

private SaveData data = new SaveData(); —— 当前持有的存档数据。

主要方法： 皆为公共接口，供游戏逻辑调用：

public void SaveLastScene(string sceneName) —— 将场景名存入data.lastScene并调用内部Save()写盘。

public string LoadLastScene() —— 返回data.lastScene，即上次保存的场景名。

public void SaveCheckpoint(string checkpointId) —— 将检查点ID存入data.lastCheckpoint并写盘。

public string LoadCheckpoint() —— 返回data.lastCheckpoint，即上次保存的检查点ID。

public void AddDiscoveredCheckpoint(string sceneName, string checkpointId) —— 当玩家触发新检查点时调用。将场景名和检查点ID组合后加入data.discoveredCheckpoints集合，并更新data.lastScene和data.lastCheckpoint，然后保存。这用于“继续游戏”菜单生成可选列表。

public IEnumerable<string> GetCheckpoints() —— 返回已发现的所有检查点（集合），供生成“继续游戏”列表使用。

public void UnlockChapter(int chapter) —— 更新data.highestChapterUnlocked为chapter（如果更大）。并写盘。

public int HighestChapterUnlocked() —— 返回当前存档中最高解锁的章节号。

public void ResetAll() —— 重置存档数据（删除所有保存进度），通常用于游戏一开始或测试时清档。

内部方法：

private void Save() —— 将data通过JsonUtility.ToJson序列化为字符串并写入FilePath文件。

private void Load() —— 如果存档文件存在，从文件读取JSON并用JsonUtility.FromJsonOverwrite加载到data。

生命周期/事件： 在Awake()阶段调用base.Awake()初始化单例后，执行Load()加载已有存档。

对外接口： 上述所有public方法（存储/读取场景、检查点、章节、列表等）。

调用关系：

被检查点和场景加载器调用更新场景及检查点。

“继续游戏”菜单调用GetCheckpoints()、HighestChapterUnlocked()等生成选项。

GameManager.SetCheckpoint内部也调用了此类的SaveCheckpoint。

SceneLoader.cs

功能概述： 场景加载静态工具类。提供加载新场景或从上一次检查点重载的功能。

类说明： SceneLoader定义为static类，不继承MonoBehaviour，只包含静态方法。

主要方法（静态）：

public static void LoadScene(string sceneName, string checkpointId = "") —— 加载指定场景。实现步骤：

调用SaveSystem.Instance.SaveLastScene(sceneName)和SaveSystem.Instance.SaveCheckpoint(checkpointId)来记录当前要去的场景和检查点（为空则表示场景开始）。

调用SceneManager.LoadScene(sceneName)切换场景。

若checkpointId不空，在场景加载完成后（因为场景内容已实例化），可能会有逻辑读取上次检查点并复位玩家位置（通常由Checkpoint组件的Start()在场景启动时触发自动定位）。

public static void ReloadAtLastCheckpoint() —— 从保存的最后场景与检查点重载玩家：

从SaveSystem中读取lastScene和lastCheckpoint。

如果lastScene为空，则使用SceneManager.GetActiveScene().name作为默认场景（即当前场景）。

调用LoadScene(lastScene, lastCheckpoint)进行场景加载和检查点设置。

对外接口： 公开的静态方法LoadScene和ReloadAtLastCheckpoint。

调用关系：

GameManager.OnPlayerDeath()调用了ReloadAtLastCheckpoint()来重生玩家。

其它部分（如“开始新游戏”按钮）可能调用LoadScene("ChapterX", "")来开始新局。

玩家控制脚本（Scripts/Player）
PlayerController2D.cs

功能概述： 2D平台游戏中玩家的核心控制器，处理移动、跳跃、冲刺（Dash）、飞行（Jetpack）等动作。还包括地面检测、加速下落等游戏机制。

类说明： PlayerController2D : MonoBehaviour。使用Rigidbody2D控制物理运动，监听输入实现各种动作。

字段（主要配置参数）：

public bool disableJumpAndDashInStory; —— 在剧情场景中禁用跳跃和冲刺（用于某些章节限制功能）。

public float moveSpeed, dashSpeed, dashDuration, dashCooldown, airDashDistanceMultiplier, airDashSpeedMultiplier; —— 分别表示水平移动速度、冲刺速度、冲刺持续时间、冲刺冷却时间，空中冲刺距离/速度系数等。

public float coyoteTime; —— 跳跃“缓冲时间”容忍值（即玩家离开地面后的一定短暂时间内仍可跳跃）。

public float jumpForce; —— 跳跃初速度。

public int maxJumps; —— 最大跳跃次数（支持二段跳等）。

public Transform groundCheck; public float groundRadius; public LayerMask groundMask; —— 地面检测相关：groundCheck是一个子对象，用来检测玩家脚底是否接触地面（使用射线或OverlapCircle），groundMask指定哪些层算作地面。

public UnityEvent onJump, onDoubleJump, onLanded, onDashStart, onDashEnd; —— 可在Inspector中绑定的事件：跳跃、二段跳、落地、冲刺开始、冲刺结束时触发。UI或特效脚本可监听这些事件。

public bool chapter3Dash; —— 特殊开关：如果开启，允许章节3中特殊的冲刺逻辑（如空中冲刺效果）。

public GameObject vfxGroundDash, vfxAirDash; public float vfxAutoDestroyDelay; —— 冲刺时生成的粒子特效预制体（地面冲刺和空中冲刺），以及特效自动销毁的延时。

私有字段（状态记录）：

private Rigidbody2D rb; —— 玩家刚体组件。

private int jumpsLeft; —— 剩余可用跳跃次数（初始化为maxJumps，落地时重置）。

private float coyoteCounter; —— 用于计算离开地面后的缓冲计时。

private bool isDashing, wasGrounded, canDash; —— 状态标志：isDashing表示当前是否正在冲刺，wasGrounded记录上一帧是否着地，用于检测落地事件，canDash标记是否允许冲刺（根据冷却重置）。

private float lastDashTime; —— 记录最后一次冲刺时间，用于实现短暂失去资格后自动恢复（如代码中“1秒内重置冲刺资格”）。

private float inputX; —— 玩家水平输入值。

private bool queuedJump; —— 跳跃排队标志：如果玩家按下跳跃键但当前不允许跳跃（如刚离地面），设置此标志在下一帧执行跳跃。

private FlashStrike flashStrike; —— 组件引用，可能用于打击特效（章节3新增）。

主要方法：

void Awake() —— 获得Rigidbody2D组件引用(rb = GetComponent<Rigidbody2D>())，初始化变量（如jumpsLeft = maxJumps），并可能缓存FlashStrike组件。

void Update() —— 处理玩家输入和状态检测：

读取水平输入inputX（如Input.GetAxis("Horizontal")）。

检查跳跃按键（如Space）：如果按下且符合条件（地面上或在“宽恕时间”内或有剩余跳跃次数），则设置queuedJump = true准备跳跃，同时触发相应的事件（onJump或onDoubleJump）。

检查冲刺按键（如左Shift）：如果按下并且canDash为真，则启动冲刺协程Dash(direction)。

如果玩家从地面起跳，则调用EnableFlight(false)结束飞行模式。

void FixedUpdate() —— 进行物理运动控制：

调用HandleGroundAndCooldown()检查玩家是否着地（使用groundCheck和OverlapCircle），更新coyoteCounter、jumpsLeft、canDash和wasGrounded等状态。

如果isDashing为假，则根据inputX设置rb.velocity.x = inputX * moveSpeed（水平匀速移动）；否则在冲刺中禁止正常控制。

实现额外下落加速度：如果rb.velocity.y < 0（下落），额外增加重力加速度，使下落更快。

IEnumerator Dash(float direction) —— 冲刺协程：

先设置isDashing = true、暂停重力（rb.gravityScale = 0），触发事件onDashStart，并生成对应地面/空中特效实例。

在冲刺持续时间内，将rb.velocity设为direction * dashSpeed水平冲刺速度并持续计时。

结束时恢复rb.gravityScale、触发事件onDashEnd，并设置isDashing = false。

记录lastDashTime = Time.time，并将canDash = false来开始冷却。

public void EnableFlight(bool enabled) —— 公共方法：开启或关闭飞行模式。如果开启，取消重力并允许玩家使用上下键控制rb.velocity在空中移动；如果关闭，则恢复重力。供其他脚本（如Jetpack或章节3逻辑）调用。

private void ForceEndDash() —— 强制结束冲刺（可被其他脚本调用，例如进入禁冲区域时）。恢复重力并中断冲刺状态。

private void HandleGroundAndCooldown() —— 私有方法，在FixedUpdate()调用：

检查是否接触地面；如果由非着地变为着地，则触发onLanded事件，并重置jumpsLeft = maxJumps、canDash = true。

距离离地后，使用coyoteCounter来允许短暂跳跃。

当冷却时间过去（Time.time - lastDashTime > dashCooldown），自动将canDash = true恢复冲刺资格。

其它辅助方法：SpawnDashVFX()创建VFX；OnDrawGizmosSelected()在编辑器中可视化地面检测范围；SetStoryRestriction(bool)根据剧情设置禁用跳跃冲刺。

Unity生命周期：

Awake()：初始化组件引用和状态。

Start()：可能订阅事件等（代码中可重写）。

Update()：处理输入、按键检测。

FixedUpdate()：处理物理运动、地面检测、跳跃执行及冲刺冷却。

OnDrawGizmosSelected()：用于编辑器，在选择该物体时绘制地面检测区域。

协程与事件： 使用了协程Dash()实现持续冲刺运动；使用了UnityEvent事件（onJump等）方便与其他系统（如音效、动画）关联。

对外接口： 公开方法EnableFlight(bool)和SetStoryRestriction(bool)（剧情冲刺开关）；UnityEvent事件对外可绑定。

脚本交互：

该脚本会通过事件或直接调用与AfterimageTrail2D和PlayerDashAfterimage合作，在冲刺时生成拖影效果。

与PlayerHealthLight、PlayerLightController等搭配，控制玩家状态和资源。

部分章节脚本（章节3特殊模式）可能通过EnableFlight、onDashStart事件来扩展行为。

AfterimageTrail2D.cs

功能概述： 2D残影特效组件，在玩家快速移动时生成“残影”图像。常用于冲刺或角色特效，使角色移动时留下渐隐复制。

类说明： AfterimageTrail2D属于VFX模块，继承自MonoBehaviour。要求附着到拥有SpriteRenderer组件的游戏物体上。

字段：

private SpriteRenderer spriteSource; —— 原始Sprite渲染器，用来生成残影。

public bool autoEmitDuringDash; —— 如果为true，在Dash期间自动启动持续发射残影（由PlayerDashAfterimage控制）。

public float emissionRate; —— 副本生成频率（秒/张）。

public float lifeTime; —— 残影存在时间。

内部字段：private bool _emitting; private float _emitTimer; private Coroutine _emitCo; —— 控制是否持续发射以及计时等。

主要方法：

public void BurstOnce() —— 立即生成一次残影：克隆当前spriteSource的图形，生成一个新的GameObject，并启动FadeAndKill协程使其渐隐。

public void BeginEmit() —— 开始持续发射：启动协程CoEmitLoop()，每隔emissionRate秒自动调用BurstOnce()；设置_emitting = true。

public void StopEmit() —— 停止持续发射：停止CoEmitLoop协程并设置_emitting = false。

public void BeginTrail() => BeginEmit(); public void EndTrail() => StopEmit(); —— 别名方法，便于外部调用时的语义统一。

协程：

IEnumerator CoEmitLoop() —— 循环协程：在BeginEmit()时启动，每隔emissionRate秒调用一次BurstOnce()，一直持续，直到StopEmit()停止。

IEnumerator CoBurst() —— 如果有一次性快速生成（代码中可能被调用）。

IEnumerator FadeAndKill(GameObject instance) —— 对生成的残影实例进行逐渐透明并销毁：根据lifeTime插值控制Alpha，直至完全透明后销毁物体。

作用机制： 利用复制Sprite渲染器生成静态图像，并让其慢慢淡出，创造“残影”效果。可以即时调用BurstOnce()做单张残影，也可BeginEmit()开启持续模式（如连续冲刺）。

对外接口： 公开方法BurstOnce()、BeginEmit()、StopEmit()（以及BeginTrail/EndTrail），供其他脚本（如PlayerDashAfterimage）触发。

脚本交互： 常与PlayerController2D或PlayerDashAfterimage配合：比如冲刺开始时调用BeginEmit()，冲刺结束时调用StopEmit()，或在Dash瞬间调用BurstOnce()。

PlayerDashAfterimage.cs

功能概述： 专用组件，结合玩家的冲刺行为自动调用AfterimageTrail2D以生成残影效果。

类说明： PlayerDashAfterimage : MonoBehaviour，要求同一物体上附加AfterimageTrail2D组件（通过[RequireComponent(typeof(AfterimageTrail2D))]）。

字段：

public bool autoEmitDuringDash; —— 如果为true，在Dash过程中自动调用_trail.BeginEmit()持续产生残影。

public float ghostHoldSeconds; —— 每次Dash时触发的BurstOnce()所“停留”时间（实际控制残影持续时间）。

私有：private AfterimageTrail2D _trail; —— 对同一物体上AfterimageTrail2D的引用。

主要方法：

private void Awake() —— 获取_trail = GetComponent<AfterimageTrail2D>()。

public void Trigger() —— 在冲刺开始时被调用的接口（例如由PlayerController2D事件调用）。方法体：调用_trail.BurstOnce()产生一次残影，然后根据autoEmitDuringDash决定是否调用_trail.BeginEmit()。（脚本中注释建议Dash过程中结束时需StopEmit()）。

public void BeginContinuous()/EndContinuous() —— 对应手动开启/关闭连续发射，调用_trail.BeginEmit()或StopEmit()。供其他脚本根据需要触发。

对外接口： Trigger()可在冲刺动画事件或PlayerController2D的onDashStart绑定时调用；BeginContinuous()/EndContinuous()用于手动控制持续发射。

调用关系： 通常由PlayerController2D的冲刺事件调用Trigger()，或者配合Input在Dash时自动发射残影。

PlayerEnergyHook.cs

功能概述： 玩家能量控制挂钩，用于外部暂停或恢复玩家能量消耗。通过反射访问PlayerLightController或其他组件上的能量属性，实现“快速充能”或暂停能量消耗功能。

类说明： PlayerEnergyHook : MonoBehaviour，挂在玩家上或其子对象上。

字段：

public Component energyOwner; —— 能量所在组件，优先获取Energy字段/属性；如果为空，则默认在当前对象查找。可以连接到PlayerLightController等组件。

私有反射字段：缓存目标组件中Energy、currentEnergy、maxEnergy等成员的FieldInfo或PropertyInfo，用于读写。

主要方法：

public void SetEnergyLossPaused(bool pause) —— 对外接口：当传入true时暂停能量损耗（开始“快速回充”模式）；当传入false时恢复正常消耗。实现上：根据pause计算当前能量比例并直接写回能源组件，或恢复原速率。

其他： 通过Unity编辑器绑定，可通过脚本或动画事件调用SetEnergyLossPaused，例如在特定区域进入时自动充能。

脚本交互： 依赖PlayerLightController（或其他组件）定义的能量字段（Energy或currentEnergy等），实际通过反射查找并设置其值。因此，它与玩家能量系统紧密结合。

PlayerHealthLight.cs

功能概述： 玩家生命/红光能量管理组件，处理玩家在红光模式下的生命损失与死亡。一般与玩家碰撞检测或受伤逻辑结合使用。

类说明： PlayerHealthLight : MonoBehaviour，附着在玩家物体上。

字段：

public float maxHealth; —— 最大生命值/红光能量值。

private float currentHealth; —— 当前生命值。

可能还有private RedLightController redLightCtrl;引用，用于对红光损失做反馈。

主要方法：

public void TakeDamage(float amount) —— 公共方法：减去指定amount的血量（红光能量）。如果减到0，则触发玩家死亡（例如调用GameManager.Instance.OnPlayerDeath()或销毁玩家）。

private void OnCollisionEnter2D(Collision2D other)或OnTriggerEnter2D(Collider2D other) —— 如果与敌人发生碰撞，调用TakeDamage扣血。具体实现可能根据红光模式决定扣血（例如只有在红光模式下碰撞敌人才扣血）。

对外接口： TakeDamage可被敌人攻击脚本调用以影响玩家血量。

脚本交互： 可能监听RedLightController的能量耗尽事件，或者与UI（RedLightHUD）交互，显示血量。若血量归零，将通过GameManager或场景重载器处理玩家死亡。

PlayerLightController.cs

功能概述： 玩家光能管理器，处理绿色光能（玩家资源）的消耗与恢复。接受外部事件影响（如采集、陷阱），并更新能量值。

类说明： PlayerLightController : MonoBehaviour。可能在玩家身上使用。

字段：

public float currentEnergy = 100f, maxEnergy = 100f, regenRate = 1f; —— 当前能量、最大能量和自然回复速率（每秒）。

public float drainRate = 0.5f; —— 消耗速度（如在红光模式下每秒消耗）。

可能的public UnityEvent onEnergyChanged;事件，用于能量变化时通知UI更新。

主要方法：

void Update() —— 常规更新：如果没有暂停消耗（SetEnergyLossPaused），根据状态持续消耗能量（如进入某状态时）。同时自然回复能量：currentEnergy = min(currentEnergy + regenRate * Time.deltaTime, maxEnergy)。

public void AddEnergy(float amount) —— 增加能量（例如拾取能量物品调用）。

public bool SpendEnergy(float amount) —— 尝试消耗指定能量，如果能量足够，减少并返回true，否则返回false。用于武器或技能消耗检查。

事件/接口： 如有onEnergyChanged事件，会在每次能量变化时触发，以便更新UI。

脚本交互： 其他脚本（如PlayerEnergyHook、能量补充装置）通过调用AddEnergy或修改currentEnergy影响此组件。此组件变化又会驱动UI（HUDController）显示。

PlayerLightVisual.cs

功能概述： 玩家光能的视觉表现组件，根据玩家当前光能状态改变角色光环、发光强度或UI元素。

类说明： PlayerLightVisual : MonoBehaviour，通常附加在玩家或光源对象上。

字段：

可能有public float minIntensity, maxIntensity;等，设置光源亮度范围。

引用到Light2D组件或SpriteRenderer以改变外观。

主要方法：

void Update() —— 每帧根据PlayerLightController.currentEnergy/maxEnergy计算百分比，然后插值调整自身光源/材质属性。例如：修改Light2D.intensity或SpriteRenderer的颜色亮度，使玩家变亮或变暗。

脚本交互： 监听PlayerLightController中的能量变化值（可通过直接读取或订阅事件），并更新视觉。

SquashStretch2D.cs

功能概述： 2D角色的挤压-拉伸动画效果组件，用于增加跳跃、落地等动作的动画表现。

类说明： SquashStretch2D : MonoBehaviour，附加在玩家角色模型上。

字段：

public float squashAmount, stretchAmount, animationTime; —— 定义跳跃和落地时缩放的量和动画持续时间。

private Vector3 originalScale; —— 初始缩放，用于复位。

方法：

public void TriggerJumpSquash() —— 调用时立即或逐渐拉长角色（如减少Y轴缩放，增加X轴），然后再恢复原状。

public void TriggerLandSquash() —— 落地时调用：短暂压扁角色（减少Y轴），再弹回。

使用方式： 可以在PlayerController2D的跳跃事件和落地事件（onJump、onLanded）中绑定这两个方法，分别在跳跃和落地时播放效果。也可以在动画状态机中设置动画事件调用。

RedLight/RedLightController.cs

功能概述： “红光模式”控制器，管理玩家进入红光模式时的能量消耗、可视效果以及模式切换。

类说明： RedLightController : MonoBehaviour，附加在玩家物体上或独立子对象。

字段：

public float maxRed = 100f; public float currentRed; —— 红光能量最大值和当前值。

public float drainRate = 10f; —— 在红光模式下，每秒消耗的红光值。

public UnityEvent onOutOfEnergy; —— 当红光能量耗尽时触发（可在编辑器绑定，比如播放死亡动画）。

可能有UI显示或影响其他系统的引用。

主要方法：

void Update() —— 如果当前处于红光模式（可能由PlayerColorModeController控制），则每帧减少currentRed：currentRed = max(currentRed - drainRate * Time.deltaTime, 0)。当currentRed降到0时，触发onOutOfEnergy事件，并可能调用玩家死亡或自动退出红光模式的逻辑。

public void TakeDamage(float amount) —— 如果被敌人攻击，直接减少currentRed。如减少到0可触发死亡。

对外接口： onOutOfEnergy事件通知其他系统。其他脚本（敌人、陷阱）通过调用TakeDamage影响玩家红光。

脚本交互： 与PlayerHealthLight结合：PlayerHealthLight可能调用RedLightController.TakeDamage。与UI（RedLightHUD）互动更新红光条。玩家脱离红光模式或重置时重置currentRed。

Player/Weapons/LaserEmitter.cs

功能概述： 激光发射器组件，玩家持有武器可发射激光束或电浆。用于触发可被镜面等反射的射线（Script/NPC也可使用）。

类说明： LaserEmitter : MonoBehaviour。附在玩家武器对象上。

字段：

public GameObject beamPrefab; —— 激光束预制体，内部带Collider2D和渲染。

public float fireRate; public float damage; public float maxDistance; —— 连射速率、伤害值、最大射程等参数。

private bool canFire = true; —— 控制发射冷却。

主要方法：

public void Fire(Vector2 direction) —— 开始发射激光。可能通过协程连发：生成beamPrefab实例并赋予初始速度或伸展后销毁，给它添加伤害值。

IEnumerator FireRoutine() —— 如果是持续射击模式，协程重复生成激光。

public void StopFiring() —— 停止连射。

脚本交互： 激光实例的Collision可能与Mirror2D、LightSource2D等交互；BeamReflector2D负责处理激光的反射逻辑。敌人（如Projectile）检测与激光碰撞并扣血。

用户界面脚本（Scripts/UI）
ContinueMenu.cs

功能概述： “继续游戏”菜单面板脚本。用于列出当前最高解锁章节下所有已发现的检查点项，让玩家点击后跳转到相应位置。

类说明： ContinueMenu : MonoBehaviour，挂载在继续菜单的Canvas或Panel上。

字段：

public GameObject buttonPrefab; —— 用于动态生成列表项的按钮预制体。

public Transform listParent; —— 列表项的父容器，用于承载生成的按钮。

主要方法：

void OnEnable() —— 每次菜单激活时被调用。实现：清空现有列表子对象，然后从GameManager.CurrentChapter获取当前章节（或通过SceneLoader等）。调用SaveSystem.Instance.GetCheckpoints()获取所有已发现的检查点ID列表，筛选出属于当前章节的（例如ID中包含当前章节名或规则）。遍历这些ID，为每个创建一个按钮实例(buttonPrefab)，设置按钮文本（如检查点编号），并给按钮添加回调：点击时调用SceneLoader.LoadScene(sceneName, checkpointId)加载对应场景和检查点。

public void OnBackClicked() —— 后退按钮回调，关闭菜单UI并返回上级菜单或继续游戏。

对外接口： Unity的UI按钮事件（OnClick）配置；无其他脚本调用方法。

脚本交互： 使用了存档系统来获取检查点数据；使用了场景加载器来实际加载场景。

GlowTextBanner.cs

功能概述： 全局文本提示横幅，用于在屏幕中央显示临时提示文字（如拾取物品提示、区域进入提示等）。通常只有一个全局实例。

类说明： GlowTextBanner : MonoBehaviour，通常挂在Canvas上并标记为单例（如public static GlowTextBanner Instance）。

字段：

public CanvasGroup canvasGroup; —— 控制横幅淡入淡出。

public TMP_Text textComponent; —— TextMeshProUGUI组件，用于显示文本。

public float fadeDuration; —— 淡入淡出持续时间。

主要方法：

public void Show(string message, float displayTime) —— 对外调用接口：设置textComponent.text = message，然后通过协程FadeInAndOut(displayTime)使横幅淡入显示，在指定时间后淡出并隐藏。

private IEnumerator FadeInAndOut(float displayTime) —— 实现：先渐变canvasGroup.alpha从0到1（fade in），保持displayTime秒，再从1到0（fade out）。

对外接口： 单例Instance.Show(...)可在任何脚本中调用（如进入某区域时）。

脚本交互： 其他脚本（如触发器、剧情事件）通过调用此脚本的Show方法来提示玩家。

HUDController.cs

功能概述： 玩家状态HUD控制器，更新并显示玩家当前的光能、模式或其他状态条。挂载在游戏HUD Canvas下。

类说明： HUDController : MonoBehaviour。

字段：

public Slider energyBar; —— 显示玩家当前光能百分比的Slider条。

public TextMeshProUGUI modeLabel; —— 显示当前光模式名称（如“Red Mode”或“Green Mode”）。

可能有public Image redMask;等，显示红光能量。

私有：缓存对PlayerLightController和PlayerColorModeController的引用。

主要方法：

void Start() —— 获取玩家控制组件引用并初始化UI显示。

void Update() —— 每帧更新：读取playerLightController.currentEnergy/maxEnergy并设置energyBar.value，更新modeLabel.text为当前模式(PlayerColorModeController.ColorMode)。也可以监听事件优化性能。

对外接口： 无公开方法，仅动态响应玩家状态。

脚本交互： 读取PlayerLightController和PlayerColorModeController的数据，并在UI元素中显示。

MainMenu.cs

功能概述： 主菜单控制器，负责新游戏、继续、退出等按钮逻辑。

类说明： MainMenu : MonoBehaviour，挂载在主菜单界面的Canvas上。

字段：

public Canvas mainMenuCanvas; public Canvas continueCanvas; public Canvas exitConfirmCanvas; —— 各个子菜单Panel的引用。

主要方法：

public void OnNewGame() —— “新游戏”按钮回调：可调用SaveSystem.ResetAll()清空存档，然后调用SceneLoader.LoadScene("Chapter1", "")加载第一章初始场景。

public void OnContinue() —— “继续”按钮回调：关闭主菜单UI，打开continueCanvas（弹出“继续”列表）。

public void OnQuit() —— “退出”按钮回调：调用Application.Quit()退出游戏，或在编辑模式下返回主菜单。

协程/动画： 可能与ScreenFade或ProjectedMenuController配合，产生界面过渡动画。

对外接口： Unity UI事件绑定，无其他脚本调用。

MainMenuOrchestrator.cs

功能概述： 主菜单动画和光影效果协调器。管理主菜单中光标高亮、鼠标跟踪光效以及键盘导航。

类说明： MainMenuOrchestrator : MonoBehaviour。

字段：

public GameObject mouseSpotlight; —— 鼠标位置的光照特效（参考MouseSpotlight.cs）。

public Animator[] menuAnimators; —— 可扩展数组，包含主菜单各按钮的Animator，用于激活不同按钮的选中动画。

private int selectedIndex; —— 记录当前选中按钮索引。

主要方法：

void Update() —— 每帧检测鼠标和键盘输入：

鼠标：使用RectTransformUtility.ScreenPointToLocalPointInRectangle检测光标是否悬停在某按钮上，如果是，则在该按钮上触发高亮动画。

键盘：监听上下箭头或W/S键，改变selectedIndex，并相应播放动画。

确认键（Enter）触发对应按钮的点击事件。

public void SetSelected(int index) —— 手动设置选中按钮，用于UI导航。

脚本交互： 跟MouseSpotlight联合，实现鼠标位置的聚光灯效果。通过Animator控制各按钮的高亮状态（可在Animator里实现平滑过渡）。

MouseSpotlight.cs

功能概述： 光标聚光效果控制器，在鼠标指针处创建亮光效果。用于主菜单和其他场景中，增加视觉表现。

类说明： MouseSpotlight : MonoBehaviour。

字段：

public GameObject spotlightPrefab; —— 聚光灯预制体（如带有光源的Sprite）。

private GameObject spotlightInstance; —— 运行时实例。

主要方法：

void Update() —— 每帧将spotlightInstance.transform.position设为当前鼠标在世界中的位置（通过Camera.ScreenToWorldPoint转换）。

void OnEnable()/OnDisable() —— 创建或销毁聚光实例以避免多余游戏对象。

脚本交互： 作为主菜单、暂停菜单等UI的辅助脚本，通过在Canvas上实时跟随鼠标指针在UI元素上产生光照效果。

ProjectedMenuController.cs

功能概述： 3D投影式菜单控制器，实现类似全息投影的UI。主要在某些场景（如太空船里）使用。

类说明： ProjectedMenuController : MonoBehaviour。

字段：

可能有public Canvas projectedCanvas;和public Transform lookAtTarget;，使菜单总是面向玩家摄像机。

主要方法：

void Update() —— 将菜单对象的朝向设置为面对摄像机，以保持阅读方向不变。

脚本交互： 结合ProjectedMenuItem脚本，使得菜单按钮在世界空间中显示，并支持交互。

ProjectedMenuItem.cs

功能概述： 3D世界空间中可交互的菜单项，可通过撞击光标或点击进行选择。

类说明： ProjectedMenuItem : MonoBehaviour。

字段：

public UnityEvent onClick; —— 当此菜单项被激活时触发的事件。

public bool isSelected; —— 当前是否被选中（例如被光照照射）。

主要方法：

public void Click() —— 对外接口：当玩家点击此菜单项时调用，触发onClick事件。

脚本交互： 可能与MouseSpotlight等交互，当灯光照射到某菜单项上时，标记isSelected = true，并在玩家点击（或通过某输入）时调用Click()。

ScreenFade.cs

功能概述： 屏幕淡入淡出控制器，用于场景切换或菜单切换时的画面过渡效果。

类说明： ScreenFade : MonoBehaviour。通常挂在Canvas上。

字段：

public Image fadeImage; —— 全屏Image组件，用于绘制纯色（通常黑色）遮罩。

public float fadeDuration; —— 淡入/淡出持续时间。

主要方法：

public IEnumerator FadeOut() —— 逐渐将fadeImage.color.a从0变到1（纯黑），实现屏幕渐变全黑。

public IEnumerator FadeIn() —— 反向，将fadeImage.color.a从1变到0（透明），使场景显现。

脚本交互： 在SceneLoader.LoadScene前后可启动FadeOut和FadeIn协程，创建场景切换的渐隐效果。也可以与菜单动画共同使用。

PauseMenu.cs

功能概述： 暂停菜单控制器，管理暂停界面的显示/隐藏及游戏暂停。

类说明： PauseMenu : MonoBehaviour。挂载在暂停菜单Canvas。

字段：

public GameObject pausePanel; —— 暂停面板（包含按钮）的引用。

主要方法：

public void Toggle() —— 切换暂停状态：如果当前未暂停，则激活pausePanel并Time.timeScale = 0暂停游戏；如果已经暂停，则反向操作Time.timeScale = 1继续游戏并隐藏面板。

public void OnResume() —— “继续”按钮回调，调用Toggle()恢复游戏。

public void OnQuitToMenu() —— “退出到菜单”按钮回调，调用SceneLoader.LoadScene(mainMenuScene)加载主菜单场景，并重置时间缩放。

脚本交互： 与Input系统结合：另有PauseToggle脚本监听按键（如Esc），调用PauseMenu.Toggle()。

PauseToggle.cs

功能概述： 监听玩家输入键（通常为Esc或P），触发暂停菜单的切换。

类说明： PauseToggle : MonoBehaviour。挂在任何常驻的游戏对象上（如GameManager）。

字段：

public PauseMenu pauseMenu; —— 引用上述PauseMenu脚本实例。

主要方法：

void Update() —— 每帧检测是否按下暂停键（例如Input.GetKeyDown(KeyCode.Escape)）。若检测到则调用pauseMenu.Toggle()。

脚本交互： 直接调用PauseMenu方法控制游戏暂停。

RedLightHUD.cs

功能概述： 红光模式下的HUD显示组件，显示玩家红光能量（生命值）。

类说明： RedLightHUD : MonoBehaviour。挂载在HUD画布上专用红光UI元素。

字段：

public Slider redHealthBar; —— 红光能量条UI组件。

可能有public Image portrait;，显示玩家头像变化。

主要方法：

void Update() —— 每帧读取PlayerHealthLight.currentHealth/ maxHealth并更新滑块值。也可能订阅事件优化。

脚本交互： 与PlayerHealthLight交互，实时更新红光能量显示。

SaveSlotItem.cs

功能概述： 存档列表中的单个存档项，用于显示存档信息并响应选择。用于制作存档/读取界面。

类说明： SaveSlotItem : MonoBehaviour。一般挂在一个按钮预制体上。

字段：

public TextMeshProUGUI slotLabel; —— 存档槽显示的文本（如“存档1 - 章节3”）。

public Button slotButton; —— 按钮组件引用。

public int slotIndex; —— 存档槽索引。

主要方法：

public void Init(int index, string displayText) —— 初始化方法，设置索引和显示文本，然后添加按钮点击监听：点击时调用一个回调方法（可能由上级菜单提供）执行加载该存档。

private void OnClick() —— 私有方法，响应按钮点击，通知场景管理者加载对应存档。

脚本交互： 常与存档系统结合，在“继续游戏”或“读取存档”菜单中生成多个存档槽项。

世界与机制脚本（Scripts/World）
BackgroundEdgeRepeatFill.cs

功能概述： 自动平铺背景精灵，使其填满相机视野的边缘部分，支持在编辑器和运行时调整。主要用于2D背景，四周重复图块。

类说明： BackgroundEdgeRepeatFill : MonoBehaviour，带有[ExecuteAlways]属性，可在编辑模式中实时生效。

字段：

public SpriteRenderer spriteTemplate; —— 背景图块的SpriteRenderer模板，用于实例化副本。

public Camera targetCamera; —— 使用的相机，默认为主相机。

public int tilesX, tilesY; —— 水平和垂直方向上的图块数量。

主要方法：

void Update()（编辑模式+运行模式）—— 计算targetCamera当前的视野宽度和高度，然后根据tilesX/Y实例化和排列tilesX * tilesY个图块，调整位置使之紧贴屏幕边缘。

对外接口： 不对其他脚本提供公有方法，仅在自身生命周期自动执行。

备注： 帮助关卡美工在横板场景中自动填充背景图案。

Collectibles/ColorInteractable.cs

功能概述： 颜色感应交互器：当玩家进入该物体的触发器，并且玩家当前使用对应的光模式（红绿蓝）时触发预设行为。可用于门、机关等。

类说明： ColorInteractable : MonoBehaviour。附带Collider2D（触发）。

字段：

public ColorMode requiredMode; —— 需要玩家处于哪种颜色模式（如红/绿/蓝）才能触发。

public UnityEvent onInteract; —— 当条件满足（玩家在触发范围且匹配模式）时触发的事件。

主要方法：

private void OnTriggerEnter2D(Collider2D other) —— 检测进入区域的Collider。如果是玩家（检测other.CompareTag("Player")）且玩家当前模式等于requiredMode，则调用onInteract.Invoke()，执行绑定的行为（如开门）。

private void OnTriggerStay2D(Collider2D other) —— 也可能存在，用于持续触发。

对外接口： UnityEventonInteract供设计者在Inspector中绑定具体响应，如启用另一个GameObject或播放动画。

Collectibles/GlowTextZone.cs

功能概述： 同名UI脚本（GlowTextBanner）的场景触发器版本。当玩家进入该区域时，通过GlowTextBanner显示提示文本。

类说明： GlowTextZone : MonoBehaviour。附带Collider2D（触发），需在Inspector设置提示文字和参数。

字段：

public string message; —— 要显示的提示文本。

public float displayTime; —— 显示时长。

主要方法：

private void OnTriggerEnter2D(Collider2D other) —— 如果进入者为玩家，则调用GlowTextBanner.Instance.Show(message, displayTime)显示文本。

Hazards/InstantLightDrain.cs

功能概述： 光能陷阱：玩家一旦进入其触发范围（通常是Collider2D触发器），立即耗尽玩家的所有绿色光能，相当于瞬间死亡。

类说明： InstantLightDrain : MonoBehaviour。附带[RequireComponent(typeof(Collider2D))]。

字段： 无或可自定义效果。

主要方法：

private void OnTriggerEnter2D(Collider2D other) —— 当触发器有对象进入时：如果此对象有PlayerLightController（检测玩家），则调用playerLightController.TakeDamage(playerLightController.currentEnergy)将玩家能量扣为0，使其死亡。或者直接调用GameManager.OnPlayerDeath()。

脚本交互： 直接与玩家脚本交互，在关卡设计中用作致命陷阱。

Light/LightIrradianceSensor.cs

功能概述： 环境光传感器组件。用于检测周围可见2D光源（基于URP的2D光照）照射强度，并通过事件通知其他对象（如控制玩家能量恢复速率等）。

类说明： LightIrradianceSensor : MonoBehaviour。要求项目启用Universal Render Pipeline 2D光照。

字段：

public float detectionRadius; —— 感应半径。

public UnityEvent<float> onIrradianceChanged; —— 自定义事件，参数为当前检测到的平均光照强度。

主要方法：

void Update() —— 每帧计算：使用Light2DPointLight或相关API检测当前区域内所有2D灯光的照度值（例如通过光照采样或使用光照地图数据），并取平均值。然后调用onIrradianceChanged.Invoke(value)。

脚本交互： 与PlayerLightController配合：例如在玩家进入某亮度区域时，根据传感值改变playerLightController.regenRate等。也可用于驱动场景中其他机制。

Light/LightSource2D.cs

功能概述： 可投射光线的光源对象。用于触发关卡中的光互动效果（如照亮扳机）。

类说明： LightSource2D : MonoBehaviour。附带2D光源（如Light2D组件）。

字段：

可能有public float intensity; public float range;等2D光照属性。

主要方法：

提供光源存在，即自动在有URP时在场景照亮。

可能包含方法public void Ignite()或其它，以响应激光点燃。

脚本交互： 通常，与Torch.cs、PhotoGate.cs等关卡机关交互；镜面照射脚本（Mirror2D）可对激光束重新发射到LightSource2D从而触发这些。

Light/Torch.cs

功能概述： 火把（Torch）脚本，可被激光点燃。点燃后提供亮光，并可触发场景机关（如开启门）。

类说明： Torch : MonoBehaviour。附加到火把对象，包含2D灯光组件和粒子特效。

字段：

public float lightIntensityOn; —— 点燃后2D光源的强度。

public float igniteTime; —— 点燃动画持续时间。

private Light2D light2D; —— 2D光源引用。

private Animator animator; —— 火把的动画控制器（点燃火焰）。

主要方法：

void Awake()/Start() —— 初始化，默认设置火把未点燃状态（光强为0，动画关闭）。

public void Ignite() —— 公共方法：播放点燃动画（animator.SetTrigger("Ignite")），并在igniteTime过后将light2D.intensity = lightIntensityOn，并可调用OpenGate()等连接行为。

public void Extinguish() —— (如有需要) 熄灭火把，恢复到无光状态。

对外接口： Ignite()可被Mirror2D或LaserEmitter触发。

脚本交互： 与镜面反射和激光系统结合：激光射到火把碰撞器时（Mirror2D.OnTriggerEnter2D逻辑），调用此脚本点燃火把；火把点燃后可通过动画事件或脚本打开相连的门（如PhotoGate.OpenGate()）。

Mechanisms/DarkRedBlock.cs

功能概述： 暗红方块，只能被红光模式下的玩家击碎。模拟只有在特定光模式下才能破坏的障碍物。

类说明： DarkHeatBlock : MonoBehaviour。附加在方块对象上。

字段：

public Animator animator; —— 方块的Animator，用于播放破碎动画。

主要方法：

private void OnCollisionEnter2D(Collision2D other) —— 当有碰撞发生时：检查碰撞物是否为玩家（通过other.collider.CompareTag("Player")）。如果是玩家，再检查玩家当前光模式是否为红色（通过PlayerColorModeController.CurrentMode == ColorMode.Red或监听RedLightController）。若满足，调用animator.SetTrigger("Break")播放碎裂动画，并在动画结束时销毁方块物体（可通过动画事件或协程）。

对外接口： 无，需要在场景中放置并配置Animator。

脚本交互： 依赖玩家控制脚本的模式状态来判断是否允许破坏。

Mechanisms/HeatPlatform.cs

功能概述： 热力平台组件，当玩家或可燃物体站上去时，会产生升温效果（如表现为火焰特效）。也可以与光照互动。

类说明： HeatPlatform : MonoBehaviour。附带Collider2D。

字段：

public GameObject flameVFX; —— 火焰粒子特效。

public float ignitionThreshold; —— 触发火焰所需温度值（可以由玩家携带的火折扣计算）。

私有bool isOnFire;跟踪平台是否已经着火。

主要方法：

private void OnCollisionEnter2D(Collision2D other) —— 当对象接触到平台时：如果接触的是玩家并且玩家带有点燃能力（或Torch碰撞），启动Ignite()使平台着火（显示flameVFX）。

private void OnCollisionExit2D(Collision2D other) —— 离开时可能熄灭或保持。

public void Ignite() —— 使平台产生火焰效果，并可触发关联事件（如打开出口）。

脚本交互： 不同于“点燃”，也可接受激光等作为点燃来源。在流程上主要作为机关元素使用。

Mechanisms/LightDrivenMover.cs

功能概述： 光驱动移动装置：根据场景中的光线强度来控制平台或物体的移动。

类说明： LightDrivenMover : MonoBehaviour。

字段：

public List<Transform> targets; —— 受光线强度影响的目标物体列表。

public List<Vector3> offsetPositions; —— 光强度不同对应的目标相对偏移。

public LightIrradianceSensor sensor; —— 依赖的光照传感器参考。

主要方法：

void Update() —— 每帧读取sensor提供的光强度值，根据该值在offsetPositions中插值，平滑地移动每个targets[i]到对应的目标位置。

脚本交互： 与LightIrradianceSensor结合，实现根据环境光强度自动移动机关或平台（可用于关卡解谜）。

Mechanisms/PhotoGate.cs

功能概述： 光电门机关，需要玩家的光照触发后打开门。

类说明： PhotoGate : MonoBehaviour。附带[RequireComponent(typeof(Animator))]来控制门的开关动画。

字段：

public Animator animator; —— 门的Animator。

public string openTrigger = "Open"; —— 在Animator中触发开门的参数名。

public Collider2D gateCollider; —— 门的碰撞体，用于阻挡玩家。开门后可以禁用。

主要方法：

private void OnTriggerEnter2D(Collider2D other) —— 如果进入的是带有LightSource2D组件的对象（例如玩家发出的激光束碰撞器），则调用OpenGate()。

public void OpenGate() —— 执行动画开门：animator.SetTrigger(openTrigger)，并可禁用gateCollider使门消失或变得可穿过。

脚本交互： 当玩家发射激光照射到该门的触发器上时（或利用镜面反射导向门），实现机关解锁。

Mirror2D.cs

功能概述： 2D镜面反射器，将激光束折射/反射到指定方向。常作为激光反射机关（放置倾斜镜面可改变激光路径）。

类说明： Mirror2D : MonoBehaviour，附带[RequireComponent(typeof(Collider2D))]用于激光碰撞检测。

字段：

public float reflectorAngle; —— 镜面的角度，用于计算反射方向。

public bool isReflective; —— 是否启用反射（可在场景中关闭镜面功能）。

主要方法：

private void OnTriggerEnter2D(Collider2D other) —— 当激光束（带Collider的发射物）碰到镜面时：

获取激光射入的方向向量（可通过BeamReflector2D.Result或other组件）。

计算反射方向：通常使用镜面法线进行向量反射。

调用BeamReflector2D.Cast()或生成新的激光射线，将结果反射路径施加到场景中：创建新的LightSource2D或Projectile沿反射方向前进。

对外接口： 无公开方法。

脚本交互： 与BeamReflector2D静态方法配合，利用物理法线计算反射。

敌人及武器（Scripts/Enemies）
IDamageable.cs

功能概述： 接口定义，用于任何可被伤害的对象（敌人、玩家等）实现。

接口：

public interface IDamageable {
    void TakeDamage(float amount);
}


任何需要接受伤害的类实现此接口，例如敌人和玩家脚本。

BlackAI.cs

功能概述： 基础敌人行为（黑暗敌人）的AI控制脚本，实现追踪或攻击玩家。

类说明： BlackAI : MonoBehaviour，实现IDamageable接口。

字段：

public float moveSpeed; public float health; —— 移动速度和生命值。

private Rigidbody2D rb; private Transform player; —— 自身刚体和玩家引用。

主要方法：

void Start()/Awake() —— 获取刚体和玩家引用。

void Update()/FixedUpdate() —— 持续向玩家方向移动（或巡逻）。

public void TakeDamage(float amount) —— 实现IDamageable：减少生命值，若<=0则销毁敌人。播放击碎动画或特效。

Unity事件： 可能有OnCollisionEnter2D处理与玩家碰撞造成玩家伤害。

脚本交互： 检测玩家位置来移动；可能与Projectile交互（被玩家子弹击中调用TakeDamage）。

DarkSpriteAI.cs

功能概述： 特殊敌人AI，可能对光线有特殊反应（如被光照打败）。

类说明： DarkSpriteAI : MonoBehaviour，实现IDamageable。

字段：

public float health; public float speed; public ColorMode weakness; —— 健康值、移动速度以及弱点颜色模式（红光或绿光）。

主要方法：

void Update() —— AI逻辑，可能跟踪玩家或随机移动。

public void TakeDamage(float amount) —— 被击中时，减少生命值。若玩家当前光模式非weakness时或普通攻击伤害时等，减少生命；若health<=0，播放死亡动画并销毁。

脚本交互： 检测PlayerColorModeController，只有在玩家激活对应颜色时才会受伤或被破坏。

EnemySelfExplodeOnOverheat.cs

功能概述： 敌人脚本，表示当环境温度（或光照）过高时，敌人会自爆。

类说明： EnemySelfExplodeOnOverheat : MonoBehaviour，可能实现IDamageable。

字段：

public float health; public float overheatThreshold; public GameObject explosionPrefab; —— 血量，过热阈值，以及爆炸效果预制体。

主要方法：

private void Update() —— 检查当前环境光强（可能通过LightIrradianceSensor）或是否处于太热区域，如果超过overheatThreshold，则执行Explode()。

private void Explode() —— 实例化爆炸特效，给周围对象造成伤害（调用它们的TakeDamage），并销毁自己。

public void TakeDamage(float amount) —— 普通攻击减少生命值，同样触发爆炸条件检查。

脚本交互： 可能与光照系统（通过传感器事件）结合，也与Projectile和PlayerColorModeController的模式有关。

Projectile.cs

功能概述： 投射物基类脚本，用于敌人或玩家发射的子弹/激光粒子，具有速度和伤害属性。

类说明： Projectile : MonoBehaviour，可能实现移动和生命周期。

字段：

public float speed; public float damage; public float lifeTime; —— 子弹速度、伤害值以及最大存活时间（到期自动销毁）。

主要方法：

void Start() —— 启动协程DestroyAfterTime()，在lifeTime后自动销毁自己。

void Update() —— 每帧沿箭头方向移动（使用transform.Translate(Vector3.forward * speed * Time.deltaTime)或Rigidbody2D.MovePosition）。

private void OnTriggerEnter2D(Collider2D other) —— 碰撞检测：若碰撞到IDamageable对象，则调用TakeDamage(damage)，然后销毁投射物（除非设置穿透逻辑）。

对外接口： 可被任何发射器脚本实例化，直接使用并设置速度、伤害。

章节三专用脚本（Scripts/d第三章）

说明： 这些脚本为“第三章”独有的实验/关卡脚本，包含额外的玩法机制（如能量武器、特殊敌人、UI元素等）。下面仅概述主要功能。

AreaEnergyRegen.cs：区域能量恢复器。玩家进入该区域时会持续恢复绿色光能。包括触发器检测玩家，累加能量；可能按时间显示区域光效。

BracketVisual.cs：能量UI刻度条的可视控制器，负责通过动画或Shader展示当前能量状态（如闪烁提示）。可能根据EnergyBracketsController的值更新UI效果。

BulletProjectile.cs：子弹射击脚本（与Projectile.cs类似），专门用于章节3的武器或敌人。

CloudSniperAI.cs：空中狙击型敌人AI。从天上飞行并朝玩家射击。追踪玩家后纵向移动射击逻辑；可能使用BulletProjectile发射子弹。

EnemyHealth.cs：通用敌人血量管理脚本。实现IDamageable接口，跟踪生命并在受伤时播放爆炸或抖动。可能与CameraShake2D（见下）配合，为每次受击添加镜头晃动效果。

ExplosionLightFader：处理敌人爆炸后发光渐弱的细节效果。

CameraShake2D：镜头抖动控制器，供EnemyHealth或其他震动场景效果时调用。

EnergyBracketsController.cs：能量条UI控制器，管理多格能量槽的显示。根据玩家当前能量量动态点亮或熄灭多个条槽（类似风扇叶片UI）。

EnergyPickup.cs：能量拾取物件，玩家碰撞时增加PlayerLightController.currentEnergy并销毁自己。可能播放特效或提示。

EnergyPickupLightTint.cs：能量物品的光照色彩控制，根据当前玩家光模式改变其颜色或亮度，提示玩家适合何种光攻击。

FlashStrike.cs：玩家技能或武器脚本，类似从高空落下造成冲击波。可能在玩家冲刺或翻滚时触发，使周围敌人受到伤害，附带屏幕闪白或火光效果。

ModeVisibilityFilter.cs：场景视觉过滤器。根据玩家当前颜色模式，隐藏或显示场景中的某些元素（例如只有在红光模式下可见的路径），通过切换层或透明度实现。

PlayerColorModeController.cs：玩家颜色模式切换器。允许玩家在红/绿两种光模式间切换（枚举ColorMode { Red, Green }）。带有public UnityEvent<ColorMode> onColorModeChanged事件，切换时广播给其他对象（如UI、受光影响的机关）。有方法SwitchColorMode()供输入触发。还定义了EnergyEvent用于广播当前能量值更新。

PlayerMeleeLaser.cs：玩家近战激光武器。玩家挥动武器生成近距离激光撞击判定，对近身敌人造成伤害。

PlayerRangedCharger.cs：玩家远程充能武器。按住按钮充能，释放时发射高能量爆炸或激光。管理充能时间、子弹生成等。

ShockwaveGrenade.cs：扔出震波手雷。生成爆炸后产生冲击波，对周围敌人施加力或伤害，可能摧毁物体。

SwarmMeleeAI.cs：群体近战型敌人。较弱但数量多的敌人AI，往往会围绕玩家移动并攻击。受碰撞或近距离攻击伤害时死亡。

Bracket/ 其它（前缀脚本）：章节3还可能包括对GUI如BracketVisual、镜头CameraShake2D等支持。

这些“第三章”脚本通过协程（如充能武器）、委托/事件系统（如UnityEvent用于颜色切换广播）、物理碰撞/触发器（拾取、武器打击、爆炸触发等）等机制进行联动，构成丰富的关卡机制。每个脚本内部字段、方法与前面通用规则类似：暴露供外界调用的接口（UnityEvent、public方法）、私有逻辑与Unity回调等。