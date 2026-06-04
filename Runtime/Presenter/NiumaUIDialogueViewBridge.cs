using System;
using System.Collections.Generic;
using NiumaGal.Dialogue;
using NiumaGal.Dialogue.Data;
using NiumaGal.Dialogue.Service;
using NiumaGal.Enum;
using NiumaTPC.Cameras;
using NiumaUI.Core;
using NiumaUI.Enum;
using NiumaUI.Views.Dialogue;
using UnityEngine;

namespace NiumaGal.Presenter
{
    /// <summary>
    /// 将 NiumaGal 的对话运行时数据转换为 NiumaUI 对话窗口数据。
    /// </summary>
    public sealed class NiumaUIDialogueViewBridge : MonoBehaviour
    {
        [Header("模块引用")]
        [Tooltip("场景中的 UIManager。为空时会自动查找。")]
        [SerializeField] private UIManager uiManager;

        [Tooltip("场景中的 DialoguePresenter。为空时优先从当前物体查找。")]
        [SerializeField] private DialoguePresenter presenter;

        [Tooltip("场景中的 NiumaDialogueController。用于读取 DialogueService 和提交选项选择。为空时会自动查找。")]
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

        [Header("鼠标控制")]
        [Tooltip("对话期间鼠标显示策略。有选项时显示适合第三人称游戏，避免 TPC 锁鼠标导致按钮无法点击。")]
        [SerializeField] private DialogueCursorMode cursorMode = DialogueCursorMode.VisibleWhenChoices;

        [Tooltip("对话关闭或不再需要鼠标时，是否恢复进入 UI 前的鼠标状态。")]
        [SerializeField] private bool restoreCursorState = true;

        [Header("对话期间隐藏")]
        [Tooltip("进入对话时是否隐藏 TPC 的 OnGUI 准心。为空时可自动查找 PlayerCameraManager。")]
        [SerializeField] private bool hideTPCrosshairDuringDialogue = true;

        [Tooltip("TPC 摄像机管理器。用于关闭 PlayerCameraManager.ShowCrosshair，避免对话时仍显示准心。")]
        [SerializeField] private PlayerCameraManager playerCameraManager;

        [Tooltip("PlayerCameraManager 为空时是否自动查找。")]
        [SerializeField] private bool autoFindPlayerCameraManager = true;

        [Tooltip("进入对话时需要临时隐藏的 Gameplay UI 物体，例如准心、交互提示、小地图、任务追踪等。对话结束会恢复进入前的显示状态。")]
        [SerializeField] private GameObject[] hideObjectsDuringDialogue = Array.Empty<GameObject>();

        private DialogueWindowView _view;
        private IDialogueService _dialogueService;
        private long _observedDialogueRevision = -1;
        private int _forceViewDataRefreshFrames;
        private int _pendingViewResolveFrames;
        private bool _isShowing;
        private bool _dirty;
        private string _speakerName;
        private string _fullText;
        private string _displayText;
        private bool _showContinueHint;
        private DialogueChoiceOptionData[] _choices = Array.Empty<DialogueChoiceOptionData>();
        private readonly List<DialogueChoiceOptionData> _choiceBuffer = new List<DialogueChoiceOptionData>(4);

        private bool _cursorStateCaptured;
        private bool _previousCursorVisible;
        private CursorLockMode _previousCursorLockState;
        private bool _dialogueVisualSuppressionApplied;
        private bool _crosshairStateCaptured;
        private bool _previousCrosshairVisible;
        private bool[] _hideObjectPreviousStates = Array.Empty<bool>();

        private void Awake()
        {
            ResolveReferences(false);
        }

        private void OnEnable()
        {
            ResolveReferences(false);

            if (presenter != null)
            {
                presenter.OnRefreshUI += HandleRefreshUI;
                presenter.OnSentenceTextCompleted += HandleSentenceTextCompleted;
                presenter.OnCloseUI += HandleCloseUI;
                presenter.OnHideUI += HandleHideUI;
            }
            else if (logWarnings)
            {
                Debug.LogWarning("[NiumaUIDialogueViewBridge] 未找到 DialoguePresenter，对话 UI 无法刷新。", this);
            }
        }

        private void OnDisable()
        {
            if (presenter != null)
            {
                presenter.OnRefreshUI -= HandleRefreshUI;
                presenter.OnSentenceTextCompleted -= HandleSentenceTextCompleted;
                presenter.OnCloseUI -= HandleCloseUI;
                presenter.OnHideUI -= HandleHideUI;
            }

            RestoreCursorIfNeeded();
            RestoreDialogueVisualSuppression();
            ResetRuntimeState();
        }

