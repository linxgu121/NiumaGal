using NiumaGal.Extension.Ambient;
using NiumaGal.Presenter;
using NiumaUI.Toolkit;
using NiumaUI.Views.AmbientNarrative;
using UnityEngine;

namespace NiumaGal.ToolkitBridge
{
    /// <summary>
    /// NiumaGal 环境叙事到 NiumaUI Toolkit 的桥接。
    /// 监听 DialoguePresenter 的 Ambient 事件，并把气泡 / 字幕数据推给 AmbientNarrativeToolkitBinding。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NiumaGalToolkitAmbientNarrativeBridge : MonoBehaviour
    {
        [Header("模块引用")]
        [Tooltip("UI Toolkit 根控制器。拖核心场景 UIRoot/UIManager 上的 UIToolkitUIManager。")]
        [SerializeField] private UIToolkitUIManager uiManager;

        [Tooltip("对话表现播放器。拖 DialogueRoot 上的 DialoguePresenter，用于接收环境叙事事件。")]
        [SerializeField] private DialoguePresenter presenter;

        [Header("Toolkit View")]
        [Tooltip("UI Toolkit 注册表中的环境叙事 ViewId。需要在 UIToolkitViewRegistrySO 中注册同名 View。")]
        [SerializeField] private string ambientViewId = "AmbientNarrative";

        [Tooltip("收到环境叙事时是否自动打开 Toolkit View。关闭后需要外部先打开该 View，本脚本只刷新数据。")]
        [SerializeField] private bool autoOpenView = true;

        [Tooltip("环境叙事关闭时是否自动关闭 Toolkit View。关闭后会改为刷新空数据。")]
        [SerializeField] private bool closeViewOnAmbientClosed = true;

        [Header("气泡定位")]
        [Tooltip("用于把世界坐标转换为屏幕坐标的相机。为空时使用 Camera.main。")]
        [SerializeField] private Camera worldCamera;

        [Tooltip("气泡屏幕偏移。通常让气泡显示在 NPC 头顶上方。")]
        [SerializeField] private Vector2 bubbleScreenOffset = new Vector2(0f, 80f);

        [Tooltip("Bubble 模式下是否每帧刷新气泡位置。需要 NPC 或玩家移动时建议开启。")]
        [SerializeField] private bool updateBubblePositionEveryFrame = true;

        [Tooltip("未绑定 WorldCamera 且场景中没有 MainCamera 时，多久重新查找一次 Camera.main。")]
        [SerializeField] private float cameraSearchRetryInterval = 1f;

        [Header("调试")]
        [Tooltip("缺少 UIManager、Presenter、ViewId 未注册等情况是否输出警告。")]
        [SerializeField] private bool logWarnings = true;

        private AmbientLineViewData _currentData;
        private bool _hasActiveLine;
        private bool _isBubbleLine;
        private bool _lastViewVisible;
        private Vector2 _lastScreenPosition;
        private float _nextCameraSearchTime;
        private bool _reportedMissingWorldCamera;

        private void Awake()
        {
            ResolveReferences(false);
        }

        private void OnEnable()
        {
            ResolveReferences(true);

            if (presenter == null)
                return;

            presenter.OnAmbientLineStarted += HandleAmbientLineUpdated;
            presenter.OnAmbientLineUpdated += HandleAmbientLineUpdated;
            presenter.OnAmbientClosed += HandleAmbientClosed;
        }

        private void OnDisable()
        {
            if (presenter != null)
            {
                presenter.OnAmbientLineStarted -= HandleAmbientLineUpdated;
                presenter.OnAmbientLineUpdated -= HandleAmbientLineUpdated;
                presenter.OnAmbientClosed -= HandleAmbientClosed;
            }

            _hasActiveLine = false;
            _isBubbleLine = false;
            ApplyEmptyView();
        }

        private void LateUpdate()
        {
            if (!updateBubblePositionEveryFrame || !_hasActiveLine || !_isBubbleLine)
                return;

            if (_currentData.SourceTransform == null)
                return;

            ApplyAmbientLine(_currentData, false);
        }

        private void HandleAmbientLineUpdated(AmbientLineViewData data)
        {
            _currentData = data;
            _hasActiveLine = true;
            _isBubbleLine = data.Mode == AmbientMode.Bubble;
            ApplyAmbientLine(data, true);
        }

        private void HandleAmbientClosed(AmbientMode mode)
        {
            _hasActiveLine = false;
            _isBubbleLine = false;

            if (!EnsureUIManager(false))
                return;

            if (closeViewOnAmbientClosed)
            {
                uiManager.CloseView(ambientViewId);
                _lastViewVisible = false;
            }
            else
            {
                uiManager.RefreshView(ambientViewId, AmbientNarrativeToolkitViewData.Empty());
            }
        }

        private void ApplyAmbientLine(AmbientLineViewData data, bool forceRefresh)
        {
            if (!EnsureUIManager())
                return;

            var viewData = BuildViewData(data);
            if (!forceRefresh && !ShouldRefreshPosition(viewData))
                return;

            if (autoOpenView)
            {
                _lastViewVisible = uiManager.OpenView(ambientViewId, viewData);
            }
            else
            {
                _lastViewVisible = uiManager.RefreshView(ambientViewId, viewData);
            }

            if (!_lastViewVisible)
                Warn($"没有刷新到 Toolkit 环境叙事 View：ViewId={ambientViewId}。请检查 UIToolkitViewRegistrySO 和 AmbientNarrativeToolkitBindingProvider。");
        }

        private AmbientNarrativeToolkitViewData BuildViewData(AmbientLineViewData data)
        {
            var isBubble = data.Mode == AmbientMode.Bubble;
            var hasPosition = false;
            var screenPosition = Vector2.zero;

            if (isBubble && data.SourceTransform != null)
                hasPosition = TryResolveScreenPosition(data.SourceTransform, out screenPosition);

            return new AmbientNarrativeToolkitViewData
            {
                HasLine = true,
                UseBubble = isBubble,
                Speaker = data.Speaker ?? string.Empty,
                Body = data.DisplayText ?? string.Empty,
                ModeKey = data.Mode.ToString(),
                HasScreenPosition = hasPosition,
                BubbleScreenPosition = screenPosition
            };
        }

        private bool TryResolveScreenPosition(Transform sourceTransform, out Vector2 screenPosition)
        {
            screenPosition = Vector2.zero;

            var camera = ResolveWorldCamera();
            if (camera == null || sourceTransform == null)
                return false;

            var point = camera.WorldToScreenPoint(sourceTransform.position);
            if (point.z < 0f)
                return false;

            screenPosition = (Vector2)point + bubbleScreenOffset;
            return true;
        }

        private bool ShouldRefreshPosition(AmbientNarrativeToolkitViewData viewData)
        {
            if (!_lastViewVisible)
                return true;

            if (!viewData.UseBubble || !viewData.HasScreenPosition)
                return false;

            if (Vector2.SqrMagnitude(viewData.BubbleScreenPosition - _lastScreenPosition) <= 0.25f)
                return false;

            _lastScreenPosition = viewData.BubbleScreenPosition;
            return true;
        }

        private void ApplyEmptyView()
        {
            if (!EnsureUIManager(false))
                return;

            if (closeViewOnAmbientClosed)
                uiManager.CloseView(ambientViewId);
            else
                uiManager.RefreshView(ambientViewId, AmbientNarrativeToolkitViewData.Empty());
        }

        private void ResolveReferences(bool warn)
        {
            if (uiManager == null)
                uiManager = FindSceneObject<UIToolkitUIManager>();

            if (presenter == null)
            {
                presenter = GetComponent<DialoguePresenter>();
                if (presenter == null)
                    presenter = FindSceneObject<DialoguePresenter>();
            }

            if (worldCamera == null)
                TryResolveWorldCamera(true);

            if (!warn)
                return;

            if (uiManager == null)
                Warn("未绑定 UIToolkitUIManager，环境叙事 Toolkit View 无法打开或刷新。");

            if (presenter == null)
                Warn("未绑定 DialoguePresenter，无法接收环境叙事事件。");
        }

        private bool EnsureUIManager(bool logMissing = true)
        {
            if (uiManager != null)
                return true;

            uiManager = FindSceneObject<UIToolkitUIManager>();
            if (uiManager == null && logMissing)
                Warn("未找到 UIToolkitUIManager，请拖核心场景 UIRoot/UIManager 上的 UIToolkitUIManager。");

            return uiManager != null;
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
                Warn("WorldCamera 未绑定，且场景中没有 MainCamera，Bubble 模式无法跟随世界目标。");
            }
        }

        private void Warn(string message)
        {
            if (logWarnings && !string.IsNullOrWhiteSpace(message))
                Debug.LogWarning($"[NiumaGalToolkitAmbientNarrativeBridge] {message}", this);
        }

        private static T FindSceneObject<T>() where T : Object
        {
#if UNITY_2023_1_OR_NEWER
            return FindFirstObjectByType<T>();
#else
            return FindObjectOfType<T>();
#endif
        }
    }
}
