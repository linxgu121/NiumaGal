using System;
using System.Collections.Generic;
using NiumaGal.Dialogue;
using NiumaGal.Dialogue.Data;
using NiumaGal.Dialogue.Service;
using NiumaGal.Presenter;
using NiumaUI.Toolkit;
using NiumaUI.Views.Dialogue;
using UnityEngine;

namespace NiumaGal.ToolkitBridge
{
    /// <summary>
    /// NiumaGal 到 NiumaUI Toolkit 对话窗口的桥接脚本。
    /// 挂在 DialogueRoot 或 UIRoot/UIBridges，负责把 DialogueService 的 ViewData 推给 DialogueToolkitBinding。
    /// </summary>
    public sealed class NiumaGalToolkitDialogueViewBridge : MonoBehaviour
    {
        [Header("模块引用")]
        [Tooltip("UI Toolkit 根控制器。拖核心场景 UIRoot/UIManager 上的 UIToolkitUIManager。")]
        [SerializeField] private UIToolkitUIManager uiManager;

        [Tooltip("对话表现播放器。拖 DialogueRoot 上的 DialoguePresenter，用于接收刷新、关闭事件和读取打字机当前文本。")]
        [SerializeField] private DialoguePresenter presenter;

        [Tooltip("对话根控制器。拖 DialogueRoot 上的 NiumaDialogueController，用于读取 DialogueService 和提交选项选择。")]
        [SerializeField] private NiumaDialogueController dialogueController;

        [Header("Toolkit View")]
        [Tooltip("UI Toolkit 注册表中的对话窗口 ViewId。需要在 UIToolkitViewRegistrySO 中注册同名 View。")]
        [SerializeField] private string dialogueViewId = "DialogueWindow";

        [Tooltip("对话开始时是否自动打开 Toolkit 对话窗口。关闭后需要外部先打开该 View，本脚本只刷新数据。")]
        [SerializeField] private bool autoOpenView = true;

        [Tooltip("对话关闭或隐藏时是否自动关闭 Toolkit 对话窗口。")]
        [SerializeField] private bool closeViewOnDialogueClose = true;

        [Tooltip("是否从 DialoguePresenter 读取打字机当前显示文本。关闭后直接显示完整句子。")]
        [SerializeField] private bool useTypewriterText = true;

        [Header("鼠标控制")]
        [Tooltip("对话期间鼠标显示策略。第三人称项目推荐 VisibleWhenChoices，只有出现选项时解锁鼠标。")]
        [SerializeField] private DialogueCursorMode cursorMode = DialogueCursorMode.VisibleWhenChoices;

        [Tooltip("对话关闭或不再需要鼠标时，是否恢复进入 UI 前的鼠标状态。")]
        [SerializeField] private bool restoreCursorState = true;

        [Header("对话期间隐藏")]
        [Tooltip("进入对话时需要临时隐藏的 Gameplay UI 物体，例如准心、交互提示、小地图、任务追踪等。对话结束会恢复进入前的显示状态。")]
        [SerializeField] private GameObject[] hideObjectsDuringDialogue = Array.Empty<GameObject>();

        [Header("调试")]
        [Tooltip("引用缺失、ViewId 未注册、选项配置缺失时是否输出警告。")]
        [SerializeField] private bool logWarnings = true;

        private IDialogueService _dialogueService;
        private long _observedDialogueRevision = -1;
        private int _forceViewDataRefreshFrames;
        private bool _isShowing;
        private bool _dirty;
        private string _speakerName = string.Empty;
        private string _fullText = string.Empty;
        private string _displayText = string.Empty;
        private bool _showContinueHint;
        private DialogueChoiceOptionData[] _choices = Array.Empty<DialogueChoiceOptionData>();
        private readonly List<DialogueChoiceOptionData> _choiceBuffer = new List<DialogueChoiceOptionData>(4);

        private bool _cursorStateCaptured;
        private bool _previousCursorVisible;
        private CursorLockMode _previousCursorLockState;
        private bool _dialogueVisualSuppressionApplied;
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
            else
            {
                Warn("未绑定 DialoguePresenter，Toolkit 对话窗口无法收到对话刷新事件。");
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

            CloseToolkitViewIfNeeded();
            RestoreCursorIfNeeded();
            RestoreDialogueVisualSuppression();
            ResetRuntimeState();
        }

        private void Update()
        {
            if (!_isShowing || !useTypewriterText || presenter == null)
            {
                return;
            }

            var nextText = presenter.GetTypewriterDisplayText?.Invoke() ?? _fullText;
            if (!string.Equals(_displayText, nextText, StringComparison.Ordinal))
            {
                _displayText = nextText;
                _dirty = true;
            }
        }

        private void LateUpdate()
        {
            if (_forceViewDataRefreshFrames > 0)
            {
                _forceViewDataRefreshFrames--;
                _observedDialogueRevision = -1;
            }

            RefreshDialogueServiceViewDataIfNeeded();
            TryApplyViewData();
        }

