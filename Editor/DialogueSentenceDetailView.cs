using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NiumaGal.Editor
{
    public sealed class DialogueSentenceDetailView
    {
        private readonly DialogueAssetEditorContext context;
        private readonly DialogueSpeakerEditorHelper speakerEditorHelper;
        private readonly Action onChanged;
        private readonly DialogueConditionCardBuilder conditionCardBuilder;
        private readonly DialogueActionCardBuilder actionCardBuilder;
        private readonly DialogueChoiceCardBuilder choiceCardBuilder;

        private ScrollView scrollView;

        public DialogueSentenceDetailView(
            DialogueAssetEditorContext context,
            DialogueSpeakerEditorHelper speakerEditorHelper,
            Action onChanged)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            this.speakerEditorHelper = speakerEditorHelper ?? throw new ArgumentNullException(nameof(speakerEditorHelper));
            this.onChanged = onChanged;
            conditionCardBuilder = new DialogueConditionCardBuilder(context.SerializedObject, onChanged);
            actionCardBuilder = new DialogueActionCardBuilder(context.SerializedObject, onChanged);
            choiceCardBuilder = new DialogueChoiceCardBuilder(context.SerializedObject, onChanged, conditionCardBuilder, actionCardBuilder);
        }

        public VisualElement Build()
        {
            scrollView = new ScrollView
            {
                name = "DialogueSentenceDetailPanel"
            };
            scrollView.style.flexGrow = 1f;
            scrollView.style.minWidth = 360f;
            return scrollView;
        }

        public void Rebuild(int originalIndex)
        {
            if (scrollView == null)
            {
                return;
            }

            // TODO(Phase 6+): Rebuilding details resets TextField focus and Foldout state.
            // Later phases should preserve detail UI state or update bound fields incrementally.
            scrollView.Unbind();
            scrollView.Clear();

            var sentencesProperty = context.SerializedObject?.FindProperty("Sentences");
            if (sentencesProperty == null || originalIndex < 0 || originalIndex >= sentencesProperty.arraySize)
            {
                scrollView.Add(new HelpBox("Select a sentence to edit details.", HelpBoxMessageType.Info));
                return;
            }

            var sentenceProperty = sentencesProperty.GetArrayElementAtIndex(originalIndex);
            var title = new Label($"Sentence #{originalIndex}")
            {
                name = "DialogueSentenceDetailTitle"
            };
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            scrollView.Add(title);

            DialogueSerializedPropertyUtility.AddRelativeProperty(scrollView, sentenceProperty, "SentenceId", "Sentence Id");
            speakerEditorHelper.AddSpeakerEditor(scrollView, sentenceProperty);
            AddTextEditor(sentenceProperty);
            AddVoiceEditor(sentenceProperty);
            DialogueSerializedPropertyUtility.AddRelativeProperty(scrollView, sentenceProperty, "NarrativeCategory", "Narrative Category");
            conditionCardBuilder.AddCards(scrollView, sentenceProperty.FindPropertyRelative("Conditions"), "Sentence Conditions", "进入本句前需要满足的条件。不满足时本句不会被播放。");
            actionCardBuilder.AddCards(scrollView, sentenceProperty.FindPropertyRelative("EnterActions"), "Enter Actions", "进入本句并开始播放时执行。不要把离开本句后的行为填在这里。");
            actionCardBuilder.AddCards(scrollView, sentenceProperty.FindPropertyRelative("ExitActions"), "Exit Actions", "本句完整推进离开时执行。Choice 被点击后会先执行 Choice Actions，再执行这里。");
            choiceCardBuilder.AddCards(scrollView, sentenceProperty.FindPropertyRelative("Choices"));

            scrollView.Bind(context.SerializedObject);
        }

        private void AddTextEditor(SerializedProperty sentenceProperty)
        {
            var textProperty = sentenceProperty.FindPropertyRelative("Text");
            if (textProperty == null)
            {
                return;
            }

            var textField = new TextField("Text")
            {
                name = "DialogueSentenceTextField",
                multiline = true,
                value = textProperty.stringValue ?? string.Empty
            };
            textField.style.minHeight = 160f;
            textField.style.whiteSpace = WhiteSpace.Normal;
            textField.RegisterCallback<AttachToPanelEvent>(_ => DialogueEditorTextUtility.ApplyMultilineTextInputStyle(textField));

            var statsLabel = new Label
            {
                name = "DialogueSentenceTextStats"
            };
            statsLabel.style.marginBottom = 6f;
            DialogueEditorTextUtility.UpdateTextStats(statsLabel, textField.value);

            textField.RegisterValueChangedCallback(evt =>
            {
                textProperty.stringValue = evt.newValue ?? string.Empty;
                context.SerializedObject.ApplyModifiedProperties();
                DialogueEditorTextUtility.UpdateTextStats(statsLabel, evt.newValue);
                // TODO(Phase 6+): Keep the left list summary in sync while typing without
                // calling RebuildIndex() on every character.
            });

            scrollView.Add(textField);
            scrollView.Add(statsLabel);
        }

        private void AddVoiceEditor(SerializedProperty sentenceProperty)
        {
            var voiceProperty = sentenceProperty.FindPropertyRelative("VoiceClip");
            if (voiceProperty == null)
            {
                return;
            }

            var row = new VisualElement
            {
                name = "DialogueVoiceClipRow"
            };
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 6f;

            var clipField = new ObjectField("Voice Clip")
            {
                name = "DialogueVoiceClipField",
                objectType = typeof(AudioClip),
                allowSceneObjects = false,
                value = voiceProperty.objectReferenceValue
            };
            clipField.style.flexGrow = 1f;

            var playButton = new Button
            {
                text = "Preview"
            };
            var stopButton = new Button(DialogueEditorAudioPreview.Stop)
            {
                text = "Stop"
            };
            playButton.clicked += () =>
            {
                if (!DialogueEditorAudioPreview.Play(clipField.value as AudioClip, out var error))
                {
                    Debug.LogWarning($"[NiumaGalEditor] Voice 试听失败：{error}");
                }
            };

            void UpdateButtons()
            {
                playButton.SetEnabled(DialogueEditorAudioPreview.IsSupported && clipField.value is AudioClip);
                stopButton.SetEnabled(DialogueEditorAudioPreview.IsSupported);
            }

            clipField.RegisterValueChangedCallback(evt =>
            {
                DialogueEditorAudioPreview.Stop();
                voiceProperty.objectReferenceValue = evt.newValue;
                context.SerializedObject.ApplyModifiedProperties();
                UpdateButtons();
                onChanged?.Invoke();
            });

            UpdateButtons();
            row.Add(clipField);
            row.Add(playButton);
            row.Add(stopButton);
            scrollView.Add(row);

            if (!DialogueEditorAudioPreview.IsSupported)
            {
                scrollView.Add(new HelpBox("当前 Unity 版本无法通过 AudioUtil 试听 VoiceClip。", HelpBoxMessageType.Info));
            }
        }
    }
}
