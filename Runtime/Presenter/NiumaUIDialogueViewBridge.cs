using NiumaUI.Core;
using NiumaUI.Enum;
using NiumaUI.Views.Dialogue;
using UnityEngine;

namespace NiumaGal.Presenter
{
    /// <summary>
    /// NiumaGal -> NiumaUI 的对话 UI 桥接层。
    /// 该类只做模块间方法调用，不实现对话播放逻辑，也不直接构建 UI 视觉。
    /// </summary>
    public sealed class NiumaUIDialogueViewBridge : MonoBehaviour
    {
        [SerializeField] private UIManager uiManager;
        [SerializeField] private DialoguePresenter presenter;
        [SerializeField] private string dialogueViewId = "DialogueWindow";
        [SerializeField] private bool switchUIMode = true;
        [SerializeField] private bool returnToGameplayOnClose = true;
        [SerializeField] private bool useTypewriterText = true;
        [SerializeField] private bool logWarnings = true;

        private string _speaker;
        private string _fullText;
        private string _displayText;
        private bool _showContinueHint;
        private bool _isShowing;
        private bool _dirty;
        private float _waitForViewTimer;
        private bool _reportedMissingView;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();

            if (presenter == null)
            {
                if (logWarnings)
                    Debug.LogWarning("[NiumaUIDialogueViewBridge] DialoguePresenter was not found.", this);
                return;
            }

            // 订阅 Gal 表现层事件，将剧情系统的刷新请求转交给 UI 模块。
            presenter.OnRefreshUI += HandleRefreshUI;
            presenter.OnSentenceTextCompleted += HandleSentenceTextCompleted;
            presenter.OnCloseUI += HandleCloseUI;
            presenter.OnHideUI += HandleHideUI;
        }

        private void OnDisable()
        {
            if (presenter == null)
                return;

            presenter.OnRefreshUI -= HandleRefreshUI;
            presenter.OnSentenceTextCompleted -= HandleSentenceTextCompleted;
            presenter.OnCloseUI -= HandleCloseUI;
            presenter.OnHideUI -= HandleHideUI;
        }

        private void Update()
        {
            if (!_isShowing)
                return;

            // UIManager 可能在下一帧才把 View 实例化出来，这里记录等待时间用于诊断。
            if (_dirty)
                _waitForViewTimer += Time.deltaTime;

            if (!useTypewriterText || presenter == null)
                return;

            // 打字机文本由 Gal 持有，桥接层只读取当前显示文本并转发给 UI View。
            var currentText = presenter.GetTypewriterDisplayText?.Invoke();
            if (currentText == null || currentText == _displayText)
                return;

            _displayText = currentText;
            _dirty = true;
        }

        private void LateUpdate()
        {
            TryApplyLine();
        }

        private void ResolveReferences()
        {
            if (presenter == null)
                presenter = GetComponent<DialoguePresenter>();

            if (uiManager == null)
                uiManager = FindObjectOfType<UIManager>();
        }

        private void HandleRefreshUI(string speaker, string text)
        {
            _speaker = speaker;
            _fullText = text;
            _displayText = useTypewriterText ? string.Empty : text;
            _showContinueHint = false;
            _isShowing = true;
            _dirty = true;
            _waitForViewTimer = 0f;
            _reportedMissingView = false;

            if (uiManager == null)
                ResolveReferences();

            if (uiManager == null)
            {
                if (logWarnings)
                    Debug.LogWarning("[NiumaUIDialogueViewBridge] UIManager was not found, dialogue UI cannot be shown.", this);
                return;
            }

            if (switchUIMode)
                uiManager.RequestMode(UIMode.Dialogue);

            // 只请求 UI 模块打开 View，具体创建和显示由 UIManager/Factory 负责。
            if (!uiManager.PushView(dialogueViewId) && logWarnings)
                Debug.LogWarning($"[NiumaUIDialogueViewBridge] PushView was rejected: {dialogueViewId}", this);
        }

        private void HandleSentenceTextCompleted()
        {
            _displayText = _fullText;
            _showContinueHint = true;
            _dirty = true;
            TryApplyLine();
        }

        private void HandleCloseUI()
        {
            _isShowing = false;
            _dirty = false;
            _waitForViewTimer = 0f;

            if (uiManager == null)
                return;

            uiManager.CloseViewById(dialogueViewId);

            if (returnToGameplayOnClose)
                uiManager.RequestMode(UIMode.Gameplay);
        }

        private void HandleHideUI()
        {
            if (uiManager == null)
                return;

            uiManager.CloseViewById(dialogueViewId);
        }

        private void TryApplyLine()
        {
            if (!_dirty || uiManager == null)
                return;

            if (!uiManager.TryGetView(dialogueViewId, out DialogueWindowView view))
            {
                if (logWarnings && !_reportedMissingView && _waitForViewTimer > 1f)
                {
                    _reportedMissingView = true;
                    Debug.LogWarning($"[NiumaUIDialogueViewBridge] Dialogue view is not active after {_waitForViewTimer:0.0}s: {dialogueViewId}", this);
                }

                return;
            }

            view.SetLine(_speaker, _displayText, _showContinueHint);
            _dirty = false;
            _waitForViewTimer = 0f;
        }
    }
}
