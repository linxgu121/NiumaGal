using NiumaGal.Extension.Ambient;
using TMPro;
using UnityEngine;

namespace NiumaGal.Presenter
{
    /// <summary>
    /// 环境叙事的轻量 UI 桥接。
    /// 只负责把 DialoguePresenter 的 Ambient 事件写入已绑定的 UI 节点，不创建 UI，不参与剧情推进。
    /// </summary>
    public sealed class AmbientNarrativeUIViewBridge : MonoBehaviour
    {
        [Header("表现层引用")]
        [Tooltip("场景中的 DialoguePresenter。为空时会优先从当前物体查找，找不到再从场景中查找。")]
        [SerializeField] private DialoguePresenter presenter;

        [Header("头顶气泡")]
        [Tooltip("气泡根节点。显示 Bubble 模式时开启，关闭环境叙事时隐藏。")]
        [SerializeField] private GameObject bubbleRoot;
        [Tooltip("气泡 RectTransform。用于把气泡移动到叙事源的屏幕位置。")]
        [SerializeField] private RectTransform bubbleRect;
        [Tooltip("气泡说话人文本。可不绑定，不绑定时只显示正文。")]
        [SerializeField] private TMP_Text bubbleSpeakerText;
        [Tooltip("气泡正文文本。")]
        [SerializeField] private TMP_Text bubbleBodyText;
        [Tooltip("气泡屏幕偏移。用于让气泡显示在 NPC 头顶上方。")]
        [SerializeField] private Vector2 bubbleScreenOffset = new Vector2(0f, 80f);

        [Header("旁白字幕")]
        [Tooltip("字幕根节点。显示 Subtitle 或 ProximityMonologue 模式时开启。")]
        [SerializeField] private GameObject subtitleRoot;
        [Tooltip("字幕说话人文本。可不绑定，不绑定时只显示正文。")]
        [SerializeField] private TMP_Text subtitleSpeakerText;
        [Tooltip("字幕正文文本。")]
        [SerializeField] private TMP_Text subtitleBodyText;

        [Header("相机")]
        [Tooltip("用于把世界坐标转换为屏幕坐标的相机。为空时使用 Camera.main。")]
        [SerializeField] private Camera worldCamera;
        [Tooltip("未绑定 WorldCamera 且场景暂时没有 MainCamera 时，多久重新查找一次 Camera.main，避免每帧查找造成额外开销。")]
        [SerializeField] private float cameraSearchRetryInterval = 1f;

        [Header("调试")]
        [Tooltip("Awake 时是否先隐藏所有环境叙事 UI。")]
        [SerializeField] private bool hideOnAwake = true;
        [Tooltip("关键引用缺失时是否输出警告。")]
        [SerializeField] private bool logWarnings = true;

        private AmbientLineViewData _currentData;
        private bool _hasBubbleSource;
        private float _nextCameraSearchTime;
        private bool _reportedMissingWorldCamera;

        private void Awake()
        {
            ResolveReferences();

            if (hideOnAwake)
                HideAll();
        }

        private void OnEnable()
        {
            ResolveReferences();

            if (presenter == null)
            {
                if (logWarnings)
                    Debug.LogWarning("[AmbientNarrativeUIViewBridge] DialoguePresenter 未找到，环境叙事 UI 无法刷新。", this);
                return;
            }

            presenter.OnAmbientLineStarted += HandleAmbientLineUpdated;
            presenter.OnAmbientLineUpdated += HandleAmbientLineUpdated;
            presenter.OnAmbientClosed += HandleAmbientClosed;
        }

        private void OnDisable()
        {
            if (presenter == null)
                return;

            presenter.OnAmbientLineStarted -= HandleAmbientLineUpdated;
            presenter.OnAmbientLineUpdated -= HandleAmbientLineUpdated;
            presenter.OnAmbientClosed -= HandleAmbientClosed;
        }

        private void LateUpdate()
        {
            if (!_hasBubbleSource || bubbleRect == null || _currentData.SourceTransform == null)
                return;

            var camera = ResolveWorldCamera();
            if (camera == null)
                return;

            Vector3 screenPosition = camera.WorldToScreenPoint(_currentData.SourceTransform.position);
            if (screenPosition.z < 0f)
            {
                if (bubbleRoot != null)
                    bubbleRoot.SetActive(false);
                return;
            }

            if (bubbleRoot != null && !bubbleRoot.activeSelf)
                bubbleRoot.SetActive(true);

            bubbleRect.position = (Vector2)screenPosition + bubbleScreenOffset;
        }

        private void ResolveReferences()
        {
            if (presenter == null)
                presenter = GetComponent<DialoguePresenter>() ?? FindObjectOfType<DialoguePresenter>();

            if (worldCamera == null)
                TryResolveWorldCamera(true);
        }

        private Camera ResolveWorldCamera()
        {
            if (worldCamera != null)
                return worldCamera;

            TryResolveWorldCamera(false);
            return worldCamera;
        }

        private void TryResolveWorldCamera(bool force)
        {
            if (!force && Time.unscaledTime < _nextCameraSearchTime)
                return;

            _nextCameraSearchTime = Time.unscaledTime + Mathf.Max(0.1f, cameraSearchRetryInterval);
            worldCamera = Camera.main;

            if (worldCamera == null && logWarnings && !_reportedMissingWorldCamera)
            {
                _reportedMissingWorldCamera = true;
                Debug.LogWarning("[AmbientNarrativeUIViewBridge] WorldCamera 未绑定，且场景中没有 MainCamera，气泡无法跟随世界目标。", this);
            }
        }

        private void HandleAmbientLineUpdated(AmbientLineViewData data)
        {
            _currentData = data;

            if (data.Mode == AmbientMode.Bubble)
                ShowBubble(data);
            else
                ShowSubtitle(data);
        }

        private void ShowBubble(AmbientLineViewData data)
        {
            if (subtitleRoot != null)
                subtitleRoot.SetActive(false);

            if (bubbleRoot != null)
                bubbleRoot.SetActive(true);

            SetText(bubbleSpeakerText, data.Speaker);
            SetText(bubbleBodyText, data.DisplayText);

            _hasBubbleSource = data.SourceTransform != null;
        }

        private void ShowSubtitle(AmbientLineViewData data)
        {
            _hasBubbleSource = false;

            if (bubbleRoot != null)
                bubbleRoot.SetActive(false);

            if (subtitleRoot != null)
                subtitleRoot.SetActive(true);

            SetText(subtitleSpeakerText, data.Speaker);
            SetText(subtitleBodyText, data.DisplayText);
        }

        private void HandleAmbientClosed(AmbientMode mode)
        {
            _hasBubbleSource = false;

            if (mode == AmbientMode.Bubble)
            {
                if (bubbleRoot != null)
                    bubbleRoot.SetActive(false);
            }
            else if (subtitleRoot != null)
            {
                subtitleRoot.SetActive(false);
            }
        }

        private void HideAll()
        {
            _hasBubbleSource = false;

            if (bubbleRoot != null)
                bubbleRoot.SetActive(false);

            if (subtitleRoot != null)
                subtitleRoot.SetActive(false);
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null)
                target.text = value ?? string.Empty;
        }
    }
}
