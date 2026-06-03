using System;
using System.Collections.Generic;
using NiumaGal.Dialogue.Arbitration;
using NiumaGal.Dialogue.Data;
using NiumaGal.Dialogue.RuntimeData;
using NiumaGal.Enum;
using NiumaGal.Save;
using UnityEngine;

namespace NiumaGal.Dialogue.Service
{
    /// <summary>
    /// Gal 对话核心服务。
    /// 该层负责对话资产检索、条件判断、行为分发、选项裁决和进度快照转换。
    /// 具体 UI、音频、玩家输入阻塞仍由 Controller / Presenter / 外部桥接层处理。
    /// </summary>
    public sealed class DialogueService : IDialogueService, IDialogueConfigurationService
    {
        private readonly NiumaGalBlackboard _blackboard;
        private readonly GalArbiter _arbiter;
        private readonly Action _ensureRuntimeReady;
        private readonly Action<DialogueAsset> _onDialogueAssetSelected;
        private readonly Dictionary<string, DialogueAsset> _dialogueAssetMap = new Dictionary<string, DialogueAsset>(StringComparer.Ordinal);
        private NiumaGalProgressStore _progressStore;
        private IDialogueConditionResolver _conditionResolver;
        private IDialogueActionHandler _actionHandler;
        private long _revision;
        private string _currentActorId;
        private string _currentSourceModule;

        public DialogueService(
            NiumaGalBlackboard blackboard,
            GalArbiter arbiter,
            NiumaGalProgressStore progressStore,
            DialogueAsset[] dialogueAssets,
            Action ensureRuntimeReady = null,
            Action<DialogueAsset> onDialogueAssetSelected = null,
            IDialogueConditionResolver conditionResolver = null,
            IDialogueActionHandler actionHandler = null)
        {
            _blackboard = blackboard;
            _arbiter = arbiter;
            _progressStore = progressStore;
            _ensureRuntimeReady = ensureRuntimeReady;
            _onDialogueAssetSelected = onDialogueAssetSelected;
            _conditionResolver = conditionResolver;
            _actionHandler = actionHandler;

            SetDialogueAssets(dialogueAssets);
            BindBlackboardRevisionEvents();
        }

        public long Revision => _revision;
        public bool IsDialoguePlaying => _blackboard != null && _blackboard.InteractionState != InteractionState.Idle;
        public string CurrentDialogueId => ResolveDialogueId(_blackboard?.CurrentDialogue);
        public int CurrentSentenceIndex => _blackboard?.CurrentSentenceIndex ?? -1;

        public DialogueOperationResult StartDialogue(DialogueStartRequest request)
        {
            if (request == null)
            {
                return Fail(DialogueOperationFailureReason.InvalidRequest, "启动对话请求为空。");
            }

            EnsureRuntimeReady();

            var asset = ResolveDialogueAsset(request);
            if (asset == null)
            {
                return Fail(DialogueOperationFailureReason.DialogueNotFound, "未找到对话资产。", request.DialogueId);
            }

            var dialogueId = ResolveDialogueId(asset);
            if (!request.RestartIfPlaying && IsDialoguePlaying)
            {
                return Fail(DialogueOperationFailureReason.DialogueAlreadyPlaying, "当前已有对话正在播放。", dialogueId);
            }

            var sentenceIndex = ResolveStartSentenceIndex(asset, request.StartSentenceId);
            if (sentenceIndex < 0)
            {
                return Fail(DialogueOperationFailureReason.SentenceNotFound, "未找到起始句子。", dialogueId, request.StartSentenceId);
            }

            var sentence = asset.Sentences[sentenceIndex];
            if (!AreConditionsMet(asset, sentence, null, sentence.Conditions, request.ActorId, request.SourceModule))
            {
                return Fail(DialogueOperationFailureReason.ConditionBlocked, "起始句子条件未满足。", dialogueId, ResolveSentenceId(sentence, sentenceIndex));
            }

            var actionResult = ExecuteActions(asset, sentence, null, asset.OnStartActions, request.ActorId, request.SourceModule);
            if (!actionResult.Succeeded)
            {
                return actionResult;
            }

            actionResult = ExecuteActions(asset, sentence, null, sentence.EnterActions, request.ActorId, request.SourceModule);
            if (!actionResult.Succeeded)
            {
                return actionResult;
            }

            _currentActorId = request.ActorId;
            _currentSourceModule = request.SourceModule;
            _onDialogueAssetSelected?.Invoke(asset);
            _arbiter.StartDialogue(asset, sentenceIndex);
            BumpRevision();
            return DialogueOperationResult.Success(dialogueId, ResolveSentenceId(sentence, sentenceIndex));
        }

