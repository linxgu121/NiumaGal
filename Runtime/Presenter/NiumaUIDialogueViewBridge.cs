using System;
using NiumaGal.Dialogue;
using NiumaGal.Dialogue.Data;
using NiumaGal.Enum;
using NiumaUI.Core;
using NiumaUI.Enum;
using NiumaUI.Views.Dialogue;
using UnityEngine;

namespace NiumaGal.Presenter
{
    /// <summary>
    /// NiumaGal 到 NiumaUI 的对话 UI 桥接层。
    /// 正文仍由 DialoguePresenter 事件驱动，选项则从 DialogueService 的 ViewData 中读取。
    /// </summary>
    public sealed class NiumaUIDialogueViewBridge : MonoBehaviour
    {
        [Header("模块引用")]
        [Tooltip("场景中的 UIManager。为空时会自动查找。")]
        [SerializeField] private UIManager uiManager;
        [Tooltip("场景中的 DialoguePresenter。为空时优先从当前物体查找。")]
        [SerializeField] private DialoguePresenter presenter;
        [Tooltip("场景中的 NiumaDialogueController。用于读取 DialogueService 和回调选项选择。为空时会自动查找。")]
        [SerializeField] private NiumaDialogueController dialogueController;

        [Header("视图配置")]
        [Tooltip("NiumaUI 中注册的对话窗口 ViewId。")]
        [SerializeField] private string dialogueViewId = "DialogueWindow";
        [Tooltip("打开正式对话时是否切换到 Dialogue UI 模式。")]
        [SerializeField] private bool switchUIMode = true;
        [Tooltip("对话关闭后是否切回 Gameplay UI 模式。")]
        [SerializeField] private bool returnToGameplayOnClose = true;
        [Tooltip("是否从 DialoguePresenter 读取打字机当前显示文本。关闭后直接显示完整文本。")]
        [SerializeField] private bool useTypewriterText = true;
        [Tooltip("引用缺失或 View 未就绪时是否输出警告。")]
        [SerializeField] private bool logWarnings = true;

        private string _speaker;
        private string _fullText;
        private string _displayText;
        private bool _showContinueHint;
        private DialogueChoiceOptionData[] _choices = Array.Empty<DialogueChoiceOptionData>();
        private bool _isShowing;
        private bool _dirty;
        private float _waitForViewTimer;
        private bool _reportedMissingView;
        private long _observedDialogueRevision = -1;

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
                    Debug.LogWarning("[NiumaUIDialogueViewBridge] DialoguePresenter 未找到，对话 UI 无法刷新。", this);
                return;
            }

            presenter.OnRefreshUI += HandleRefreshUI;
            presenter.OnSentenceTextCompleted += HandleSentenceTextCompleted;
            presenter.OnCloseUI += HandleCloseUI;
            presenter.OnHideUI += HandleHideUI;
            _observedDialogueRevision = -1;
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

            if (_dirty)
                _waitForViewTimer += Time.deltaTime;

            if (!useTypewriterText || presenter == null)
                return;

            var currentText = presenter.GetTypewriterDisplayText?.Invoke();
            if (currentText == null || currentText == _displayText)
                return;

