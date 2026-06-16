using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NiumaGal.Dialogue.Data;
using NiumaGal.Enum;
using UnityEngine;

namespace NiumaGal.Editor
{
    public enum DialogueValidationSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2
    }

    public sealed class DialogueValidationItem
    {
        public DialogueValidationSeverity Severity;
        public string Code;
        public string Message;
        public int SentenceIndex = -1;
        public int ChoiceIndex = -1;
        public string Scope;
    }

    public sealed class DialogueValidationReport
    {
        public DialogueValidationItem[] Items = Array.Empty<DialogueValidationItem>();
        public DialogueGraphSnapshot Graph = DialogueGraphSnapshot.Empty;
        public int ErrorCount;
        public int WarningCount;
        public int InfoCount;
        public int SentenceCount;
        public int CharacterCount;
        public int EstimatedReadSeconds;
        public int BranchSentenceCount;

        public bool IsValid => ErrorCount == 0;
    }

    public sealed class DialogueGraphSnapshot
    {
        public static readonly DialogueGraphSnapshot Empty = new DialogueGraphSnapshot();

        public DialogueGraphNodeData[] Nodes = Array.Empty<DialogueGraphNodeData>();
        public DialogueGraphEdgeData[] Edges = Array.Empty<DialogueGraphEdgeData>();
        public int StartIndex = -1;
    }

    public sealed class DialogueGraphNodeData
    {
        public int Index;
        public string EditorGuid;
        public string SentenceId;
        public string Title;
        public string Summary;
        public DialogueNarrativeCategory NarrativeCategory;
        public bool IsStart;
        public bool IsReachable;
    }

    public sealed class DialogueGraphEdgeData
    {
        public int FromIndex;
        public int ToIndex = -1;
        public string Label;
        public bool IsConditional;
        public bool IsUnknown;
        public bool IsEnd;
    }

    public static class DialogueValidationService
    {
        private static readonly Regex RichTextTagRegex = new Regex(@"</?[a-zA-Z][^>]*>", RegexOptions.Compiled);

        public static DialogueValidationReport Validate(DialogueAsset asset, DialogueSpeakerCatalog speakerCatalog, bool warnWhenSpeakerEmpty = true)
        {
            var builder = new ReportBuilder();
            var graph = BuildGraph(asset);

            if (asset == null)
            {
                builder.Add(DialogueValidationSeverity.Error, "asset_null", "DialogueAsset 为空。");
                return builder.Build(DialogueGraphSnapshot.Empty, 0, 0, 0, 0);
            }

            if (string.IsNullOrWhiteSpace(asset.DialogueId))
            {
                builder.Add(DialogueValidationSeverity.Error, "dialogue_id_empty", "DialogueId 为空，运行时无法稳定索引该对话。");
            }

            var sentences = asset.Sentences;
            if (sentences == null || sentences.Count == 0)
            {
                builder.Add(DialogueValidationSeverity.Error, "sentences_empty", "Sentences 为空，对话没有可播放内容。");
                return builder.Build(graph, 0, 0, 0, 0);
            }

            var sentenceIdToIndex = BuildSentenceIdMap(sentences, builder);
            var totalCharacters = 0;
            var branchSentenceCount = 0;

            ValidateAssetActions(asset.OnStartActions, "OnStartActions", builder);
            ValidateAssetActions(asset.OnCompleteActions, "OnCompleteActions", builder);
            ValidateAssetActions(asset.OnAbortActions, "OnAbortActions", builder);
            ValidateStartSentence(asset.StartSentenceId, sentenceIdToIndex, builder);

            for (var i = 0; i < sentences.Count; i++)
            {
                var sentence = sentences[i];
                if (sentence == null)
                {
                    builder.Add(DialogueValidationSeverity.Error, "sentence_null", $"第 {i + 1} 句为空。", i);
                    continue;
                }

                var plainText = StripRichTextTags(sentence.Text);
                totalCharacters += plainText.Length;
                if (plainText.Length > 120)
                {
                    builder.Add(DialogueValidationSeverity.Warning, "text_too_long", $"第 {i + 1} 句文本较长（{plainText.Length} 字），建议拆句提升阅读节奏。", i);
                }

                ValidateSpeaker(sentence, speakerCatalog, warnWhenSpeakerEmpty, i, builder);
                ValidateEditorMetadata(sentence, i, builder);
                ValidateConditions(sentence.Conditions, $"Sentence[{i}].Conditions", i, builder);
                ValidateActions(sentence.EnterActions, $"Sentence[{i}].EnterActions", i, builder);
                ValidateActions(sentence.ExitActions, $"Sentence[{i}].ExitActions", i, builder);

                var choices = sentence.Choices;
                if (choices == null || choices.Length == 0)
                {
                    continue;
                }

                branchSentenceCount++;
                ValidateChoices(choices, sentenceIdToIndex, i, builder);
            }

            AddReachabilityWarnings(graph, builder);
            builder.Add(
                DialogueValidationSeverity.Info,
                "summary",
                $"总句数 {sentences.Count}，可见字符 {totalCharacters}，预计阅读 {Mathf.CeilToInt(totalCharacters / 5f)} 秒，分支句 {branchSentenceCount} 个。");

            return builder.Build(graph, sentences.Count, totalCharacters, Mathf.CeilToInt(totalCharacters / 5f), branchSentenceCount);
        }

        public static DialogueGraphSnapshot BuildGraph(DialogueAsset asset)
        {
            if (asset?.Sentences == null || asset.Sentences.Count == 0)
            {
                return DialogueGraphSnapshot.Empty;
            }

            var sentences = asset.Sentences;
            var idToIndex = BuildSentenceIdMap(sentences, null);
            var startIndex = ResolveStartIndex(asset, idToIndex);
            var edges = new List<DialogueGraphEdgeData>();
            var nodes = new DialogueGraphNodeData[sentences.Count];

            for (var i = 0; i < sentences.Count; i++)
            {
                var sentence = sentences[i];
                nodes[i] = new DialogueGraphNodeData
                {
                    Index = i,
                    EditorGuid = sentence?.EditorGuid ?? string.Empty,
                    SentenceId = sentence?.SentenceId ?? string.Empty,
                    Title = BuildNodeTitle(sentence, i),
                    Summary = BuildSummary(sentence?.Text, 42),
                    NarrativeCategory = sentence?.NarrativeCategory ?? DialogueNarrativeCategory.None,
                    IsStart = i == startIndex
                };

                AddOutgoingEdges(sentences, idToIndex, i, edges);
            }

            MarkReachable(nodes, edges, startIndex);
            return new DialogueGraphSnapshot
            {
                Nodes = nodes,
                Edges = edges.ToArray(),
                StartIndex = startIndex
            };
        }

        private static Dictionary<string, int> BuildSentenceIdMap(IList<DialogueSentence> sentences, ReportBuilder builder)
        {
            var result = new Dictionary<string, int>(StringComparer.Ordinal);
            if (sentences == null)
            {
                return result;
            }

            for (var i = 0; i < sentences.Count; i++)
            {
                var id = sentences[i]?.SentenceId;
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                if (result.ContainsKey(id))
                {
                    builder?.Add(DialogueValidationSeverity.Error, "sentence_id_duplicate", $"SentenceId 重复：{id}", i);
                    continue;
                }

                result[id] = i;
            }

            return result;
        }

        private static void ValidateStartSentence(string startSentenceId, Dictionary<string, int> sentenceIdToIndex, ReportBuilder builder)
        {
            if (string.IsNullOrWhiteSpace(startSentenceId))
            {
                return;
            }

            if (!sentenceIdToIndex.ContainsKey(startSentenceId))
            {
                builder.Add(DialogueValidationSeverity.Error, "start_sentence_missing", $"StartSentenceId 指向不存在的句子：{startSentenceId}");
            }
        }

        private static void ValidateSpeaker(DialogueSentence sentence, DialogueSpeakerCatalog speakerCatalog, bool warnWhenSpeakerEmpty, int sentenceIndex, ReportBuilder builder)
        {
            if (sentence == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(sentence.Speaker))
            {
                if (warnWhenSpeakerEmpty)
                {
                    builder.Add(DialogueValidationSeverity.Warning, "speaker_empty", $"Sentence {sentenceIndex + 1} has an empty Speaker. Disable WarnWhenSpeakerEmpty in Gal Editor settings if this is narration.", sentenceIndex);
                }

                return;
            }

            if (speakerCatalog == null || speakerCatalog.Speakers == null || speakerCatalog.Speakers.Length == 0)
            {
                builder.Add(DialogueValidationSeverity.Warning, "speaker_catalog_missing", $"第 {sentenceIndex + 1} 句填写了 Speaker，但当前没有可用 Speaker Catalog。", sentenceIndex);
                return;
            }

            for (var i = 0; i < speakerCatalog.Speakers.Length; i++)
            {
                var speaker = speakerCatalog.Speakers[i];
                if (speaker != null && string.Equals(speaker.SpeakerKey, sentence.Speaker, StringComparison.Ordinal))
                {
                    return;
                }
            }

            builder.Add(DialogueValidationSeverity.Warning, "speaker_missing", $"Speaker 不在当前 Catalog 中：{sentence.Speaker}", sentenceIndex);
        }

        private static void ValidateEditorMetadata(DialogueSentence sentence, int sentenceIndex, ReportBuilder builder)
        {
            if (sentence == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(sentence.EditorGuid))
            {
                builder.Add(DialogueValidationSeverity.Warning, "editor_guid_missing", $"Sentence {sentenceIndex + 1} 缺少 EditorGuid。打开专用编辑器时会自动补齐。", sentenceIndex);
            }

            if (sentence.NarrativeCategory == DialogueNarrativeCategory.None)
            {
                builder.Add(DialogueValidationSeverity.Info, "narrative_category_none", $"Sentence {sentenceIndex + 1} 未设置叙事分类。None 是合法默认值，不影响运行。", sentenceIndex);
            }
        }

        private static void ValidateChoices(DialogueChoiceData[] choices, Dictionary<string, int> sentenceIdToIndex, int sentenceIndex, ReportBuilder builder)
        {
            var choiceIds = new HashSet<string>(StringComparer.Ordinal);
            var allChoicesAreConditional = true;

            for (var i = 0; i < choices.Length; i++)
            {
                var choice = choices[i];
                if (choice == null)
                {
                    builder.Add(DialogueValidationSeverity.Error, "choice_null", $"第 {sentenceIndex + 1} 句的第 {i + 1} 个选项为空。", sentenceIndex, i);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(choice.ChoiceId))
                {
                    builder.Add(DialogueValidationSeverity.Error, "choice_id_empty", $"第 {sentenceIndex + 1} 句的第 {i + 1} 个 ChoiceId 为空。", sentenceIndex, i);
                }
                else if (!choiceIds.Add(choice.ChoiceId))
                {
                    builder.Add(DialogueValidationSeverity.Error, "choice_id_duplicate", $"第 {sentenceIndex + 1} 句内 ChoiceId 重复：{choice.ChoiceId}", sentenceIndex, i);
                }

                if (choice.Conditions == null || choice.Conditions.Length == 0)
                {
                    allChoicesAreConditional = false;
                }

                ValidateConditions(choice.Conditions, $"Sentence[{sentenceIndex}].Choices[{i}].Conditions", sentenceIndex, builder, i);
                ValidateActions(choice.Actions, $"Sentence[{sentenceIndex}].Choices[{i}].Actions", sentenceIndex, builder, i);
                ValidateChoiceTarget(choice, sentenceIdToIndex, sentenceIndex, i, builder);
            }

            if (choices.Length > 0 && allChoicesAreConditional)
            {
                builder.Add(DialogueValidationSeverity.Warning, "all_choices_conditional", $"第 {sentenceIndex + 1} 句的所有选项都带条件，运行时可能全部不可见或不可点击。", sentenceIndex);
            }
        }

        private static void ValidateChoiceTarget(DialogueChoiceData choice, Dictionary<string, int> sentenceIdToIndex, int sentenceIndex, int choiceIndex, ReportBuilder builder)
        {
            switch (choice.Behavior)
            {
                case DialogueChoiceBehavior.JumpToSentence:
                    if (string.IsNullOrWhiteSpace(choice.NextSentenceId))
                    {
                        builder.Add(DialogueValidationSeverity.Error, "choice_jump_target_empty", "JumpToSentence 选项缺少 NextSentenceId。", sentenceIndex, choiceIndex);
                    }
                    else if (!sentenceIdToIndex.ContainsKey(choice.NextSentenceId))
                    {
                        builder.Add(DialogueValidationSeverity.Error, "choice_jump_target_missing", $"JumpToSentence 目标不存在：{choice.NextSentenceId}", sentenceIndex, choiceIndex);
                    }
                    break;

                case DialogueChoiceBehavior.Custom:
                    if (!string.IsNullOrWhiteSpace(choice.NextSentenceId) && !sentenceIdToIndex.ContainsKey(choice.NextSentenceId))
                    {
                        builder.Add(DialogueValidationSeverity.Error, "choice_custom_target_missing", $"Custom 选项的 NextSentenceId 不存在：{choice.NextSentenceId}", sentenceIndex, choiceIndex);
                    }
                    break;
            }
        }

        private static void ValidateAssetActions(DialogueActionData[] actions, string scope, ReportBuilder builder)
        {
            ValidateActions(actions, scope, -1, builder);
        }

        private static void ValidateActions(DialogueActionData[] actions, string scope, int sentenceIndex, ReportBuilder builder, int choiceIndex = -1)
        {
            if (actions == null)
            {
                return;
            }

            for (var i = 0; i < actions.Length; i++)
            {
                var action = actions[i];
                if (action == null)
                {
                    builder.Add(DialogueValidationSeverity.Error, "action_null", $"{scope} 的第 {i + 1} 个 Action 为空。", sentenceIndex, choiceIndex, scope);
                    continue;
                }

                if (action.Type == DialogueActionType.None && HasKeyParameters(action))
                {
                    builder.Add(DialogueValidationSeverity.Warning, "action_none_has_params", $"{scope} 的第 {i + 1} 个 Action 类型为 None，但填写了参数。", sentenceIndex, choiceIndex, scope);
                }

                if (RequiresTargetId(action.Type) && string.IsNullOrWhiteSpace(action.TargetId))
                {
                    builder.Add(DialogueValidationSeverity.Error, "action_target_missing", $"{scope} 的第 {i + 1} 个 {action.Type} 缺少 TargetId。", sentenceIndex, choiceIndex, scope);
                }
            }
        }

        private static void ValidateConditions(DialogueConditionData[] conditions, string scope, int sentenceIndex, ReportBuilder builder, int choiceIndex = -1)
        {
            if (conditions == null)
            {
                return;
            }

            for (var i = 0; i < conditions.Length; i++)
            {
                var condition = conditions[i];
                if (condition == null)
                {
                    builder.Add(DialogueValidationSeverity.Error, "condition_null", $"{scope} 的第 {i + 1} 个 Condition 为空。", sentenceIndex, choiceIndex, scope);
                    continue;
                }

                if (condition.Type == DialogueConditionType.None && HasKeyParameters(condition))
                {
                    builder.Add(DialogueValidationSeverity.Warning, "condition_none_has_params", $"{scope} 的第 {i + 1} 个 Condition 类型为 None，但填写了参数。", sentenceIndex, choiceIndex, scope);
                }

                if (RequiresTargetId(condition.Type) && string.IsNullOrWhiteSpace(condition.TargetId))
                {
                    builder.Add(DialogueValidationSeverity.Warning, "condition_target_missing", $"{scope} 的第 {i + 1} 个 {condition.Type} 可能缺少 TargetId。", sentenceIndex, choiceIndex, scope);
                }
            }
        }

        private static bool RequiresTargetId(DialogueActionType type)
        {
            switch (type)
            {
                case DialogueActionType.StartDialogue:
                case DialogueActionType.OpenMiniGame:
                case DialogueActionType.AcceptQuest:
                case DialogueActionType.PushQuestSignal:
                case DialogueActionType.StartStory:
                case DialogueActionType.SetStoryFlag:
                case DialogueActionType.LoadScene:
                case DialogueActionType.PlayAudioCue:
                    return true;
                default:
                    return false;
            }
        }

        private static bool RequiresTargetId(DialogueConditionType type)
        {
            switch (type)
            {
                case DialogueConditionType.DialogueRead:
                case DialogueConditionType.DialogueUnread:
                case DialogueConditionType.QuestState:
                case DialogueConditionType.QuestObjectiveCompleted:
                case DialogueConditionType.StoryFlag:
                case DialogueConditionType.HasItem:
                case DialogueConditionType.GrowthLevel:
                case DialogueConditionType.MiniGameUnlocked:
                    return true;
                default:
                    return false;
            }
        }

        private static bool HasKeyParameters(DialogueActionData action)
        {
            return !string.IsNullOrWhiteSpace(action.TargetId) ||
                   !string.IsNullOrWhiteSpace(action.StringValue) ||
                   action.IntValue != 0 ||
                   !Mathf.Approximately(action.FloatValue, 0f) ||
                   action.BoolValue ||
                   (action.CustomData != null && action.CustomData.Length > 0);
        }

        private static bool HasKeyParameters(DialogueConditionData condition)
        {
            return !string.IsNullOrWhiteSpace(condition.TargetId) ||
                   !string.IsNullOrWhiteSpace(condition.StringValue) ||
                   condition.IntValue != 0 ||
                   !Mathf.Approximately(condition.FloatValue, 0f) ||
                   condition.BoolValue ||
                   (condition.CustomData != null && condition.CustomData.Length > 0);
        }

        private static void AddOutgoingEdges(IList<DialogueSentence> sentences, Dictionary<string, int> idToIndex, int index, List<DialogueGraphEdgeData> edges)
        {
            var sentence = sentences[index];
            var choices = sentence?.Choices;
            if (choices == null || choices.Length == 0)
            {
                AddLinearEdge(sentences, index, edges);
                return;
            }

            for (var i = 0; i < choices.Length; i++)
            {
                var choice = choices[i];
                if (choice == null)
                {
                    continue;
                }

                var label = string.IsNullOrWhiteSpace(choice.ChoiceId)
                    ? $"Choice {i + 1}"
                    : choice.ChoiceId;
                var conditional = choice.Conditions != null && choice.Conditions.Length > 0;

                switch (choice.Behavior)
                {
                    case DialogueChoiceBehavior.Continue:
                        AddLinearEdge(sentences, index, edges, label, conditional);
                        break;

                    case DialogueChoiceBehavior.JumpToSentence:
                        AddTargetEdge(index, choice.NextSentenceId, idToIndex, label, conditional, edges);
                        break;

                    case DialogueChoiceBehavior.EndDialogue:
                        edges.Add(new DialogueGraphEdgeData { FromIndex = index, Label = label, IsConditional = conditional, IsEnd = true });
                        break;

                    case DialogueChoiceBehavior.Custom:
                        if (string.IsNullOrWhiteSpace(choice.NextSentenceId))
                        {
                            edges.Add(new DialogueGraphEdgeData { FromIndex = index, Label = $"{label} (Custom / Unknown)", IsConditional = conditional, IsUnknown = true });
                        }
                        else
                        {
                            AddTargetEdge(index, choice.NextSentenceId, idToIndex, $"{label} (Custom)", conditional, edges);
                        }
                        break;

                    default:
                        edges.Add(new DialogueGraphEdgeData { FromIndex = index, Label = $"{label} (Unknown)", IsConditional = conditional, IsUnknown = true });
                        break;
                }
            }
        }

        private static void AddLinearEdge(IList<DialogueSentence> sentences, int index, List<DialogueGraphEdgeData> edges, string label = "Continue", bool conditional = false)
        {
            if (index + 1 < sentences.Count)
            {
                edges.Add(new DialogueGraphEdgeData { FromIndex = index, ToIndex = index + 1, Label = label, IsConditional = conditional });
                return;
            }

            edges.Add(new DialogueGraphEdgeData { FromIndex = index, Label = label, IsConditional = conditional, IsEnd = true });
        }

        private static void AddTargetEdge(int fromIndex, string targetSentenceId, Dictionary<string, int> idToIndex, string label, bool conditional, List<DialogueGraphEdgeData> edges)
        {
            if (!string.IsNullOrWhiteSpace(targetSentenceId) && idToIndex.TryGetValue(targetSentenceId, out var targetIndex))
            {
                edges.Add(new DialogueGraphEdgeData { FromIndex = fromIndex, ToIndex = targetIndex, Label = label, IsConditional = conditional });
                return;
            }

            edges.Add(new DialogueGraphEdgeData { FromIndex = fromIndex, Label = $"{label} -> Missing", IsConditional = conditional, IsUnknown = true });
        }

        private static void MarkReachable(DialogueGraphNodeData[] nodes, List<DialogueGraphEdgeData> edges, int startIndex)
        {
            if (nodes == null || nodes.Length == 0 || startIndex < 0 || startIndex >= nodes.Length)
            {
                return;
            }

            var visited = new bool[nodes.Length];
            var queue = new Queue<int>();
            visited[startIndex] = true;
            queue.Enqueue(startIndex);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                for (var i = 0; i < edges.Count; i++)
                {
                    var edge = edges[i];
                    if (edge.FromIndex != current || edge.ToIndex < 0 || edge.ToIndex >= nodes.Length || visited[edge.ToIndex])
                    {
                        continue;
                    }

                    visited[edge.ToIndex] = true;
                    queue.Enqueue(edge.ToIndex);
                }
            }

            for (var i = 0; i < nodes.Length; i++)
            {
                nodes[i].IsReachable = visited[i];
            }
        }

        private static void AddReachabilityWarnings(DialogueGraphSnapshot graph, ReportBuilder builder)
        {
            if (graph?.Nodes == null)
            {
                return;
            }

            for (var i = 0; i < graph.Nodes.Length; i++)
            {
                var node = graph.Nodes[i];
                if (node != null && !node.IsReachable)
                {
                    builder.Add(DialogueValidationSeverity.Warning, "sentence_unreachable", $"第 {node.Index + 1} 句结构上不可达。", node.Index);
                }
            }
        }

        private static int ResolveStartIndex(DialogueAsset asset, Dictionary<string, int> idToIndex)
        {
            if (asset?.Sentences == null || asset.Sentences.Count == 0)
            {
                return -1;
            }

            if (!string.IsNullOrWhiteSpace(asset.StartSentenceId) &&
                idToIndex != null &&
                idToIndex.TryGetValue(asset.StartSentenceId, out var index))
            {
                return index;
            }

            return 0;
        }

        private static string BuildNodeTitle(DialogueSentence sentence, int index)
        {
            if (sentence == null)
            {
                return $"{index + 1}. <null>";
            }

            var id = string.IsNullOrWhiteSpace(sentence.SentenceId) ? "<empty id>" : sentence.SentenceId;
            return $"{index + 1}. {id}";
        }

        private static string BuildSummary(string text, int maxLength)
        {
            var plain = StripRichTextTags(text).Replace("\r", " ").Replace("\n", " ").Trim();
            if (plain.Length <= maxLength)
            {
                return plain;
            }

            return plain.Substring(0, Math.Max(0, maxLength)) + "...";
        }

        private static string StripRichTextTags(string text)
        {
            return RichTextTagRegex.Replace(text ?? string.Empty, string.Empty);
        }

        private sealed class ReportBuilder
        {
            private readonly List<DialogueValidationItem> items = new List<DialogueValidationItem>();

            public void Add(DialogueValidationSeverity severity, string code, string message, int sentenceIndex = -1, int choiceIndex = -1, string scope = null)
            {
                items.Add(new DialogueValidationItem
                {
                    Severity = severity,
                    Code = code,
                    Message = message,
                    SentenceIndex = sentenceIndex,
                    ChoiceIndex = choiceIndex,
                    Scope = scope
                });
            }

            public DialogueValidationReport Build(DialogueGraphSnapshot graph, int sentenceCount, int characterCount, int estimatedReadSeconds, int branchSentenceCount)
            {
                var report = new DialogueValidationReport
                {
                    Items = items.ToArray(),
                    Graph = graph ?? DialogueGraphSnapshot.Empty,
                    SentenceCount = sentenceCount,
                    CharacterCount = characterCount,
                    EstimatedReadSeconds = estimatedReadSeconds,
                    BranchSentenceCount = branchSentenceCount
                };

                for (var i = 0; i < items.Count; i++)
                {
                    switch (items[i].Severity)
                    {
                        case DialogueValidationSeverity.Error:
                            report.ErrorCount++;
                            break;
                        case DialogueValidationSeverity.Warning:
                            report.WarningCount++;
                            break;
                        default:
                            report.InfoCount++;
                            break;
                    }
                }

                return report;
            }
        }
    }
}