        private void Update()
        {
            if (!_isShowing || _view == null)
            {
                return;
            }

            if (useTypewriterText && presenter != null && _isShowing)
            {
                var nextText = presenter.GetTypewriterDisplayText?.Invoke() ?? _fullText;
                if (!string.Equals(_displayText, nextText, StringComparison.Ordinal))
                {
                    _displayText = nextText;
                    _dirty = true;
                }
            }
        }

        private void LateUpdate()
        {
            ResolvePendingDialogueView();

            if (_forceViewDataRefreshFrames > 0)
            {
                _forceViewDataRefreshFrames--;
                _observedDialogueRevision = -1;
            }

            RefreshDialogueServiceViewDataIfNeeded();
            TryApplyLine();
        }

        private void ResolveReferences(bool warn)
        {
            if (uiManager == null)
            {
                uiManager = FindSceneObject<UIManager>();
            }

            if (presenter == null)
            {
                presenter = GetComponent<DialoguePresenter>();
                if (presenter == null)
                {
                    presenter = FindSceneObject<DialoguePresenter>();
                }
            }

            if (dialogueController == null)
            {
                dialogueController = FindSceneObject<NiumaDialogueController>();
            }

            if (playerCameraManager == null && autoFindPlayerCameraManager)
            {
                playerCameraManager = FindSceneObject<PlayerCameraManager>();
            }

            _dialogueService = dialogueController != null ? dialogueController.DialogueService : null;

            if (warn && logWarnings)
            {
                if (uiManager == null)
                {
                    Debug.LogWarning("[NiumaUIDialogueViewBridge] 未绑定 UIManager，无法打开 NiumaUI 对话窗口。", this);
                }

                if (dialogueController == null)
                {
                    Debug.LogWarning("[NiumaUIDialogueViewBridge] 未绑定 NiumaDialogueController，选项提交和 ViewData 刷新不可用。", this);
                }
            }
        }

        private void HandleRefreshUI(string speakerName, string content)
        {
            ResolveReferences(true);

            _speakerName = speakerName ?? string.Empty;
            _fullText = content ?? string.Empty;
            _displayText = useTypewriterText && presenter != null ? presenter.GetTypewriterDisplayText?.Invoke() ?? _fullText : _fullText;
            _showContinueHint = false;
            _choices = Array.Empty<DialogueChoiceOptionData>();
            _observedDialogueRevision = -1;
            _forceViewDataRefreshFrames = 1;
            _dirty = true;

            if (uiManager == null)
            {
                return;
            }

            if (switchUIMode)
            {
                uiManager.RequestMode(UIMode.Dialogue);
            }

            ApplyDialogueVisualSuppression();
            uiManager.PushView(dialogueViewId);
            _isShowing = true;
            _pendingViewResolveFrames = 4;
            TryResumeActiveDialogueView();
            if (_view == null && logWarnings && _pendingViewResolveFrames < 0)
            {
                Debug.LogWarning($"[NiumaUIDialogueViewBridge] ViewId={dialogueViewId} 没有注册 DialogueWindowView。", this);
            }

            _isShowing = true;
            UpdateCursorState();
        }

        private void HandleSentenceTextCompleted()
        {
            _displayText = _fullText ?? string.Empty;
            _showContinueHint = true;

            // DialoguePresenter 的文本完成事件可能早于 DialogueService 的等待选项状态刷新。
            // 这里强制连续刷新几帧，避免最后一句带选项时 UI 只显示继续提示、不显示选项。
            _observedDialogueRevision = -1;
            _forceViewDataRefreshFrames = 2;
            _dirty = true;

            TryApplyLine();
        }

        private void HandleCloseUI()
        {
            if (uiManager != null && !string.IsNullOrWhiteSpace(dialogueViewId))
            {
                uiManager.CloseViewById(dialogueViewId);
            }

            if (returnToGameplayOnClose && uiManager != null)
            {
                uiManager.RequestMode(UIMode.Gameplay);
            }

            RestoreCursorIfNeeded();
            RestoreDialogueVisualSuppression();
            ResetRuntimeState();
        }

        private void HandleHideUI()
        {
            if (uiManager != null && !string.IsNullOrWhiteSpace(dialogueViewId))
            {
                uiManager.CloseViewById(dialogueViewId);
            }

            RestoreCursorIfNeeded();
            RestoreDialogueVisualSuppression();
            ResetRuntimeState();
        }

