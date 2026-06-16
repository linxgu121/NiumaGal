using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NiumaGal.Editor
{
    public abstract class DialogueArrayCardBuilderBase
    {
        private readonly SerializedObject serializedObject;
        private readonly Action onChanged;

        protected DialogueArrayCardBuilderBase(SerializedObject serializedObject, Action onChanged)
        {
            this.serializedObject = serializedObject ?? throw new ArgumentNullException(nameof(serializedObject));
            this.onChanged = onChanged;
        }

        protected void AddArrayCommandRow(VisualElement parent, SerializedProperty arrayProperty, string itemName, Action<SerializedProperty> initializer)
        {
            var row = new Toolbar
            {
                name = $"Dialogue{itemName}ArrayToolbar"
            };
            row.Add(new ToolbarButton(() => AddArrayElement(arrayProperty, initializer))
            {
                text = $"Add {itemName}"
            });
            parent.Add(row);
        }

        protected void AddCardDeleteButton(VisualElement parent, SerializedProperty arrayProperty, int index, string itemName)
        {
            var row = new VisualElement
            {
                name = $"Dialogue{itemName}CardCommandRow"
            };
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.FlexEnd;

            var capturedIndex = index;
            row.Add(new Button(() => DeleteArrayElementAndRefresh(arrayProperty, capturedIndex))
            {
                text = $"Delete {itemName}"
            });
            parent.Add(row);
        }

        protected static Foldout BuildArrayFoldout(string title, string timingDescription, int count)
        {
            var foldout = new Foldout
            {
                text = title,
                value = true
            };
            foldout.style.marginTop = 6f;
            foldout.style.marginBottom = 4f;

            if (!string.IsNullOrWhiteSpace(timingDescription))
            {
                foldout.Add(new HelpBox(timingDescription, HelpBoxMessageType.Info));
            }

            if (count == 0)
            {
                foldout.Add(new HelpBox("当前列表为空。点击下方 Add 按钮新增元素。", HelpBoxMessageType.Info));
            }

            return foldout;
        }

        protected static Foldout BuildCardFoldout(string title)
        {
            var card = new Foldout
            {
                text = title,
                value = false
            };
            card.style.marginLeft = 10f;
            card.style.marginTop = 4f;
            card.style.paddingLeft = 6f;
            card.style.paddingTop = 4f;
            card.style.paddingBottom = 4f;
            card.style.borderLeftWidth = 2f;
            card.style.borderLeftColor = new Color(0.3f, 0.55f, 0.85f, 1f);
            return card;
        }

        private void AddArrayElement(SerializedProperty arrayProperty, Action<SerializedProperty> initializer)
        {
            var propertyPath = arrayProperty?.propertyPath;
            if (string.IsNullOrWhiteSpace(propertyPath))
            {
                return;
            }

            serializedObject.UpdateIfRequiredOrScript();
            var refreshedArray = serializedObject.FindProperty(propertyPath);
            if (refreshedArray == null || !refreshedArray.isArray)
            {
                return;
            }

            var index = refreshedArray.arraySize;
            refreshedArray.InsertArrayElementAtIndex(index);
            initializer?.Invoke(refreshedArray.GetArrayElementAtIndex(index));
            serializedObject.ApplyModifiedProperties();
            serializedObject.UpdateIfRequiredOrScript();
            onChanged?.Invoke();
        }

        private void DeleteArrayElementAndRefresh(SerializedProperty arrayProperty, int index)
        {
            var propertyPath = arrayProperty?.propertyPath;
            if (string.IsNullOrWhiteSpace(propertyPath))
            {
                return;
            }

            serializedObject.UpdateIfRequiredOrScript();
            var refreshedArray = serializedObject.FindProperty(propertyPath);
            if (refreshedArray == null || !refreshedArray.isArray || index < 0 || index >= refreshedArray.arraySize)
            {
                return;
            }

            DialogueSerializedPropertyUtility.DeleteArrayElement(refreshedArray, index);
            serializedObject.ApplyModifiedProperties();
            serializedObject.UpdateIfRequiredOrScript();
            onChanged?.Invoke();
        }
    }

    public sealed class DialogueConditionCardBuilder : DialogueArrayCardBuilderBase
    {
        public DialogueConditionCardBuilder(SerializedObject serializedObject, Action onChanged)
            : base(serializedObject, onChanged)
        {
        }

        public void AddCards(VisualElement parent, SerializedProperty conditionsProperty, string title, string timingDescription)
        {
            if (conditionsProperty == null || !conditionsProperty.isArray)
            {
                return;
            }

            var foldout = BuildArrayFoldout($"{title} ({conditionsProperty.arraySize})", timingDescription, conditionsProperty.arraySize);
            AddArrayCommandRow(foldout, conditionsProperty, "Condition", InitializeElement);

            for (var i = 0; i < conditionsProperty.arraySize; i++)
            {
                var condition = conditionsProperty.GetArrayElementAtIndex(i);
                var card = BuildCardFoldout(BuildCardTitle(condition, i));
                AddCardDeleteButton(card, conditionsProperty, i, "Condition");
                AddBody(card, condition);
                foldout.Add(card);
            }

            parent.Add(foldout);
        }

        private static void AddBody(VisualElement parent, SerializedProperty condition)
        {
            DialogueSerializedPropertyUtility.AddRelativeProperty(parent, condition, "ConditionId", "Condition Id");
            DialogueSerializedPropertyUtility.AddRelativeProperty(parent, condition, "Type", "Condition Type");
            DialogueSerializedPropertyUtility.AddRelativeProperty(parent, condition, "TargetId", "Target Id");
            DialogueSerializedPropertyUtility.AddRelativeProperty(parent, condition, "Operator", "Operator");
            DialogueSerializedPropertyUtility.AddRelativeProperty(parent, condition, "StringValue", "String Value");
            DialogueSerializedPropertyUtility.AddRelativeProperty(parent, condition, "IntValue", "Int Value");
            DialogueSerializedPropertyUtility.AddRelativeProperty(parent, condition, "FloatValue", "Float Value");
            DialogueSerializedPropertyUtility.AddRelativeProperty(parent, condition, "BoolValue", "Bool Value");
            DialogueSerializedPropertyUtility.AddRelativeProperty(parent, condition, "CustomData", "Custom Data");
        }

        private static void InitializeElement(SerializedProperty condition)
        {
            DialogueSerializedPropertyUtility.SetString(condition, "ConditionId", string.Empty);
            DialogueSerializedPropertyUtility.SetEnumIndex(condition, "Type", 0);
            DialogueSerializedPropertyUtility.SetString(condition, "TargetId", string.Empty);
            DialogueSerializedPropertyUtility.SetString(condition, "Operator", string.Empty);
            DialogueSerializedPropertyUtility.SetString(condition, "StringValue", string.Empty);
            DialogueSerializedPropertyUtility.SetInt(condition, "IntValue", 0);
            DialogueSerializedPropertyUtility.SetFloat(condition, "FloatValue", 0f);
            DialogueSerializedPropertyUtility.SetBool(condition, "BoolValue", false);
            DialogueSerializedPropertyUtility.ResetCustomData(condition);
        }

        private static string BuildCardTitle(SerializedProperty condition, int index)
        {
            var type = DialogueSerializedPropertyUtility.GetEnumDisplayName(condition, "Type");
            var id = DialogueSerializedPropertyUtility.GetString(condition, "ConditionId");
            var target = DialogueSerializedPropertyUtility.GetString(condition, "TargetId");
            return $"{index + 1}. {type} | {DialogueSerializedPropertyUtility.Fallback(id, "<no id>")} | Target:{DialogueSerializedPropertyUtility.Fallback(target, "<empty>")}";
        }
    }

    public sealed class DialogueActionCardBuilder : DialogueArrayCardBuilderBase
    {
        public DialogueActionCardBuilder(SerializedObject serializedObject, Action onChanged)
            : base(serializedObject, onChanged)
        {
        }

        public void AddCards(VisualElement parent, SerializedProperty actionsProperty, string title, string timingDescription)
        {
            if (actionsProperty == null || !actionsProperty.isArray)
            {
                return;
            }

            var foldout = BuildArrayFoldout($"{title} ({actionsProperty.arraySize})", timingDescription, actionsProperty.arraySize);
            AddArrayCommandRow(foldout, actionsProperty, "Action", InitializeElement);

            for (var i = 0; i < actionsProperty.arraySize; i++)
            {
                var action = actionsProperty.GetArrayElementAtIndex(i);
                var card = BuildCardFoldout(BuildCardTitle(action, i));
                AddCardDeleteButton(card, actionsProperty, i, "Action");
                AddBody(card, action);
                foldout.Add(card);
            }

            parent.Add(foldout);
        }

        private static void AddBody(VisualElement parent, SerializedProperty action)
        {
            DialogueSerializedPropertyUtility.AddRelativeProperty(parent, action, "ActionId", "Action Id");
            DialogueSerializedPropertyUtility.AddRelativeProperty(parent, action, "Type", "Action Type");
            parent.Add(new HelpBox(GetTargetHint(action), HelpBoxMessageType.Info));
            DialogueSerializedPropertyUtility.AddRelativeProperty(parent, action, "TargetId", "Target Id");
            DialogueSerializedPropertyUtility.AddRelativeProperty(parent, action, "StringValue", "String Value");
            DialogueSerializedPropertyUtility.AddRelativeProperty(parent, action, "IntValue", "Int Value");
            DialogueSerializedPropertyUtility.AddRelativeProperty(parent, action, "FloatValue", "Float Value");
            DialogueSerializedPropertyUtility.AddRelativeProperty(parent, action, "BoolValue", "Bool Value");
            DialogueSerializedPropertyUtility.AddRelativeProperty(parent, action, "CustomData", "Custom Data");
        }

        private static void InitializeElement(SerializedProperty action)
        {
            DialogueSerializedPropertyUtility.SetString(action, "ActionId", string.Empty);
            DialogueSerializedPropertyUtility.SetEnumIndex(action, "Type", 0);
            DialogueSerializedPropertyUtility.SetString(action, "TargetId", string.Empty);
            DialogueSerializedPropertyUtility.SetString(action, "StringValue", string.Empty);
            DialogueSerializedPropertyUtility.SetInt(action, "IntValue", 0);
            DialogueSerializedPropertyUtility.SetFloat(action, "FloatValue", 0f);
            DialogueSerializedPropertyUtility.SetBool(action, "BoolValue", false);
            DialogueSerializedPropertyUtility.ResetCustomData(action);
        }

        private static string BuildCardTitle(SerializedProperty action, int index)
        {
            var type = DialogueSerializedPropertyUtility.GetEnumDisplayName(action, "Type");
            var id = DialogueSerializedPropertyUtility.GetString(action, "ActionId");
            var target = DialogueSerializedPropertyUtility.GetString(action, "TargetId");
            return $"{index + 1}. {type} | {DialogueSerializedPropertyUtility.Fallback(id, "<no id>")} | Target:{DialogueSerializedPropertyUtility.Fallback(target, "<empty>")}";
        }

        private static string GetTargetHint(SerializedProperty action)
        {
            var type = DialogueSerializedPropertyUtility.GetEnumName(action, "Type");
            switch (type)
            {
                case "StartDialogue":
                    return "TargetId: fill DialogueId.";
                case "EndDialogue":
                    return "TargetId: usually empty.";
                case "OpenMiniGame":
                    return "TargetId: fill MiniGame entry id or mode id.";
                case "AcceptQuest":
                    return "TargetId: fill QuestId.";
                case "PushQuestSignal":
                    return "TargetId: fill SignalId.";
                case "StartStory":
                    return "TargetId: fill StoryId.";
                case "SetStoryFlag":
                    return "TargetId: fill FlagId.";
                case "LoadScene":
                    return "TargetId: fill scene name.";
                case "RequestCheckpointSave":
                    return "TargetId: usually empty.";
                case "PlayAudioCue":
                    return "TargetId: fill AudioCueDefinition.CueId.";
                case "Custom":
                    return "TargetId/CustomData: interpreted by custom ActionHandler.";
                default:
                    return "TargetId usage depends on Action Type.";
            }
        }
    }

    public sealed class DialogueChoiceCardBuilder : DialogueArrayCardBuilderBase
    {
        private readonly DialogueConditionCardBuilder conditionCardBuilder;
        private readonly DialogueActionCardBuilder actionCardBuilder;

        public DialogueChoiceCardBuilder(
            SerializedObject serializedObject,
            Action onChanged,
            DialogueConditionCardBuilder conditionCardBuilder,
            DialogueActionCardBuilder actionCardBuilder)
            : base(serializedObject, onChanged)
        {
            this.conditionCardBuilder = conditionCardBuilder ?? throw new ArgumentNullException(nameof(conditionCardBuilder));
            this.actionCardBuilder = actionCardBuilder ?? throw new ArgumentNullException(nameof(actionCardBuilder));
        }

        public void AddCards(VisualElement parent, SerializedProperty choicesProperty)
        {
            if (choicesProperty == null || !choicesProperty.isArray)
            {
                return;
            }

            var foldout = BuildArrayFoldout(
                $"Choices ({choicesProperty.arraySize})",
                "文字播放完成后显示给玩家的选项。ChoiceId 必填，运行时点击依赖它。",
                choicesProperty.arraySize);
            AddArrayCommandRow(foldout, choicesProperty, "Choice", InitializeElement);
            foldout.Add(new HelpBox("Choice 点击顺序：先执行 Choice Actions，再执行当前句子的 Exit Actions，最后应用 Behavior。", HelpBoxMessageType.Info));

            for (var i = 0; i < choicesProperty.arraySize; i++)
            {
                var choice = choicesProperty.GetArrayElementAtIndex(i);
                var card = BuildCardFoldout(BuildCardTitle(choice, i));
                AddCardDeleteButton(card, choicesProperty, i, "Choice");
                DialogueSerializedPropertyUtility.AddRelativeProperty(card, choice, "ChoiceId", "Choice Id");
                DialogueSerializedPropertyUtility.AddRelativeProperty(card, choice, "DisplayText", "Display Text");
                DialogueSerializedPropertyUtility.AddRelativeProperty(card, choice, "Behavior", "Behavior");
                DialogueSerializedPropertyUtility.AddRelativeProperty(card, choice, "NextSentenceId", "Next Sentence Id");
                DialogueSerializedPropertyUtility.AddRelativeProperty(card, choice, "HideWhenUnavailable", "Hide When Unavailable");
                DialogueSerializedPropertyUtility.AddRelativeProperty(card, choice, "DisabledText", "Disabled Text");
                conditionCardBuilder.AddCards(card, choice.FindPropertyRelative("Conditions"), "Choice Conditions", "该选项显示或可点击前需要满足的条件。");
                actionCardBuilder.AddCards(card, choice.FindPropertyRelative("Actions"), "Choice Actions", "玩家点击该选项后立即执行，然后再执行当前句子的 Exit Actions。");
                foldout.Add(card);
            }

            parent.Add(foldout);
        }

        private static void InitializeElement(SerializedProperty choice)
        {
            DialogueSerializedPropertyUtility.SetString(choice, "ChoiceId", string.Empty);
            DialogueSerializedPropertyUtility.SetString(choice, "DisplayText", string.Empty);
            DialogueSerializedPropertyUtility.SetEnumIndex(choice, "Behavior", 0);
            DialogueSerializedPropertyUtility.SetString(choice, "NextSentenceId", string.Empty);
            DialogueSerializedPropertyUtility.SetBool(choice, "HideWhenUnavailable", false);
            DialogueSerializedPropertyUtility.SetString(choice, "DisabledText", string.Empty);
            DialogueSerializedPropertyUtility.ClearArray(choice, "Conditions");
            DialogueSerializedPropertyUtility.ClearArray(choice, "Actions");
        }

        private static string BuildCardTitle(SerializedProperty choice, int index)
        {
            var id = DialogueSerializedPropertyUtility.GetString(choice, "ChoiceId");
            var text = DialogueSerializedPropertyUtility.GetString(choice, "DisplayText");
            var behavior = DialogueSerializedPropertyUtility.GetEnumDisplayName(choice, "Behavior");
            return $"{index + 1}. {DialogueSerializedPropertyUtility.Fallback(id, "<empty choice id>")} | {behavior} | {DialogueEditorTextUtility.BuildTextSummary(text, 24)}";
        }
    }
}