            _displayText = currentText;
            _dirty = true;
        }

        private void LateUpdate()
        {
            RefreshDialogueServiceViewDataIfNeeded();
            TryApplyLine();
        }

        private void ResolveReferences()
        {
            if (presenter == null)
                presenter = GetComponent<DialoguePresenter>();

            if (dialogueController == null)
                dialogueController = GetComponent<NiumaDialogueController>() ?? FindObjectOfType<NiumaDialogueController>();

            if (uiManager == null)
                uiManager = FindObjectOfType<UIManager>();
        }

        private void HandleRefreshUI(string speaker, string text)
        {
            _speaker = speaker;
            _fullText = text;
            _displayText = useTypewriterText ? string.Empty : text;
            _showContinueHint = false;
            _choices = Array.Empty<DialogueChoiceOptionData>();
            _isShowing = true;
            _dirty = true;
            _waitForViewTimer = 0f;
            _reportedMissingView = false;
            _observedDialogueRevision = -1;

            if (uiManager == null)
                ResolveReferences();

            if (uiManager == null)
            {
                if (logWarnings)
                    Debug.LogWarning("[NiumaUIDialogueViewBridge] UIManager 未找到，对话 UI 无法显示。", this);
                return;
            }

            if (switchUIMode)
                uiManager.RequestMode(UIMode.Dialogue);

            if (!uiManager.PushView(dialogueViewId) && logWarnings)
                Debug.LogWarning($"[NiumaUIDialogueViewBridge] PushView 被拒绝：{dialogueViewId}", this);
        }

        private void HandleSentenceTextCompleted()
        {
            _displayText = _fullText;
            _showContinueHint = true;
            _observedDialogueRevision = -1;
            _dirty = true;
            TryApplyLine();
        }

        private void HandleCloseUI()
        {
            _isShowing = false;
            _dirty = false;
            _waitForViewTimer = 0f;
            _choices = Array.Empty<DialogueChoiceOptionData>();
            _observedDialogueRevision = -1;

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

        private void RefreshDialogueServiceViewDataIfNeeded()
        {
            if (!_isShowing || dialogueController?.DialogueService == null)
                return;

            var revision = dialogueController.DialogueService.Revision;
            if (_observedDialogueRevision == revision)
                return;

            var viewData = dialogueController.DialogueService.BuildViewData();
            _observedDialogueRevision = revision;
            ApplyDialogueViewData(viewData);
        }

        private void ApplyDialogueViewData(DialogueViewData viewData)
        {
            if (viewData == null)
                return;

            _speaker = viewData.Speaker;
            _fullText = viewData.FullText;

            if (!useTypewriterText && !string.IsNullOrEmpty(viewData.DisplayText))
                _displayText = viewData.DisplayText;

            _choices = BuildChoiceOptions(viewData.Choices);
            _showContinueHint = viewData.ShowContinueHint && _choices.Length == 0;
            _dirty = true;
        }

        private DialogueChoiceOptionData[] BuildChoiceOptions(DialogueChoiceViewData[] choices)
        {
            if (choices == null || choices.Length == 0)
                return Array.Empty<DialogueChoiceOptionData>();

            var result = new DialogueChoiceOptionData[choices.Length];
            for (var i = 0; i < choices.Length; i++)
            {
                var choice = choices[i];
                result[i] = new DialogueChoiceOptionData
                {
                    ChoiceId = choice?.ChoiceId,
                    DisplayText = choice?.DisplayText,
                    IsAvailable = choice != null && choice.IsAvailable,
                    DisabledText = choice?.DisabledText,
                    OnSelected = HandleChoiceSelected
                };
            }

            return result;
        }

        private void HandleChoiceSelected(string choiceId)
        {
            if (dialogueController == null)
                ResolveReferences();

            if (dialogueController == null)
            {
                if (logWarnings)
                    Debug.LogWarning("[NiumaUIDialogueViewBridge] NiumaDialogueController 未找到，无法提交对话选项。", this);
                return;
            }

            var result = dialogueController.SelectChoice(choiceId, null, nameof(NiumaUIDialogueViewBridge));
            if (result == null || !result.Succeeded)
            {
                if (logWarnings)
                {
                    var reason = result == null ? DialogueOperationFailureReason.Unknown : result.FailureReason;
                    var message = result == null ? "选项提交返回空结果。" : result.Message;
                    Debug.LogWarning($"[NiumaUIDialogueViewBridge] 选项提交失败：{reason} {message}", this);
                }

                return;
            }

            _choices = Array.Empty<DialogueChoiceOptionData>();
            _showContinueHint = false;
            _observedDialogueRevision = -1;
            _dirty = true;
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
                    Debug.LogWarning($"[NiumaUIDialogueViewBridge] 对话 View 超过 {_waitForViewTimer:0.0}s 仍未激活：{dialogueViewId}", this);
                }

                return;
            }

            view.SetLine(_speaker, _displayText, _showContinueHint);
            view.SetChoices(_choices);
            _dirty = false;
            _waitForViewTimer = 0f;
        }
    }
}