        public DialogueOperationResult Advance(DialogueAdvanceRequest request)
        {
            EnsureRuntimeReady();

            if (!IsDialoguePlaying)
            {
                return Fail(DialogueOperationFailureReason.DialogueNotPlaying, "当前没有正在播放的对话。");
            }

            _currentActorId = request?.ActorId ?? _currentActorId;
            _currentSourceModule = request?.SourceModule ?? _currentSourceModule;

            if (_blackboard.LineState == LineState.Playing)
            {
                _arbiter.ProcessInput(new InputRequest(InputCommand.Advance));
                return DialogueOperationResult.Success(CurrentDialogueId, CurrentSentenceId);
            }

            if (_blackboard.ScriptState != DialogueScriptState.BetweenSentences)
            {
                _arbiter.ProcessInput(new InputRequest(InputCommand.Advance));
                return DialogueOperationResult.Success(CurrentDialogueId, CurrentSentenceId);
            }

            var currentSentence = CurrentSentence;
            if (currentSentence == null)
            {
                return Fail(DialogueOperationFailureReason.SentenceNotFound, "当前句子不存在。", CurrentDialogueId);
            }

            var visibleChoices = BuildChoiceViewData(currentSentence);
            if (visibleChoices.Length > 0)
            {
                return Fail(DialogueOperationFailureReason.ChoiceUnavailable, "当前句子正在等待选项选择。", CurrentDialogueId, CurrentSentenceId);
            }

            var exitResult = ExecuteActions(_blackboard.CurrentDialogue, currentSentence, null, currentSentence.ExitActions, _currentActorId, _currentSourceModule);
            if (!exitResult.Succeeded)
            {
                return exitResult;
            }

            var nextIndex = _blackboard.CurrentSentenceIndex + 1;
            if (nextIndex >= _blackboard.CurrentDialogue.Sentences.Count)
            {
                var completeResult = ExecuteActions(_blackboard.CurrentDialogue, currentSentence, null, _blackboard.CurrentDialogue.OnCompleteActions, _currentActorId, _currentSourceModule);
                if (!completeResult.Succeeded)
                {
                    return completeResult;
                }

                _arbiter.ProcessInput(new InputRequest(InputCommand.Advance));
                BumpRevision();
                return DialogueOperationResult.Success(CurrentDialogueId, CurrentSentenceId);
            }

            var nextSentence = _blackboard.CurrentDialogue.Sentences[nextIndex];
            if (!AreConditionsMet(_blackboard.CurrentDialogue, nextSentence, null, nextSentence.Conditions, _currentActorId, _currentSourceModule))
            {
                return Fail(DialogueOperationFailureReason.ConditionBlocked, "下一句条件未满足。", CurrentDialogueId, ResolveSentenceId(nextSentence, nextIndex));
            }

            var enterResult = ExecuteActions(_blackboard.CurrentDialogue, nextSentence, null, nextSentence.EnterActions, _currentActorId, _currentSourceModule);
            if (!enterResult.Succeeded)
            {
                return enterResult;
            }

            _arbiter.ProcessInput(new InputRequest(InputCommand.Advance));
            BumpRevision();
            return DialogueOperationResult.Success(CurrentDialogueId, ResolveSentenceId(nextSentence, nextIndex));
        }

        public DialogueOperationResult SelectChoice(DialogueChoiceSelectRequest request)
        {
            EnsureRuntimeReady();

            if (request == null || string.IsNullOrWhiteSpace(request.ChoiceId))
            {
                return Fail(DialogueOperationFailureReason.InvalidRequest, "选择请求或 ChoiceId 为空。", CurrentDialogueId, CurrentSentenceId);
            }

            if (!IsDialoguePlaying)
            {
                return Fail(DialogueOperationFailureReason.DialogueNotPlaying, "当前没有正在播放的对话。");
            }

            var sentence = CurrentSentence;
            if (sentence == null)
            {
                return Fail(DialogueOperationFailureReason.SentenceNotFound, "当前句子不存在。", CurrentDialogueId);
            }

            var choice = FindChoice(sentence, request.ChoiceId);
            if (choice == null)
            {
                return Fail(DialogueOperationFailureReason.ChoiceNotFound, "未找到指定选项。", CurrentDialogueId, CurrentSentenceId, request.ChoiceId);
            }

            if (!AreConditionsMet(_blackboard.CurrentDialogue, sentence, choice, choice.Conditions, request.ActorId, request.SourceModule))
            {
                return Fail(DialogueOperationFailureReason.ChoiceUnavailable, "选项条件未满足。", CurrentDialogueId, CurrentSentenceId, choice.ChoiceId);
            }

            _currentActorId = request.ActorId;
            _currentSourceModule = request.SourceModule;

            var actionResult = ExecuteActions(_blackboard.CurrentDialogue, sentence, choice, choice.Actions, request.ActorId, request.SourceModule);
            if (!actionResult.Succeeded)
            {
                return actionResult;
            }

            var exitResult = ExecuteActions(_blackboard.CurrentDialogue, sentence, choice, sentence.ExitActions, request.ActorId, request.SourceModule);
            if (!exitResult.Succeeded)
            {
                return exitResult;
            }

            return ApplyChoiceBehavior(sentence, choice, request);
        }

