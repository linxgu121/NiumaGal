using System;
using System.Collections.Generic;
using NiumaGal.Dialogue.Data;
using NiumaGal.Enum;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NiumaGal.Editor
{
    public sealed class DialogueEditorSimulator
    {
        private const float CharactersPerSecond = 32f;

        private readonly DialogueAsset asset;
        private readonly Action<int> onSentenceFocused;
        private readonly Dictionary<string, int> sentenceIdToIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly List<string> logLines = new List<string>();

        private VisualElement root;
        private Label statusLabel;
        private Label speakerLabel;
        private Label textLabel;
        private VisualElement choiceContainer;
        private ScrollView logView;
        private Button playButton;
        private Button pauseButton;
        private Button stopButton;
        private Button skipButton;
        private Button advanceButton;
        private EnumField conditionModeField;
        private Toggle manualConditionToggle;

        private DialogueSimulatorConditionMode conditionMode = DialogueSimulatorConditionMode.AllPass;
        private int currentIndex = -1;
        private string fullText = string.Empty;
        private float visibleCharacters;
        private bool isPlaying;
        private bool isPaused;
        private bool waitingForInput;
        private bool tickRegistered;
        private bool playModeHookRegistered;
        private double lastTickTime;

        public DialogueEditorSimulator(DialogueAsset asset, Action<int> onSentenceFocused)
        {
            this.asset = asset;
            this.onSentenceFocused = onSentenceFocused;
        }

        public VisualElement Build()
        {
            root = new VisualElement
            {
                name = "DialogueEditorSimulator"
            };
            root.style.marginTop = 8f;
            root.style.paddingLeft = 8f;
            root.style.paddingRight = 8f;
            root.style.paddingTop = 6f;
            root.style.paddingBottom = 6f;
            root.style.borderLeftWidth = 1f;
            root.style.borderRightWidth = 1f;
            root.style.borderTopWidth = 1f;
            root.style.borderBottomWidth = 1f;
            root.style.borderLeftColor = new Color(0.25f, 0.25f, 0.25f);
            root.style.borderRightColor = new Color(0.25f, 0.25f, 0.25f);
            root.style.borderTopColor = new Color(0.25f, 0.25f, 0.25f);
            root.style.borderBottomColor = new Color(0.25f, 0.25f, 0.25f);
            root.RegisterCallback<DetachFromPanelEvent>(_ => Dispose());

            BuildToolbar(root);
            BuildContent(root);
            BuildLog(root);
            RebuildIndex();
            UpdateStateLabels();
            RegisterTick();
            RegisterPlayModeHook();
            return root;
        }

        public void Dispose()
        {
            Stop();
            UnregisterTick();
            UnregisterPlayModeHook();
        }

        public void Refresh()
        {
            RebuildIndex();
            var sentence = ResolveCurrentSentence();
            if (currentIndex >= 0 && sentence == null)
            {
                Stop();
                return;
            }

            if (sentence != null)
            {
                fullText = sentence.Text ?? string.Empty;
                visibleCharacters = Mathf.Clamp(visibleCharacters, 0f, fullText.Length);
            }

            RenderCurrentSentence();
            UpdateStateLabels();
        }

        public void Stop()
        {
            if (!isPlaying && currentIndex < 0)
            {
                DialogueEditorAudioPreview.Stop();
                return;
            }

            isPlaying = false;
            isPaused = false;
            waitingForInput = false;
            currentIndex = -1;
            fullText = string.Empty;
            visibleCharacters = 0f;
            DialogueEditorAudioPreview.Stop();
            Log("Simulator stopped.");
            RenderCurrentSentence();
            UpdateStateLabels();
        }

        private void BuildToolbar(VisualElement parent)
        {
            var toolbar = new Toolbar
            {
                name = "DialogueSimulatorToolbar"
            };

            playButton = new ToolbarButton(Play)
            {
                text = "Play"
            };
            pauseButton = new ToolbarButton(TogglePause)
            {
                text = "Pause"
            };
            stopButton = new ToolbarButton(Stop)
            {
                text = "Stop"
            };
            skipButton = new ToolbarButton(SkipTypewriter)
            {
                text = "Skip Typewriter"
            };
            advanceButton = new ToolbarButton(Advance)
            {
                text = "Advance"
            };

            conditionModeField = new EnumField("Conditions", conditionMode)
            {
                name = "DialogueSimulatorConditionMode"
            };
            conditionModeField.RegisterValueChangedCallback(evt =>
            {
                conditionMode = evt.newValue is DialogueSimulatorConditionMode mode
                    ? mode
                    : DialogueSimulatorConditionMode.AllPass;
                RenderCurrentSentence();
            });

            manualConditionToggle = new Toggle("Manual Pass")
            {
                value = true
            };
            manualConditionToggle.RegisterValueChangedCallback(_ => RenderCurrentSentence());

            statusLabel = new Label
            {
                name = "DialogueSimulatorStatus"
            };
            statusLabel.style.flexGrow = 1f;

            toolbar.Add(playButton);
            toolbar.Add(pauseButton);
            toolbar.Add(stopButton);
            toolbar.Add(skipButton);
            toolbar.Add(advanceButton);
            toolbar.Add(conditionModeField);
            toolbar.Add(manualConditionToggle);
            toolbar.Add(statusLabel);
            parent.Add(toolbar);
        }

        private void BuildContent(VisualElement parent)
        {
            var content = new VisualElement
            {
                name = "DialogueSimulatorContent"
            };
            content.style.marginTop = 6f;
            content.style.flexDirection = FlexDirection.Row;

            var linePanel = new VisualElement
            {
                name = "DialogueSimulatorLinePanel"
            };
            linePanel.style.flexGrow = 1f;
            linePanel.style.marginRight = 8f;

            speakerLabel = new Label("Speaker")
            {
                name = "DialogueSimulatorSpeaker"
            };
            speakerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            textLabel = new Label("Click Play to preview dialogue locally.")
            {
                name = "DialogueSimulatorText"
            };
            textLabel.style.whiteSpace = WhiteSpace.Normal;
            textLabel.style.minHeight = 64f;
            textLabel.style.marginTop = 4f;

            linePanel.Add(speakerLabel);
            linePanel.Add(textLabel);

            choiceContainer = new VisualElement
            {
                name = "DialogueSimulatorChoices"
            };
            choiceContainer.style.width = 260f;
            choiceContainer.style.flexShrink = 0f;

            content.Add(linePanel);
            content.Add(choiceContainer);
            parent.Add(content);
        }

        private void BuildLog(VisualElement parent)
        {
            logView = new ScrollView(ScrollViewMode.Vertical)
            {
                name = "DialogueSimulatorLog"
            };
            logView.style.maxHeight = 90f;
            logView.style.marginTop = 6f;
            parent.Add(logView);
        }

        private void Play()
        {
            if (asset == null)
            {
                Log("No DialogueAsset selected.");
                return;
            }

            RebuildIndex();
            logLines.Clear();
            RefreshLog();
            LogActions("OnStart", asset.OnStartActions);

            var startIndex = ResolveStartIndex();
            if (startIndex < 0)
            {
                Log("No playable sentence found.");
                Stop();
                return;
            }

            isPlaying = true;
            isPaused = false;
            lastTickTime = EditorApplication.timeSinceStartup;
            EnterSentence(startIndex);
        }

        private void TogglePause()
        {
            if (!isPlaying)
            {
                return;
            }

            isPaused = !isPaused;
            pauseButton.text = isPaused ? "Resume" : "Pause";
            UpdateStateLabels();
        }

        private void SkipTypewriter()
        {
            if (!isPlaying || currentIndex < 0)
            {
                return;
            }

            visibleCharacters = fullText.Length;
            waitingForInput = true;
            RenderCurrentSentence();
            UpdateStateLabels();
        }

        private void Advance()
        {
            if (!isPlaying || currentIndex < 0)
            {
                return;
            }

            var sentence = ResolveCurrentSentence();
            if (sentence == null)
            {
                EndSimulation(true);
                return;
            }

            if (visibleCharacters < fullText.Length)
            {
                SkipTypewriter();
                return;
            }

            if (sentence.Choices != null && sentence.Choices.Length > 0)
            {
                Log("Waiting for choice.");
                return;
            }

            LogActions("ExitActions", sentence.ExitActions);
            var nextIndex = currentIndex + 1;
            if (nextIndex >= (asset?.Sentences?.Count ?? 0))
            {
                EndSimulation(true);
                return;
            }

            EnterSentence(nextIndex);
        }

        private void Tick()
        {
            if (!isPlaying || isPaused || waitingForInput || currentIndex < 0)
            {
                return;
            }

            var now = EditorApplication.timeSinceStartup;
            var deltaTime = Mathf.Clamp((float)(now - lastTickTime), 0f, 0.2f);
            lastTickTime = now;
            visibleCharacters += CharactersPerSecond * deltaTime;
            if (visibleCharacters >= fullText.Length)
            {
                visibleCharacters = fullText.Length;
                waitingForInput = true;
            }

            RenderCurrentSentence();
            UpdateStateLabels();
        }

        private void EnterSentence(int index)
        {
            var sentence = ResolveSentence(index);
            if (sentence == null)
            {
                EndSimulation(true);
                return;
            }

            currentIndex = index;
            visibleCharacters = 0f;
            waitingForInput = false;
            fullText = sentence.Text ?? string.Empty;
            onSentenceFocused?.Invoke(index);

            if (!EvaluateConditions(sentence.Conditions))
            {
                Log($"Sentence #{index + 1} conditions failed. Simulator stopped before executing side effects.");
                isPlaying = false;
                isPaused = false;
                waitingForInput = false;
                DialogueEditorAudioPreview.Stop();
                speakerLabel.text = "Simulator";
                textLabel.text = "[条件未满足，模拟器已停止]";
                choiceContainer.Clear();
                UpdateStateLabels();
                return;
            }

            Log($"Enter sentence #{index + 1}: {ResolveSentenceTitle(sentence, index)}");
            LogActions("EnterActions", sentence.EnterActions);
            if (sentence.VoiceClip != null && !DialogueEditorAudioPreview.Play(sentence.VoiceClip, out var error))
            {
                Log($"Voice preview failed: {error}");
            }

            RenderCurrentSentence();
            UpdateStateLabels();
        }

        private void EndSimulation(bool completed)
        {
            LogActions(completed ? "OnComplete" : "OnAbort", completed ? asset?.OnCompleteActions : asset?.OnAbortActions);
            isPlaying = false;
            isPaused = false;
            waitingForInput = false;
            DialogueEditorAudioPreview.Stop();
            Log(completed ? "Dialogue completed." : "Dialogue aborted.");
            UpdateStateLabels();
        }

        private void RenderCurrentSentence()
        {
            var sentence = ResolveCurrentSentence();
            if (sentence == null)
            {
                speakerLabel.text = "Simulator";
                textLabel.text = asset == null ? "No DialogueAsset selected." : "Click Play to preview dialogue locally.";
                choiceContainer.Clear();
                UpdateButtonState();
                return;
            }

            speakerLabel.text = string.IsNullOrWhiteSpace(sentence.Speaker) ? "Narration" : sentence.Speaker;
            var count = Mathf.Clamp(Mathf.FloorToInt(visibleCharacters), 0, fullText.Length);
            textLabel.text = fullText.Substring(0, count);
            RenderChoices(sentence);
            UpdateButtonState();
        }

        private void RenderChoices(DialogueSentence sentence)
        {
            choiceContainer.Clear();
            if (!isPlaying || !waitingForInput || sentence?.Choices == null || sentence.Choices.Length == 0)
            {
                return;
            }

            for (var i = 0; i < sentence.Choices.Length; i++)
            {
                var choice = sentence.Choices[i];
                if (choice == null)
                {
                    continue;
                }

                var isAvailable = EvaluateConditions(choice.Conditions);
                if (!isAvailable && choice.HideWhenUnavailable)
                {
                    continue;
                }

                var index = i;
                var text = isAvailable
                    ? choice.DisplayText
                    : string.IsNullOrWhiteSpace(choice.DisabledText) ? choice.DisplayText : choice.DisabledText;
                var button = new Button(() => SelectChoice(index))
                {
                    text = string.IsNullOrWhiteSpace(text) ? $"Choice {i + 1}" : text
                };
                button.SetEnabled(isAvailable);
                button.style.marginBottom = 4f;
                choiceContainer.Add(button);
            }
        }

        private void SelectChoice(int choiceIndex)
        {
            var sentence = ResolveCurrentSentence();
            if (sentence?.Choices == null || choiceIndex < 0 || choiceIndex >= sentence.Choices.Length)
            {
                return;
            }

            var choice = sentence.Choices[choiceIndex];
            if (choice == null || !EvaluateConditions(choice.Conditions))
            {
                Log("Choice blocked by simulator conditions.");
                return;
            }

            Log($"Select choice: {DialogueEditorTextUtility.BuildTextSummary(choice.DisplayText, 36)}");
            LogActions("ChoiceActions", choice.Actions);
            LogActions("ExitActions", sentence.ExitActions);

            switch (choice.Behavior)
            {
                case DialogueChoiceBehavior.Continue:
                    EnterOrEnd(currentIndex + 1);
                    break;
                case DialogueChoiceBehavior.JumpToSentence:
                    JumpToSentence(choice.NextSentenceId);
                    break;
                case DialogueChoiceBehavior.EndDialogue:
                    EndSimulation(true);
                    break;
                case DialogueChoiceBehavior.Custom:
                    if (string.IsNullOrWhiteSpace(choice.NextSentenceId))
                    {
                        EndSimulation(true);
                    }
                    else
                    {
                        JumpToSentence(choice.NextSentenceId);
                    }
                    break;
                default:
                    EndSimulation(true);
                    break;
            }
        }

        private void JumpToSentence(string sentenceId)
        {
            if (string.IsNullOrWhiteSpace(sentenceId) || !sentenceIdToIndex.TryGetValue(sentenceId, out var index))
            {
                Log($"Jump target missing: {sentenceId}");
                EndSimulation(true);
                return;
            }

            EnterSentence(index);
        }

        private void EnterOrEnd(int index)
        {
            if (index < 0 || index >= (asset?.Sentences?.Count ?? 0))
            {
                EndSimulation(true);
                return;
            }

            EnterSentence(index);
        }

        private bool EvaluateConditions(DialogueConditionData[] conditions)
        {
            if (conditions == null || conditions.Length == 0)
            {
                return true;
            }

            return conditionMode switch
            {
                DialogueSimulatorConditionMode.AllFail => false,
                DialogueSimulatorConditionMode.Manual => manualConditionToggle == null || manualConditionToggle.value,
                _ => true
            };
        }

        private DialogueSentence ResolveCurrentSentence()
        {
            return ResolveSentence(currentIndex);
        }

        private DialogueSentence ResolveSentence(int index)
        {
            var sentences = asset?.Sentences;
            return sentences != null && index >= 0 && index < sentences.Count ? sentences[index] : null;
        }

        private int ResolveStartIndex()
        {
            if (asset?.Sentences == null || asset.Sentences.Count == 0)
            {
                return -1;
            }

            if (!string.IsNullOrWhiteSpace(asset.StartSentenceId) &&
                sentenceIdToIndex.TryGetValue(asset.StartSentenceId, out var index))
            {
                return index;
            }

            return 0;
        }

        private void RebuildIndex()
        {
            sentenceIdToIndex.Clear();
            var sentences = asset?.Sentences;
            if (sentences == null)
            {
                return;
            }

            for (var i = 0; i < sentences.Count; i++)
            {
                var id = sentences[i]?.SentenceId;
                if (!string.IsNullOrWhiteSpace(id) && !sentenceIdToIndex.ContainsKey(id))
                {
                    sentenceIdToIndex[id] = i;
                }
            }
        }

        private void RegisterTick()
        {
            if (tickRegistered)
            {
                return;
            }

            lastTickTime = EditorApplication.timeSinceStartup;
            tickRegistered = true;
            EditorApplication.update += Tick;
        }

        private void UnregisterTick()
        {
            if (!tickRegistered)
            {
                return;
            }

            tickRegistered = false;
            EditorApplication.update -= Tick;
        }

        private void RegisterPlayModeHook()
        {
            if (playModeHookRegistered)
            {
                return;
            }

            playModeHookRegistered = true;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        }

        private void UnregisterPlayModeHook()
        {
            if (!playModeHookRegistered)
            {
                return;
            }

            playModeHookRegistered = false;
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        }

        private void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.EnteredPlayMode)
            {
                Stop();
                UnregisterTick();
                return;
            }

            if (state == PlayModeStateChange.EnteredEditMode)
            {
                RegisterTick();
            }
        }

        private void LogActions(string scope, DialogueActionData[] actions)
        {
            if (actions == null || actions.Length == 0)
            {
                return;
            }

            for (var i = 0; i < actions.Length; i++)
            {
                var action = actions[i];
                if (action == null || action.Type == DialogueActionType.None)
                {
                    continue;
                }

                Log($"{scope}: {action.Type} Target={action.TargetId} (simulated only)");
            }
        }

        private void Log(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            logLines.Add($"[{DateTime.Now:HH:mm:ss}] {line}");
            if (logLines.Count > 80)
            {
                logLines.RemoveAt(0);
            }

            RefreshLog();
        }

        private void RefreshLog()
        {
            if (logView == null)
            {
                return;
            }

            logView.Clear();
            for (var i = 0; i < logLines.Count; i++)
            {
                logView.Add(new Label(logLines[i]));
            }
        }

        private void UpdateStateLabels()
        {
            if (statusLabel != null)
            {
                statusLabel.text = isPlaying
                    ? isPaused ? "Paused" : waitingForInput ? "Waiting Input" : "Playing"
                    : "Stopped";
            }

            UpdateButtonState();
        }

        private void UpdateButtonState()
        {
            playButton?.SetEnabled(asset != null && !isPlaying);
            pauseButton?.SetEnabled(isPlaying);
            stopButton?.SetEnabled(isPlaying || currentIndex >= 0);
            skipButton?.SetEnabled(isPlaying && currentIndex >= 0 && visibleCharacters < fullText.Length);
            advanceButton?.SetEnabled(isPlaying && waitingForInput);
            if (pauseButton != null)
            {
                pauseButton.text = isPaused ? "Resume" : "Pause";
            }

            manualConditionToggle?.SetEnabled(conditionMode == DialogueSimulatorConditionMode.Manual);
        }

        private static string ResolveSentenceTitle(DialogueSentence sentence, int index)
        {
            return string.IsNullOrWhiteSpace(sentence?.SentenceId) ? $"#{index + 1}" : sentence.SentenceId;
        }
    }

    public enum DialogueSimulatorConditionMode
    {
        AllPass = 0,
        AllFail = 1,
        Manual = 2
    }
}