        private void ApplyDialogueVisualSuppression()
        {
            if (_dialogueVisualSuppressionApplied)
            {
                return;
            }

            _dialogueVisualSuppressionApplied = true;

            if (hideTPCrosshairDuringDialogue && playerCameraManager != null)
            {
                _previousCrosshairVisible = playerCameraManager.ShowCrosshair;
                _crosshairStateCaptured = true;
                playerCameraManager.ShowCrosshair = false;
            }

            var objects = hideObjectsDuringDialogue ?? Array.Empty<GameObject>();
            _hideObjectPreviousStates = new bool[objects.Length];
            for (var i = 0; i < objects.Length; i++)
            {
                var target = objects[i];
                if (target == null)
                {
                    continue;
                }

                _hideObjectPreviousStates[i] = target.activeSelf;
                target.SetActive(false);
            }
        }

        private void RestoreDialogueVisualSuppression()
        {
            if (!_dialogueVisualSuppressionApplied)
            {
                return;
            }

            if (_crosshairStateCaptured && playerCameraManager != null)
            {
                playerCameraManager.ShowCrosshair = _previousCrosshairVisible;
            }

            var objects = hideObjectsDuringDialogue ?? Array.Empty<GameObject>();
            var states = _hideObjectPreviousStates ?? Array.Empty<bool>();
            var count = Mathf.Min(objects.Length, states.Length);
            for (var i = 0; i < count; i++)
            {
                if (objects[i] != null)
                {
                    objects[i].SetActive(states[i]);
                }
            }

            _dialogueVisualSuppressionApplied = false;
            _crosshairStateCaptured = false;
            _hideObjectPreviousStates = Array.Empty<bool>();
        }

        private void RefreshDialogueServiceViewDataIfNeeded()
        {
            if (dialogueController == null || dialogueController.DialogueService == null)
            {
                ResolveReferences(false);
            }

            _dialogueService = dialogueController != null ? dialogueController.DialogueService : null;
            if (_dialogueService == null)
            {
                return;
            }

            var revision = _dialogueService.Revision;
            if (_observedDialogueRevision == revision)
            {
                return;
            }

            _observedDialogueRevision = revision;
            var viewData = _dialogueService.BuildViewData();
            ApplyDialogueViewData(viewData);
        }

        private void ApplyDialogueViewData(DialogueViewData viewData)
        {
            if (viewData == null || !_dialogueService.IsDialoguePlaying)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(viewData.Speaker))
            {
                _speakerName = viewData.Speaker;
            }

            if (!string.IsNullOrEmpty(viewData.FullText))
            {
                _fullText = viewData.FullText;
                if (!useTypewriterText || presenter == null || !_isShowing)
                {
                    _displayText = viewData.FullText;
                }
            }

            // 选项必须等当前句文本显示完成后再出现，避免玩家没看完台词就被迫选择。
            var canShowChoices = viewData.ShowContinueHint && viewData.IsWaitingChoice;
            _choices = canShowChoices
                ? BuildChoiceOptions(viewData.Choices)
                : Array.Empty<DialogueChoiceOptionData>();

            _showContinueHint = viewData.ShowContinueHint && _choices.Length == 0;
            UpdateCursorState();
            _dirty = true;
        }

        private DialogueChoiceOptionData[] BuildChoiceOptions(DialogueChoiceViewData[] choices)
        {
            if (choices == null || choices.Length == 0)
            {
                return Array.Empty<DialogueChoiceOptionData>();
            }

            _choiceBuffer.Clear();
            for (var i = 0; i < choices.Length; i++)
            {
                var choice = choices[i];
                if (choice == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(choice.ChoiceId))
                {
                    if (logWarnings)
                    {
                        Debug.LogWarning("[NiumaUIDialogueViewBridge] 发现 ChoiceId 为空的对话选项，已跳过。请在 DialogueAsset 中填写稳定的选项 ID。", this);
                    }

                    continue;
                }

                var displayText = choice.IsAvailable
                    ? choice.DisplayText
                    : (!string.IsNullOrWhiteSpace(choice.DisabledText) ? choice.DisabledText : choice.DisplayText);

                _choiceBuffer.Add(new DialogueChoiceOptionData
                {
                    ChoiceId = choice.ChoiceId,
                    DisplayText = displayText,
                    DisabledText = choice.DisabledText,
                    IsAvailable = choice.IsAvailable,
                    OnSelected = HandleChoiceSelected
                });
            }

            return _choiceBuffer.Count > 0 ? _choiceBuffer.ToArray() : Array.Empty<DialogueChoiceOptionData>();
        }