        public DialogueOperationResult ForceClose(DialogueCloseRequest request)
        {
            EnsureRuntimeReady();

            if (!IsDialoguePlaying)
            {
                return DialogueOperationResult.Success();
            }

            var asset = _blackboard.CurrentDialogue;
            var sentence = CurrentSentence;
            var markAsCompleted = request != null && request.MarkAsCompleted;
            var actions = markAsCompleted ? asset.OnCompleteActions : asset.OnAbortActions;
            var result = ExecuteActions(asset, sentence, null, actions, request?.ActorId, request?.SourceModule);
            if (!result.Succeeded)
            {
                return result;
            }

            if (markAsCompleted)
            {
                MarkDialogueRead(asset);
            }

            _arbiter.CloseDialogue();
            BumpRevision();
            return DialogueOperationResult.Success(ResolveDialogueId(asset), ResolveSentenceId(sentence, _blackboard.CurrentSentenceIndex));
        }

        public DialoguePlaybackSnapshot GetPlaybackSnapshot()
        {
            return new DialoguePlaybackSnapshot
            {
                Revision = Revision,
                DialogueId = CurrentDialogueId,
                SentenceId = CurrentSentenceId,
                SentenceIndex = CurrentSentenceIndex,
                IsPlaying = IsDialoguePlaying,
                Phase = ResolvePlaybackPhase(),
                InteractionState = _blackboard?.InteractionState ?? InteractionState.Idle,
                ScriptState = _blackboard?.ScriptState ?? DialogueScriptState.Idle,
                LineState = _blackboard?.LineState ?? LineState.Idle,
                VoiceState = _blackboard?.VoiceState ?? VoiceState.Idle,
                PlaybackMode = _blackboard?.PlaybackMode ?? PlaybackMode.Manual,
                CurrentChoices = BuildChoiceViewData(CurrentSentence)
            };
        }

        public DialogueViewData BuildViewData()
        {
            var sentence = CurrentSentence;
            var choices = BuildChoiceViewData(sentence);
            return new DialogueViewData
            {
                Revision = Revision,
                DialogueId = CurrentDialogueId,
                SentenceId = CurrentSentenceId,
                SentenceIndex = CurrentSentenceIndex,
                Speaker = sentence?.Speaker ?? string.Empty,
                FullText = sentence?.Text ?? string.Empty,
                DisplayText = _blackboard?.CurrentText ?? sentence?.Text ?? string.Empty,
                ShowContinueHint = IsDialoguePlaying && _blackboard.ScriptState == DialogueScriptState.BetweenSentences,
                IsWaitingChoice = choices.Length > 0,
                Choices = choices
            };
        }

        public bool IsDialogueRead(string dialogueId)
        {
            return _progressStore != null && _progressStore.IsDialogueRead(dialogueId);
        }

        public DialogueProgressSnapshot ExportProgressSnapshot()
        {
            var saveData = _progressStore != null ? _progressStore.ExportSaveData() : null;
            return new DialogueProgressSnapshot
            {
                Revision = _progressStore != null ? _progressStore.Revision : 0,
                ReadDialogueIds = CloneStringArray(saveData?.ReadDialogueIds),
                TriggeredAmbientIds = CloneStringArray(saveData?.TriggeredAmbientIds)
            };
        }

        public DialogueOperationResult ImportProgressSnapshot(DialogueProgressSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return Fail(DialogueOperationFailureReason.InvalidRequest, "导入的 Gal 进度快照为空。");
            }

            if (_progressStore == null)
            {
                return Fail(DialogueOperationFailureReason.ServiceNotReady, "缺少 NiumaGalProgressStore，无法导入进度。");
            }