        private void ResolveReferences(bool warn)
        {
            if (uiManager == null)
            {
                uiManager = FindSceneObject<UIToolkitUIManager>();
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

            _dialogueService = dialogueController != null ? dialogueController.DialogueService : null;

            if (!warn)
            {
                return;
            }

            if (uiManager == null)
            {
                Warn("未绑定 UIToolkitUIManager，无法打开或刷新 Toolkit 对话窗口。");
            }

            if (dialogueController == null)
            {
                Warn("未绑定 NiumaDialogueController，选项提交和 DialogueService 查询不可用。");
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
            _isShowing = true;

            ApplyDialogueVisualSuppression();
            UpdateCursorState();
            TryApplyViewData();
        }

        private void HandleSentenceTextCompleted()
        {
            _displayText = _fullText ?? string.Empty;
            _showContinueHint = true;
            _observedDialogueRevision = -1;
            _forceViewDataRefreshFrames = 2;
            _dirty = true;
            UpdateCursorState();
            TryApplyViewData();
        }

        private void HandleCloseUI()
        {
            CloseToolkitViewIfNeeded();
            RestoreCursorIfNeeded();
            RestoreDialogueVisualSuppression();
            ResetRuntimeState();
        }

        private void HandleHideUI()
        {
            CloseToolkitViewIfNeeded();
            RestoreCursorIfNeeded();
            RestoreDialogueVisualSuppression();
            ResetRuntimeState();
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
            ApplyDialogueViewData(_dialogueService.BuildViewData());
        }

        private void ApplyDialogueViewData(DialogueViewData viewData)
        {
            if (viewData == null || _dialogueService == null || !_dialogueService.IsDialoguePlaying)
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

            var canShowChoices = viewData.ShowContinueHint && viewData.IsWaitingChoice;
            _choices = canShowChoices
                ? BuildChoiceOptions(viewData.Choices)
                : Array.Empty<DialogueChoiceOptionData>();

            _showContinueHint = viewData.ShowContinueHint && _choices.Length == 0;
            _dirty = true;
            UpdateCursorState();
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
                    Warn("发现 ChoiceId 为空的对话选项，已跳过。请在 DialogueAsset 中填写稳定的选项 ID。");
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
            if (dialogueController == null)
            {
                ResolveReferences(true);
            }

            if (dialogueController == null)
            {
                return;
            }

            var result = dialogueController.SelectChoice(choiceId, null, nameof(NiumaGalToolkitDialogueViewBridge));
            if (result == null || !result.Succeeded)
            {
                Warn($"选择对话选项失败：ChoiceId={choiceId}，原因={result?.Message}");
                return;
            }

            _choices = Array.Empty<DialogueChoiceOptionData>();
            _showContinueHint = false;
            _observedDialogueRevision = -1;
            _forceViewDataRefreshFrames = 1;
            _dirty = true;
            UpdateCursorState();
        }

        private void TryApplyViewData()
        {
            if (!_dirty || !_isShowing)
            {
                return;
            }

            if (uiManager == null)
            {
                ResolveReferences(true);
            }

            if (uiManager == null || string.IsNullOrWhiteSpace(dialogueViewId))
            {
                return;
            }

            var data = new DialogueToolkitViewData
            {
                Speaker = _speakerName ?? string.Empty,
                Body = _displayText ?? string.Empty,
                ShowContinueHint = _showContinueHint,
                Choices = _choices ?? Array.Empty<DialogueChoiceOptionData>()
            };

            var applied = _isShowing && uiManager.RefreshView(dialogueViewId, data);
            if (!applied && autoOpenView)
            {
                applied = uiManager.OpenView(dialogueViewId, data);
            }

            if (!applied)
            {
                Warn($"没有刷新到 Toolkit 对话窗口：ViewId={dialogueViewId}。请检查 UIToolkitViewRegistrySO 和 DialogueToolkitBindingProvider。");
                return;
            }

            _dirty = false;
        }

        private void CloseToolkitViewIfNeeded()
        {
            if (closeViewOnDialogueClose && uiManager != null && !string.IsNullOrWhiteSpace(dialogueViewId))
            {
                uiManager.CloseView(dialogueViewId);
            }
        }

        private void ApplyDialogueVisualSuppression()
        {
            if (_dialogueVisualSuppressionApplied)
            {
                return;
            }

            _dialogueVisualSuppressionApplied = true;
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
            _hideObjectPreviousStates = Array.Empty<bool>();
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

        private void ResetRuntimeState()
        {
            _isShowing = false;
            _dirty = false;
            _speakerName = string.Empty;
            _fullText = string.Empty;
            _displayText = string.Empty;
            _showContinueHint = false;
            _choices = Array.Empty<DialogueChoiceOptionData>();
            _observedDialogueRevision = -1;
            _forceViewDataRefreshFrames = 0;
        }

        private void Warn(string message)
        {
            if (logWarnings)
            {
                Debug.LogWarning($"[NiumaGalToolkitDialogueViewBridge] {message}", this);
            }
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
}
