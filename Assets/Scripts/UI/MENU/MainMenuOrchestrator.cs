using System.Collections;
using UnityEngine;

namespace FadedDreams.UI
{
    public class MainMenuOrchestrator : MonoBehaviour
    {
        [Header("Stage")]
        public Camera cam;
        public Transform itemsRoot;
        public GameObject pressAnyKey;
        public float itemsFadeInDelay = 0.25f;
        public float idleCinematicDelay = 15f;
        public float camDriftAmp = 0.5f;
        public float camDriftSpeed = 0.2f;

        [Header("Refs")]
        public ProjectedMenuItem newGameItem;
        public ProjectedMenuItem continueItem;
        public ProjectedMenuItem quitItem;

        [Header("Lighting")]
        [Range(0f, 1.5f)]
        public float areaSpotDimMul = 0.65f;     // �����ھ۹���������
        public bool addMouseSpotlight = true;    // �Ƿ�������ϼ����׷�پ۹��
        public MouseSpotlight mouseSpotlightPrefab; // �����գ�Ϊ����ֱ�� AddComponent

        float idleTimer;
        Vector3 camBasePos, camBaseEuler;

        void Awake()
        {
            if (!cam) cam = Camera.main;
            camBasePos = cam.transform.position;
            camBaseEuler = cam.transform.eulerAngles;

            // ��ʼ��������ť����ʾ PressAnyKey
            SetItemsVisible(false);
            if (pressAnyKey) pressAnyKey.SetActive(true);

            // ȫ�ֵ����˵����
            ProjectedMenuItem.SetGlobalSpotMul(areaSpotDimMul);

            // ��������۹�ƣ����ӳٸ��棩
            if (addMouseSpotlight)
            {
                MouseSpotlight ms = null;
                if (mouseSpotlightPrefab)
                {
                    ms = Instantiate(mouseSpotlightPrefab, cam.transform);
                    ms.transform.localPosition = Vector3.zero;
                }
                else
                {
                    ms = cam.gameObject.GetComponent<MouseSpotlight>();
                    if (!ms) ms = cam.gameObject.AddComponent<MouseSpotlight>();
                }
                ms.enabled = true;
            }
        }

        void Update()
        {
            // �������뻽��
            if (pressAnyKey && pressAnyKey.activeSelf && Input.anyKeyDown)
            {
                StartCoroutine(RevealMenu());
            }

            if (!pressAnyKey || !pressAnyKey.activeSelf)
            {
                // Idle ��ʱ�뾵ͷ��΢Ư��
                idleTimer += Time.deltaTime;
                if (idleTimer > idleCinematicDelay)
                {
                    float t = Time.time * camDriftSpeed;
                    cam.transform.position = camBasePos + new Vector3(Mathf.Sin(t), Mathf.Sin(t * 0.7f) * 0.2f, Mathf.Cos(t * 0.6f)) * camDriftAmp * 0.1f;
                    cam.transform.eulerAngles = camBaseEuler + new Vector3(Mathf.Sin(t * 0.5f) * camDriftAmp, Mathf.Cos(t * 0.4f) * camDriftAmp, 0);
                }

                if (Input.anyKeyDown || Mathf.Abs(Input.GetAxisRaw("Mouse X")) > 0 || Mathf.Abs(Input.GetAxisRaw("Mouse Y")) > 0)
                {
                    idleTimer = 0f;
                    cam.transform.position = Vector3.Lerp(cam.transform.position, camBasePos, 0.6f);
                    cam.transform.eulerAngles = Vector3.Lerp(cam.transform.eulerAngles, camBaseEuler, 0.6f);
                }
            }
        }

        IEnumerator RevealMenu()
        {
            if (pressAnyKey) pressAnyKey.SetActive(false);
            yield return new WaitForSeconds(itemsFadeInDelay);

            // ���ݴ浵�������� Continue
            bool hasSave = !string.IsNullOrEmpty(FadedDreams.Core.SaveSystem.Instance.LoadLastScene());
            continueItem.SetInteractable(hasSave);

            SetItemsVisible(true);
        }

        void SetItemsVisible(bool v)
        {
            foreach (Transform t in itemsRoot)
            {
                var it = t.GetComponent<ProjectedMenuItem>();
                if (!it) continue;
                it.SetVisible(v, fadeDuration: 0.4f);
            }
        }
    }
}