            _progressStore.ImportSaveData(new GalSaveData
            {
                ReadDialogueIds = CloneStringArray(snapshot.ReadDialogueIds),
                TriggeredAmbientIds = CloneStringArray(snapshot.TriggeredAmbientIds)
            });
            BumpRevision();
            return DialogueOperationResult.Success();
        }

        public void SetDialogueAssets(DialogueAsset[] dialogueAssets)
        {
            _dialogueAssetMap.Clear();
            if (dialogueAssets == null)
            {
                BumpRevision();
                return;
            }

            for (var i = 0; i < dialogueAssets.Length; i++)
            {
                var asset = dialogueAssets[i];
                if (asset == null || string.IsNullOrWhiteSpace(asset.DialogueId))
                {
                    continue;
                }

                if (_dialogueAssetMap.ContainsKey(asset.DialogueId))
                {
                    Debug.LogWarning($"[DialogueService] DialogueId 重复，保留后注册的资源：{asset.DialogueId}");
                }

                _dialogueAssetMap[asset.DialogueId] = asset;
            }

            BumpRevision();
        }

        public void SetConditionResolver(IDialogueConditionResolver resolver)
        {
            _conditionResolver = resolver;
            BumpRevision();
        }

        public void SetActionHandler(IDialogueActionHandler handler)
        {
            _actionHandler = handler;
            BumpRevision();
        }

        public void SetProgressStore(NiumaGalProgressStore progressStore)
        {
            _progressStore = progressStore;
            BumpRevision();
        }

        private DialogueOperationResult ApplyChoiceBehavior(DialogueSentence sentence, DialogueChoiceData choice, DialogueChoiceSelectRequest request)
        {
            switch (choice.Behavior)
            {
                case DialogueChoiceBehavior.Continue:
                    return Advance(new DialogueAdvanceRequest
                    {
                        ActorId = request.ActorId,
                        SourceModule = request.SourceModule
                    });

                case DialogueChoiceBehavior.JumpToSentence:
                    return JumpToSentence(choice.NextSentenceId, request.ActorId, request.SourceModule, choice.ChoiceId);

                case DialogueChoiceBehavior.EndDialogue:
                    return CompleteAndClose(sentence, choice, request.ActorId, request.SourceModule);

                case DialogueChoiceBehavior.Custom:
                    if (!string.IsNullOrWhiteSpace(choice.NextSentenceId))
                    {
                        return JumpToSentence(choice.NextSentenceId, request.ActorId, request.SourceModule, choice.ChoiceId);
                    }

                    return CompleteAndClose(sentence, choice, request.ActorId, request.SourceModule);

                default:
                    return Fail(DialogueOperationFailureReason.InvalidRequest, "未知的选项行为。", CurrentDialogueId, CurrentSentenceId, choice.ChoiceId);
            }
        }

        private DialogueOperationResult CompleteAndClose(DialogueSentence sentence, DialogueChoiceData choice, string actorId, string sourceModule)
        {
            var result = ExecuteActions(_blackboard.CurrentDialogue, sentence, choice, _blackboard.CurrentDialogue.OnCompleteActions, actorId, sourceModule);
            if (!result.Succeeded)
            {
                return result;
            }

            MarkDialogueRead(_blackboard.CurrentDialogue);
            _arbiter.CloseDialogue();
            BumpRevision();
            return DialogueOperationResult.Success(CurrentDialogueId, ResolveSentenceId(sentence, _blackboard.CurrentSentenceIndex), choice?.ChoiceId);
        }

        private DialogueOperationResult JumpToSentence(string sentenceId, string actorId, string sourceModule, string choiceId)
        {
            var targetIndex = FindSentenceIndex(_blackboard.CurrentDialogue, sentenceId);
            if (targetIndex < 0)
            {
                return Fail(DialogueOperationFailureReason.SentenceNotFound, "选项跳转目标句子不存在。", CurrentDialogueId, sentenceId, choiceId);
            }

            var targetSentence = _blackboard.CurrentDialogue.Sentences[targetIndex];
            if (!AreConditionsMet(_blackboard.CurrentDialogue, targetSentence, null, targetSentence.Conditions, actorId, sourceModule))
            {
                return Fail(DialogueOperationFailureReason.ConditionBlocked, "跳转目标句子条件未满足。", CurrentDialogueId, sentenceId, choiceId);
            }

            var result = ExecuteActions(_blackboard.CurrentDialogue, targetSentence, null, targetSentence.EnterActions, actorId, sourceModule);
            if (!result.Succeeded)
            {
                return result;
            }

            _arbiter.JumpToSentence(targetIndex);
            BumpRevision();
            return DialogueOperationResult.Success(CurrentDialogueId, ResolveSentenceId(targetSentence, targetIndex), choiceId);
        }