        private void HandleChoiceSelected(string choiceId)
        {
            if (dialogueController == null && !ResolveDialogueControllerForChoice())
            {
                return;
            }

            var result = dialogueController.SelectChoice(choiceId, null, nameof(NiumaUIDialogueViewBridge));
            if (result == null || !result.Succeeded)
            {
                if (logWarnings)
                {
                    Debug.LogWarning($"[NiumaUIDialogueViewBridge] 选择对话选项失败：ChoiceId={choiceId}，原因={result?.Message}", this);
                }

                return;
            }

            _choices = Array.Empty<DialogueChoiceOptionData>();
            _showContinueHint = false;
            _observedDialogueRevision = -1;
            _forceViewDataRefreshFrames = 1;
            _dirty = true;
            UpdateCursorState();
        }

        private bool ResolveDialogueControllerForChoice()
        {
            ResolveReferences(true);
            return dialogueController != null;
        }

        private void TryApplyLine()
        {
            if (!_dirty || !_isShowing || _view == null)
            {
                if (_dirty && _isShowing && _view == null)
                {
                    TryResumeActiveDialogueView();
                }

                if (!_dirty || !_isShowing || _view == null)
                {
                    return;
                }
            }

            _dirty = false;
            _view.SetLine(_speakerName ?? string.Empty, _displayText ?? string.Empty, _showContinueHint);
            _view.SetChoices(_choices);
        }

        private void TryResumeActiveDialogueView()
        {
            if (uiManager == null || string.IsNullOrWhiteSpace(dialogueViewId))
            {
                return;
            }

            if (uiManager.TryGetView<DialogueWindowView>(dialogueViewId, out var view))
            {
                _view = view;
                _isShowing = true;
            }
        }

        private void ResolvePendingDialogueView()
        {
            if (!_isShowing || _view != null || _pendingViewResolveFrames <= 0)
            {
                return;
            }

            _pendingViewResolveFrames--;
            TryResumeActiveDialogueView();

            if (_view != null)
            {
                _dirty = true;
                return;
            }

            if (_pendingViewResolveFrames == 0 && logWarnings)
            {
                Debug.LogWarning($"[NiumaUIDialogueViewBridge] ViewId={dialogueViewId} 没有拿到 DialogueWindowView。请检查 UIManager 的 ViewFactory 是否注册了 DialogueWindowBinding，或 Binding 的 ViewId 是否等于 DialogueWindow。", this);
            }
        }

        private void ResetRuntimeState()
        {
            _view = null;
            _isShowing = false;
            _dirty = false;
            _speakerName = string.Empty;
            _fullText = string.Empty;
            _displayText = string.Empty;
            _showContinueHint = false;
            _choices = Array.Empty<DialogueChoiceOptionData>();
            _observedDialogueRevision = -1;
            _forceViewDataRefreshFrames = 0;
            _pendingViewResolveFrames = 0;
        }

        private void UpdateCursorState()
        {
            switch (cursorMode)
            {
                case DialogueCursorMode.DoNotControl:
                    return;
                case DialogueCursorMode.VisibleDuringDialogue:
                    if (_isShowing)
                    {
                        ShowCursorForDialogue();
                    }
                    else
                    {
                        RestoreCursorIfNeeded();
                    }
                    return;
                case DialogueCursorMode.VisibleWhenChoices:
                    if (_isShowing && _choices.Length > 0)
                    {
                        ShowCursorForDialogue();
                    }
                    else
                    {
                        RestoreCursorIfNeeded();
                    }
                    return;
                default:
                    return;
            }
        }

        private void ShowCursorForDialogue()
        {
            if (!_cursorStateCaptured)
            {
                _previousCursorVisible = Cursor.visible;
                _previousCursorLockState = Cursor.lockState;
                _cursorStateCaptured = true;
            }

            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        private void RestoreCursorIfNeeded()
        {
            if (!_cursorStateCaptured)
            {
                return;
            }

            if (restoreCursorState)
            {
                Cursor.visible = _previousCursorVisible;
                Cursor.lockState = _previousCursorLockState;
            }

            _cursorStateCaptured = false;
        }

        private static T FindSceneObject<T>() where T : UnityEngine.Object
        {
#if UNITY_2023_1_OR_NEWER
            return FindFirstObjectByType<T>();
#else
            return FindObjectOfType<T>();
#endif
        }
    }

    /// <summary>
    /// 对话期间鼠标显示策略。
    /// </summary>
    public enum DialogueCursorMode
    {
        [Tooltip("不由对话桥接层控制鼠标。")]
        DoNotControl = 0,

        [Tooltip("整个对话期间都显示并解锁鼠标。")]
        VisibleDuringDialogue = 1,

        [Tooltip("只有出现选项时显示并解锁鼠标。")]
        VisibleWhenChoices = 2
    }
}
