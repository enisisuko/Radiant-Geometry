using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

[DisallowMultipleComponent]
public class SeekerBurstEmitter : MonoBehaviour
{
    [Header("预制体 & 目标")]
    public HomingShard2D shardPrefab;
    public Transform player;

    [Header("发射频率（会越来越快）")]
    [Min(0.01f)] public float initialInterval = 0.5f;
    [Min(0.01f)] public float minInterval = 0.01f;

    [Header("发射数量与场景")]
    [Min(1)] public int totalToEmit = 77;
    [Tooltip("要切换到的场景名（需加入 Build Settings）")]
    public string nextSceneName = "NextScene";
    [Min(0f)] public float sceneLoadDelay = 0f;

    [Header("初速度配置")]
    public Vector2 speedRange = new Vector2(6f, 12f);
    [Min(0f)] public float spawnJitterRadius = 0.1f;

    [Header("红幕过场")]
    [Tooltip("勾上后，用红幕淡入→切场景→淡出")]
    public bool useRedCurtain = true;
    [Tooltip("淡入到全红时长（秒）")]
    [Min(0f)] public float curtainFadeIn = 0.8f;
    [Tooltip("场景加载后的淡出时长（秒）")]
    [Min(0f)] public float curtainFadeOut = 1.2f;
    [Tooltip("到达全红后再停留多久（秒）")]
    [Min(0f)] public float curtainHoldAtFull = 0f;

    [Header("可视化")]
    public bool drawGizmos = true;
    public float gizmoRadius = 0.25f;

    int emitted;

    void Start()
    {
        if (!player)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p) player = p.transform;
        }

        if (!shardPrefab)
        {
            Debug.LogError("[SeekerBurstEmitter] 未指定 shardPrefab，小物体无法生成。", this);
            enabled = false;
            return;
        }

        StartCoroutine(EmitRoutine());
    }

    IEnumerator EmitRoutine()
    {
        emitted = 0;

        while (emitted < totalToEmit)
        {
            float progress01 = emitted / Mathf.Max(1f, (float)totalToEmit - 1f);
            float interval = Mathf.Lerp(initialInterval, minInterval, progress01);

            Vector2 dir = Random.insideUnitCircle.normalized;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
            float speed = Random.Range(Mathf.Min(speedRange.x, speedRange.y), Mathf.Max(speedRange.x, speedRange.y));
            Vector2 velocity = dir * speed;

            Vector2 spawnPos = (Vector2)transform.position + Random.insideUnitCircle * spawnJitterRadius;

            var shard = Instantiate(shardPrefab, spawnPos, Quaternion.identity);
            shard.Launch(velocity, player);

            emitted++;
            yield return new WaitForSeconds(interval);
        }

        // 可选：发完后等待一会再切换
        if (sceneLoadDelay > 0f)
            yield return new WaitForSeconds(sceneLoadDelay);

        if (string.IsNullOrEmpty(nextSceneName))
        {
            Debug.LogWarning("[SeekerBurstEmitter] nextSceneName 为空，已达到发射数量但不会切场景。", this);
            yield break;
        }

        if (useRedCurtain)
        {
            // —— 使用红幕过场：淡入→LoadScene→淡出（全程使用未缩放时间）——
            RedCurtainTransition.Go(nextSceneName, curtainFadeIn, curtainFadeOut, curtainHoldAtFull);
        }
        else
        {
            // —— 旧逻辑：直接切场景 —— 
            SceneManager.LoadScene(nextSceneName);
        }
    }

    void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;
        Gizmos.color = new Color(1f, 1f, 1f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, gizmoRadius);
    }
}