        private DialogueAsset ResolveDialogueAsset(DialogueStartRequest request)
        {
            if (request.DialogueAsset != null)
            {
                return request.DialogueAsset;
            }

            if (!string.IsNullOrWhiteSpace(request.DialogueId) &&
                _dialogueAssetMap.TryGetValue(request.DialogueId, out var asset))
            {
                return asset;
            }

            return null;
        }

        private int ResolveStartSentenceIndex(DialogueAsset asset, string requestedSentenceId)
        {
            if (asset == null || asset.Sentences == null || asset.Sentences.Count == 0)
            {
                return -1;
            }

            var sentenceId = !string.IsNullOrWhiteSpace(requestedSentenceId)
                ? requestedSentenceId
                : asset.StartSentenceId;

            if (!string.IsNullOrWhiteSpace(sentenceId))
            {
                return FindSentenceIndex(asset, sentenceId);
            }

            return 0;
        }

        private static int FindSentenceIndex(DialogueAsset asset, string sentenceId)
        {
            if (asset == null || asset.Sentences == null || string.IsNullOrWhiteSpace(sentenceId))
            {
                return -1;
            }

            for (var i = 0; i < asset.Sentences.Count; i++)
            {
                if (string.Equals(asset.Sentences[i]?.SentenceId, sentenceId, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private bool AreConditionsMet(
            DialogueAsset asset,
            DialogueSentence sentence,
            DialogueChoiceData choice,
            DialogueConditionData[] conditions,
            string actorId,
            string sourceModule)
        {
            if (conditions == null || conditions.Length == 0)
            {
                return true;
            }

            for (var i = 0; i < conditions.Length; i++)
            {
                var condition = conditions[i];
                if (condition == null || condition.Type == DialogueConditionType.None)
                {
                    continue;
                }

                if (condition.Type == DialogueConditionType.DialogueRead)
                {
                    if (!IsDialogueRead(condition.TargetId))
                    {
                        return false;
                    }

                    continue;
                }

                if (condition.Type == DialogueConditionType.DialogueUnread)
                {
                    if (IsDialogueRead(condition.TargetId))
                    {
                        return false;
                    }

                    continue;
                }

                if (_conditionResolver == null)
                {
                    return false;
                }

                var context = new DialogueConditionContext(asset, sentence, choice, condition, actorId, sourceModule);
                if (!_conditionResolver.IsConditionMet(in context))
                {
                    return false;
                }
            }

            return true;
        }

        private DialogueOperationResult ExecuteActions(
            DialogueAsset asset,
            DialogueSentence sentence,
            DialogueChoiceData choice,
            DialogueActionData[] actions,
            string actorId,
            string sourceModule)
        {
            if (actions == null || actions.Length == 0)
            {
                return DialogueOperationResult.Success(ResolveDialogueId(asset), ResolveSentenceId(sentence, _blackboard?.CurrentSentenceIndex ?? -1), choice?.ChoiceId);
            }

            for (var i = 0; i < actions.Length; i++)
            {
                var action = actions[i];
                if (action == null || action.Type == DialogueActionType.None)
                {
                    continue;
                }

                if (_actionHandler == null)
                {
                    return Fail(DialogueOperationFailureReason.ActionFailed, $"缺少对话行为处理器：{action.Type}", ResolveDialogueId(asset), ResolveSentenceId(sentence, _blackboard?.CurrentSentenceIndex ?? -1), choice?.ChoiceId);
                }

                var context = new DialogueActionContext(asset, sentence, choice, action, actorId, sourceModule);
                var result = _actionHandler.Execute(in context);
                if (result == null || !result.Succeeded)
                {
                    return result ?? Fail(DialogueOperationFailureReason.ActionFailed, $"对话行为执行失败：{action.Type}", ResolveDialogueId(asset), ResolveSentenceId(sentence, _blackboard?.CurrentSentenceIndex ?? -1), choice?.ChoiceId);
                }
            }

            return DialogueOperationResult.Success(ResolveDialogueId(asset), ResolveSentenceId(sentence, _blackboard?.CurrentSentenceIndex ?? -1), choice?.ChoiceId);
        }

        private DialogueChoiceViewData[] BuildChoiceViewData(DialogueSentence sentence)
        {
            if (sentence?.Choices == null || sentence.Choices.Length == 0)
            {
                return Array.Empty<DialogueChoiceViewData>();
            }

            var result = new List<DialogueChoiceViewData>(sentence.Choices.Length);
            for (var i = 0; i < sentence.Choices.Length; i++)
            {
                var choice = sentence.Choices[i];
                if (choice == null)
                {
                    continue;
                }

                var available = AreConditionsMet(_blackboard.CurrentDialogue, sentence, choice, choice.Conditions, _currentActorId, _currentSourceModule);
                if (!available && choice.HideWhenUnavailable)
                {
                    continue;
                }

                result.Add(new DialogueChoiceViewData
                {
                    ChoiceId = choice.ChoiceId,
                    DisplayText = choice.DisplayText,
                    IsAvailable = available,
                    DisabledText = available ? null : choice.DisabledText
                });
            }

            return result.ToArray();
        }

        private DialogueChoiceData FindChoice(DialogueSentence sentence, string choiceId)
        {
            if (sentence?.Choices == null)
            {
                return null;
            }

            for (var i = 0; i < sentence.Choices.Length; i++)
            {
                var choice = sentence.Choices[i];
                if (choice != null && string.Equals(choice.ChoiceId, choiceId, StringComparison.Ordinal))
                {
                    return choice;
                }
            }

            return null;
        }

        private DialoguePlaybackPhase ResolvePlaybackPhase()
        {
            if (!IsDialoguePlaying)
            {
                return DialoguePlaybackPhase.Idle;
            }

            if (_blackboard.ScriptState == DialogueScriptState.Running)
            {
                return DialoguePlaybackPhase.PlayingLine;
            }

            if (_blackboard.ScriptState == DialogueScriptState.BetweenSentences)
            {
                return BuildChoiceViewData(CurrentSentence).Length > 0
                    ? DialoguePlaybackPhase.WaitingChoice
                    : DialoguePlaybackPhase.WaitingAdvance;
            }

            return DialoguePlaybackPhase.Closing;
        }

        private DialogueSentence CurrentSentence
        {
            get
            {
                var asset = _blackboard?.CurrentDialogue;
                if (asset?.Sentences == null)
                {
                    return null;
                }

                var index = _blackboard.CurrentSentenceIndex;
                return index >= 0 && index < asset.Sentences.Count ? asset.Sentences[index] : null;
            }
        }

        private string CurrentSentenceId => ResolveSentenceId(CurrentSentence, CurrentSentenceIndex);

        private static string ResolveDialogueId(DialogueAsset asset)
        {
            return asset == null ? null : asset.DialogueId;
        }

        private static string ResolveSentenceId(DialogueSentence sentence, int index)
        {
            if (!string.IsNullOrWhiteSpace(sentence?.SentenceId))
            {
                return sentence.SentenceId;
            }

            return index >= 0 ? index.ToString() : null;
        }

        private void MarkDialogueRead(DialogueAsset asset)
        {
            var dialogueId = ResolveDialogueId(asset);
            if (!string.IsNullOrWhiteSpace(dialogueId))
            {
                _progressStore?.MarkDialogueRead(dialogueId);
            }
        }

        private void EnsureRuntimeReady()
        {
            _ensureRuntimeReady?.Invoke();
        }

        private void BindBlackboardRevisionEvents()
        {
            if (_blackboard == null)
            {
                return;
            }

            _blackboard.OnInteractionStateChanged += _ => BumpRevision();
            _blackboard.OnScriptStateChanged += _ => BumpRevision();
            _blackboard.OnLineStateChanged += _ => BumpRevision();
            _blackboard.OnVoiceStateChanged += _ => BumpRevision();
            _blackboard.OnPlaybackModeChanged += _ => BumpRevision();
        }

        private void BumpRevision()
        {
            _revision = _revision == long.MaxValue ? long.MaxValue : _revision + 1;
        }

        private static DialogueOperationResult Fail(DialogueOperationFailureReason reason, string message, string dialogueId = null, string sentenceId = null, string choiceId = null)
        {
            return DialogueOperationResult.Fail(reason, message, dialogueId, sentenceId, choiceId);
        }

        private static string[] CloneStringArray(string[] source)
        {
            if (source == null || source.Length == 0)
            {
                return Array.Empty<string>();
            }

            var result = new string[source.Length];
            Array.Copy(source, result, source.Length);
            return result;
        }
    }
}
