using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NiumaGal.Dialogue.Data;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NiumaGal.Editor
{
    // TODO(Phase 6): Split this God Class into list/detail/card/speaker helper views before adding more editor features.
    public sealed class DialogueAssetEditorView
    {
        private sealed class SentenceListItem
        {
            public int OriginalIndex;
            public string SentenceId;
            public string Speaker;
            public string Text;
            public bool HasVoice;
            public int ChoiceCount;
        }

        private static readonly Regex RichTextTagRegex = new Regex(@"</?[a-zA-Z][^>]*>", RegexOptions.Compiled);

        private readonly DialogueAssetEditorContext context;
        private readonly DialogueAssetEditorSession session;
        private readonly List<SentenceListItem> sentenceItems = new List<SentenceListItem>();

        private VisualElement root;
        private TextField searchField;
        private ListView sentenceListView;
        private Label selectionLabel;
        private Label summaryLabel;
        private VisualElement validationPanel;
        private VisualElement detailPanel;
        private Button addButton;
        private Button duplicateButton;
        private Button deleteButton;
        private Button moveUpButton;
        private Button moveDownButton;
        private DialogueValidationReport validationReport;

        public DialogueAssetEditorView(DialogueAssetEditorContext context, DialogueAssetEditorSession session)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            this.session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public VisualElement Build()
        {
            root = new VisualElement
            {
                name = "DialogueAssetEditorRoot"
            };

            root.style.flexGrow = 1f;
            root.style.paddingLeft = 8f;
            root.style.paddingRight = 8f;
            root.style.paddingTop = 8f;
            root.style.paddingBottom = 8f;
            root.RegisterCallback<DetachFromPanelEvent>(_ => DialogueEditorAudioPreview.Stop());

            session.Load(context.Asset);
            BuildToolbar(root);

            if (context.Asset == null || context.SerializedObject == null)
            {
                root.Add(new HelpBox("No DialogueAsset selected.", HelpBoxMessageType.Info));
                return root;
            }

            context.SerializedObject.UpdateIfRequiredOrScript();

            BuildAssetInfo(root);
            BuildValidationPanel(root);
            BuildEditorBody(root);
            RebuildIndex();

            return root;
        }

        // Public refresh entry for host windows and future validation/graph panels.
        public void Refresh()
        {
            RebuildIndex();
        }

        private void RebuildIndex()
        {
            if (context.Asset == null || context.SerializedObject == null)
            {
                sentenceItems.Clear();
                sentenceListView?.Rebuild();
                UpdateSelectionInfo(null);
                UpdateSummary(null);
                RebuildDetails(-1);
                return;
            }

            context.SerializedObject.UpdateIfRequiredOrScript();
            // Phase 4 rebuilds the sentence list snapshot, selected details, action/condition/choice cards.
            // Later phases extend this into validation caches, search summary caches and graph caches.
            // TODO(Phase 6+): Structural operations currently trigger ListView.Rebuild(). This is fine
            // for short dialogues, but large assets should use cached indexes and incremental refreshes.
            RefreshSentenceItems();
            RunValidation(false);
        }

        private void BuildToolbar(VisualElement parent)
        {
            var toolbar = new Toolbar
            {
                name = "DialogueAssetEditorToolbar"
            };

            if (context.HostKind == DialogueAssetEditorHostKind.EditorWindow)
            {
                var assetSelector = new ObjectField("Dialogue Asset")
                {
                    name = "DialogueAssetSelector",
                    objectType = typeof(DialogueAsset),
                    allowSceneObjects = false,
                    value = context.Asset
                };
                assetSelector.style.minWidth = 240f;
                assetSelector.RegisterValueChangedCallback(evt =>
                {
                    context.OnAssetSelected?.Invoke(evt.newValue as DialogueAsset);
                });
                toolbar.Add(assetSelector);
            }

            searchField = new TextField
            {
                name = "SentenceSearchField",
                value = session.SearchText
            };
            searchField.style.minWidth = 180f;
            searchField.RegisterValueChangedCallback(evt =>
            {
                session.SetSearchText(context.Asset, evt.newValue);
                RebuildIndex();
            });

            var validateButton = new ToolbarButton(() => RunValidation(true))
            {
                text = "Validate"
            };

            var focusStartButton = new ToolbarButton(FocusStartSentence)
            {
                text = "Focus Start"
            };

            var graphButton = new ToolbarButton(OpenGraphPreview)
            {
                text = "Open Graph Preview"
            };

            var rebuildButton = new ToolbarButton(RebuildIndex)
            {
                text = "Rebuild Index"
            };

            toolbar.Add(new Label("Search"));
            toolbar.Add(searchField);
            toolbar.Add(new ToolbarSpacer());
            toolbar.Add(validateButton);
            toolbar.Add(focusStartButton);
            toolbar.Add(graphButton);
            toolbar.Add(rebuildButton);
            parent.Add(toolbar);
        }

        private void BuildAssetInfo(VisualElement parent)
        {
            var container = new VisualElement
            {
                name = "DialogueAssetInfo"
            };
            container.style.marginTop = 8f;
            container.style.marginBottom = 8f;

            var title = new Label("Asset Info")
            {
                name = "DialogueAssetInfoTitle"
            };
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            container.Add(title);

            AddProperty(container, "DialogueId");
            AddProperty(container, "DisplayName");
            AddProperty(container, "StartSentenceId");
            AddActionCards(container, context.SerializedObject.FindProperty("OnStartActions"), "On Start Actions", "对话开始时执行，适合锁输入、切镜头、播放音效等入口行为。");
            AddActionCards(container, context.SerializedObject.FindProperty("OnCompleteActions"), "On Complete Actions", "对话正常完成时执行，适合推进任务、进入小游戏、切换剧情节点。");
            AddActionCards(container, context.SerializedObject.FindProperty("OnAbortActions"), "On Abort Actions", "对话被强制关闭或中断时执行。通常可以留空，避免误写进度。");
            AddSpeakerCatalogSelector(container);

            summaryLabel = new Label
            {
                name = "DialogueAssetSummary"
            };
            summaryLabel.style.marginTop = 4f;
            container.Add(summaryLabel);

            container.Bind(context.SerializedObject);
            parent.Add(container);
        }

        private void BuildValidationPanel(VisualElement parent)
        {
            validationPanel = new ScrollView(ScrollViewMode.Vertical)
            {
                name = "DialogueValidationPanel"
            };
            validationPanel.style.marginTop = 4f;
            validationPanel.style.marginBottom = 8f;
            validationPanel.style.paddingLeft = 6f;
            validationPanel.style.paddingRight = 6f;
            validationPanel.style.paddingTop = 6f;
            validationPanel.style.paddingBottom = 6f;
            validationPanel.style.borderLeftWidth = 2f;
            validationPanel.style.borderLeftColor = new Color(0.45f, 0.45f, 0.45f, 1f);
            validationPanel.style.maxHeight = 220f;
            validationPanel.style.flexShrink = 0f;
            parent.Add(validationPanel);
        }

        private void RunValidation(bool logSummary)
        {
            if (context.Asset == null)
            {
                validationReport = null;
                RefreshValidationPanel();
                return;
            }

            var settings = NiumaGalEditorSettings.instance;
            var speakerCatalog = settings.ResolveSpeakerCatalog(context.Asset);
            validationReport = DialogueValidationService.Validate(context.Asset, speakerCatalog, settings.WarnWhenSpeakerEmpty);
            RefreshValidationPanel();

            if (logSummary && validationReport != null)
            {
                Debug.Log($"[NiumaGalEditor] Validate {context.Asset.name}: Errors={validationReport.ErrorCount}, Warnings={validationReport.WarningCount}, Info={validationReport.InfoCount}");
            }
        }

        private void RefreshValidationPanel()
        {
            if (validationPanel == null)
            {
                return;
            }

            validationPanel.Clear();
            if (context.Asset == null)
            {
                validationPanel.Add(new HelpBox("No DialogueAsset selected. Validation is unavailable.", HelpBoxMessageType.Info));
                return;
            }

            if (validationReport == null)
            {
                validationPanel.Add(new HelpBox("Validation has not run yet. Click Validate or edit the asset to refresh.", HelpBoxMessageType.Info));
                return;
            }

            var summaryType = validationReport.ErrorCount > 0
                ? HelpBoxMessageType.Error
                : validationReport.WarningCount > 0
                    ? HelpBoxMessageType.Warning
                    : HelpBoxMessageType.Info;
            validationPanel.Add(new HelpBox(
                $"Validation: Error {validationReport.ErrorCount} / Warning {validationReport.WarningCount} / Info {validationReport.InfoCount}. Sentences {validationReport.SentenceCount}, Characters {validationReport.CharacterCount}, Estimated Read {validationReport.EstimatedReadSeconds}s, Branch Sentences {validationReport.BranchSentenceCount}.",
                summaryType));

            var items = validationReport.Items ?? Array.Empty<DialogueValidationItem>();
            var max = Mathf.Min(items.Length, 30);
            for (var i = 0; i < max; i++)
            {
                AddValidationItemRow(validationPanel, items[i]);
            }

            if (items.Length > max)
            {
                validationPanel.Add(new HelpBox($"Showing first {max} validation items. {items.Length - max} more items are hidden for now.", HelpBoxMessageType.Info));
            }
        }

        private void AddValidationItemRow(VisualElement parent, DialogueValidationItem item)
        {
            if (item == null)
            {
                return;
            }

            var row = new VisualElement
            {
                name = "DialogueValidationItemRow"
            };
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = 3f;

            var label = new Label($"[{item.Severity}] {item.Code}: {item.Message}")
            {
                name = "DialogueValidationItemLabel"
            };
            label.style.flexGrow = 1f;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.color = ResolveValidationColor(item.Severity);
            row.Add(label);

            if (item.SentenceIndex >= 0)
            {
                var targetIndex = item.SentenceIndex;
                row.Add(new Button(() =>
                {
                    DialogueEditorAudioPreview.Stop();
                    session.SetSelectedSentence(context.Asset, targetIndex);
                    RebuildIndex();
                })
                {
                    text = $"Focus #{targetIndex + 1}"
                });
            }

            parent.Add(row);
        }
        private static Color ResolveValidationColor(DialogueValidationSeverity severity)
        {
            switch (severity)
            {
                case DialogueValidationSeverity.Error:
                    return new Color(1f, 0.35f, 0.35f);
                case DialogueValidationSeverity.Warning:
                    return new Color(1f, 0.68f, 0.25f);
                default:
                    return new Color(0.72f, 0.86f, 1f);
            }
        }

        private void FocusStartSentence()
        {
            if (context.Asset == null)
            {
                return;
            }

            var graph = DialogueValidationService.BuildGraph(context.Asset);
            if (graph.StartIndex < 0)
            {
                return;
            }

            DialogueEditorAudioPreview.Stop();
            session.SetSelectedSentence(context.Asset, graph.StartIndex);
            RebuildIndex();
        }

        private void OpenGraphPreview()
        {
            if (context.Asset == null)
            {
                Debug.LogWarning("[NiumaGalEditor] No DialogueAsset selected. Graph Preview cannot open.");
                return;
            }

            DialogueGraphPreviewWindow.Open(context.Asset);
        }

        private void BuildEditorBody(VisualElement parent)
        {
            var body = new VisualElement
            {
                name = "DialogueEditorBody"
            };
            body.style.flexDirection = FlexDirection.Row;
            body.style.flexGrow = 1f;
            body.style.minHeight = 340f;

            var listPanel = BuildSentenceListPanel();
            detailPanel = BuildDetailPanel();

            body.Add(listPanel);
            body.Add(detailPanel);
            parent.Add(body);
        }

        private VisualElement BuildSentenceListPanel()
        {
            var container = new VisualElement
            {
                name = "DialogueSentenceListPanel"
            };
            container.style.width = 420f;
            container.style.minWidth = 320f;
            container.style.marginRight = 8f;
            container.style.flexShrink = 0f;

            var title = new Label("Sentences")
            {
                name = "DialogueSentenceListTitle"
            };
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            container.Add(title);

            var buttonRow = new Toolbar
            {
                name = "DialogueSentenceCommandToolbar"
            };
            addButton = new ToolbarButton(AddSentence) { text = "Add" };
            duplicateButton = new ToolbarButton(DuplicateSelectedSentence) { text = "Duplicate" };
            deleteButton = new ToolbarButton(DeleteSelectedSentence) { text = "Delete" };
            moveUpButton = new ToolbarButton(() => MoveSelectedSentence(-1)) { text = "Up" };
            moveDownButton = new ToolbarButton(() => MoveSelectedSentence(1)) { text = "Down" };
            buttonRow.Add(addButton);
            buttonRow.Add(duplicateButton);
            buttonRow.Add(deleteButton);
            buttonRow.Add(moveUpButton);
            buttonRow.Add(moveDownButton);
            container.Add(buttonRow);

            sentenceListView = new ListView
            {
                name = "DialogueSentenceList",
                itemsSource = sentenceItems,
                fixedItemHeight = 30f,
                selectionType = SelectionType.Single,
                makeItem = MakeSentenceItem,
                bindItem = BindSentenceItem
            };
            sentenceListView.style.flexGrow = 1f;
            sentenceListView.selectionChanged += OnSentenceSelectionChanged;

            container.Add(sentenceListView);

            selectionLabel = new Label
            {
                name = "DialogueSentenceSelectionInfo"
            };
            selectionLabel.style.marginTop = 6f;
            container.Add(selectionLabel);

            return container;
        }

        private VisualElement BuildDetailPanel()
        {
            var container = new ScrollView
            {
                name = "DialogueSentenceDetailPanel"
            };
            container.style.flexGrow = 1f;
            container.style.minWidth = 360f;
            return container;
        }

        private void AddProperty(VisualElement parent, string propertyName)
        {
            var property = context.SerializedObject.FindProperty(propertyName);
            if (property != null)
            {
                parent.Add(new PropertyField(property));
            }
        }

        private VisualElement MakeSentenceItem()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;

            var index = new Label
            {
                name = "SentenceIndex"
            };
            index.style.width = 44f;

            var content = new Label
            {
                name = "SentenceSummary"
            };
            content.style.flexGrow = 1f;
            content.style.unityTextOverflowPosition = TextOverflowPosition.End;
            content.style.overflow = Overflow.Hidden;

            row.Add(index);
            row.Add(content);
            return row;
        }

        private void BindSentenceItem(VisualElement element, int index)
        {
            var item = index >= 0 && index < sentenceItems.Count ? sentenceItems[index] : null;

            var indexLabel = element.Q<Label>("SentenceIndex");
            if (indexLabel != null)
            {
                indexLabel.text = item != null ? $"#{item.OriginalIndex}" : "#-";
            }

            var contentLabel = element.Q<Label>("SentenceSummary");
            if (contentLabel != null)
            {
                contentLabel.text = BuildSentenceSummary(item);
            }
        }

        private void OnSentenceSelectionChanged(IEnumerable<object> selectedItems)
        {
            SentenceListItem selected = null;
            foreach (var item in selectedItems)
            {
                selected = item as SentenceListItem;
                break;
            }

            var originalIndex = selected?.OriginalIndex ?? -1;
            DialogueEditorAudioPreview.Stop();
            session.SetSelectedSentence(context.Asset, originalIndex);
            UpdateSelectionInfo(selected);
            RebuildDetails(originalIndex);
            UpdateCommandState();
        }

        private void RefreshSentenceItems()
        {
            sentenceItems.Clear();

            var sentencesProperty = GetSentencesProperty();
            var search = session.SearchText ?? string.Empty;
            if (sentencesProperty != null && sentencesProperty.isArray)
            {
                for (var i = 0; i < sentencesProperty.arraySize; i++)
                {
                    var element = sentencesProperty.GetArrayElementAtIndex(i);
                    var item = BuildItem(element, i);
                    if (MatchesSearch(item, search))
                    {
                        sentenceItems.Add(item);
                    }
                }
            }

            sentenceListView?.Rebuild();
            RestoreSelection();
            UpdateSummary(sentencesProperty);
            UpdateCommandState();
        }

        private void RestoreSelection()
        {
            if (sentenceListView == null || session.SelectedSentenceIndex < 0)
            {
                UpdateSelectionInfo(null);
                RebuildDetails(-1);
                return;
            }

            var filteredIndex = sentenceItems.FindIndex(item => item.OriginalIndex == session.SelectedSentenceIndex);
            if (filteredIndex >= 0)
            {
                sentenceListView.SetSelectionWithoutNotify(new[] { filteredIndex });
                UpdateSelectionInfo(sentenceItems[filteredIndex]);
                RebuildDetails(session.SelectedSentenceIndex);
                return;
            }

            sentenceListView.ClearSelection();
            UpdateSelectionInfo(null);
            RebuildDetails(-1);
        }

        private void RebuildDetails(int originalIndex)
        {
            if (detailPanel == null)
            {
                return;
            }

            // TODO(Phase 6+): Rebuilding details resets TextField focus and Foldout state.
            // Later phases should preserve detail UI state or update bound fields incrementally.
            detailPanel.Unbind();
            detailPanel.Clear();

            var sentencesProperty = GetSentencesProperty();
            if (sentencesProperty == null || originalIndex < 0 || originalIndex >= sentencesProperty.arraySize)
            {
                detailPanel.Add(new HelpBox("Select a sentence to edit details.", HelpBoxMessageType.Info));
                return;
            }

            var sentenceProperty = sentencesProperty.GetArrayElementAtIndex(originalIndex);
            var title = new Label($"Sentence #{originalIndex}")
            {
                name = "DialogueSentenceDetailTitle"
            };
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            detailPanel.Add(title);

            AddRelativeProperty(detailPanel, sentenceProperty, "SentenceId", "Sentence Id");
            AddSpeakerEditor(detailPanel, sentenceProperty);
            AddTextEditor(detailPanel, sentenceProperty);
            AddVoiceEditor(detailPanel, sentenceProperty);
            AddRelativeProperty(detailPanel, sentenceProperty, "NarrativeCategory", "Narrative Category");
            AddConditionCards(detailPanel, sentenceProperty.FindPropertyRelative("Conditions"), "Sentence Conditions", "进入本句前需要满足的条件。不满足时本句不会被播放。");
            AddActionCards(detailPanel, sentenceProperty.FindPropertyRelative("EnterActions"), "Enter Actions", "进入本句并开始播放时执行。不要把离开本句后的行为填在这里。");
            AddActionCards(detailPanel, sentenceProperty.FindPropertyRelative("ExitActions"), "Exit Actions", "本句完整推进离开时执行。Choice 被点击后会先执行 Choice Actions，再执行这里。");
            AddChoiceCards(detailPanel, sentenceProperty.FindPropertyRelative("Choices"));

            detailPanel.Bind(context.SerializedObject);
        }

        private void AddRelativeProperty(VisualElement parent, SerializedProperty owner, string propertyName, string label)
        {
            var property = owner.FindPropertyRelative(propertyName);
            if (property != null)
            {
                parent.Add(new PropertyField(property, label));
            }
        }

        private void AddSpeakerCatalogSelector(VisualElement parent)
        {
            var settings = NiumaGalEditorSettings.instance;
            var explicitCatalog = settings.GetExplicitSpeakerCatalog(context.Asset);
            var resolvedCatalog = settings.ResolveSpeakerCatalog(context.Asset);

            var field = new ObjectField("Speaker Catalog (Editor)")
            {
                name = "DialogueSpeakerCatalogField",
                objectType = typeof(DialogueSpeakerCatalog),
                allowSceneObjects = false,
                value = explicitCatalog,
                tooltip = "可选：给当前 DialogueAsset 单独指定编辑器说话人清单。留空时使用 Project Settings / Niuma / Gal Editor 中的默认清单。"
            };
            field.RegisterValueChangedCallback(evt =>
            {
                DialogueEditorAudioPreview.Stop();
                settings.SetSpeakerCatalogForAsset(context.Asset, evt.newValue as DialogueSpeakerCatalog);
                RebuildIndex();
            });
            parent.Add(field);

            if (explicitCatalog == null && resolvedCatalog != null)
            {
                parent.Add(new HelpBox($"当前使用默认 Speaker Catalog：{resolvedCatalog.name}", HelpBoxMessageType.Info));
            }
            else if (resolvedCatalog == null)
            {
                parent.Add(new HelpBox("未配置 Speaker Catalog。Speaker 将以普通字符串编辑；可在 Project Settings / Niuma / Gal Editor 配置默认清单。", HelpBoxMessageType.Info));
            }
        }

        private void AddSpeakerEditor(VisualElement parent, SerializedProperty sentenceProperty)
        {
            var speakerProperty = sentenceProperty.FindPropertyRelative("Speaker");
            if (speakerProperty == null)
            {
                return;
            }

            var catalog = NiumaGalEditorSettings.instance.ResolveSpeakerCatalog(context.Asset);
            if (catalog == null || catalog.Speakers == null || catalog.Speakers.Length == 0)
            {
                parent.Add(new PropertyField(speakerProperty, "Speaker"));
                parent.Add(new HelpBox("未配置可用 Speaker Catalog，当前使用字符串输入。", HelpBoxMessageType.Info));
                return;
            }

            var choices = BuildSpeakerChoices(catalog, speakerProperty.stringValue);
            var currentIndex = Mathf.Max(0, choices.IndexOf(speakerProperty.stringValue ?? string.Empty));
            string FormatSpeaker(string key) => FormatSpeakerChoice(catalog, key);
            // TODO(Phase 6+): PopupField is manually synced with SerializedProperty. Undo/Redo
            // updates after the detail panel rebuilds; later phases can bind it through a wrapper.
            var popup = new PopupField<string>("Speaker", choices, currentIndex, FormatSpeaker, FormatSpeaker)
            {
                name = "DialogueSpeakerPopup",
                tooltip = "选择说话人。实际写入 DialogueSentence.Speaker 字符串，不会新增运行时字段。"
            };
            popup.RegisterValueChangedCallback(evt =>
            {
                speakerProperty.stringValue = evt.newValue ?? string.Empty;
                context.SerializedObject.ApplyModifiedProperties();
                context.SerializedObject.UpdateIfRequiredOrScript();
                RebuildIndex();
            });
            parent.Add(popup);

            var speaker = FindSpeaker(catalog, speakerProperty.stringValue);
            if (speaker != null)
            {
                AddSpeakerPreview(parent, speaker);
            }
            else if (!string.IsNullOrWhiteSpace(speakerProperty.stringValue))
            {
                parent.Add(new HelpBox($"Speaker '{speakerProperty.stringValue}' 不在当前 Catalog 中。", HelpBoxMessageType.Warning));
            }
        }

        private void AddTextEditor(VisualElement parent, SerializedProperty sentenceProperty)
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
            textField.RegisterCallback<AttachToPanelEvent>(_ => ApplyMultilineTextInputStyle(textField));

            var statsLabel = new Label
            {
                name = "DialogueSentenceTextStats"
            };
            statsLabel.style.marginBottom = 6f;
            UpdateTextStats(statsLabel, textField.value);

            textField.RegisterValueChangedCallback(evt =>
            {
                textProperty.stringValue = evt.newValue ?? string.Empty;
                context.SerializedObject.ApplyModifiedProperties();
                UpdateTextStats(statsLabel, evt.newValue);
                // TODO(Phase 6+): Keep the left list summary in sync while typing without
                // calling RebuildIndex() on every character.
            });

            parent.Add(textField);
            parent.Add(statsLabel);
        }

        private void AddVoiceEditor(VisualElement parent, SerializedProperty sentenceProperty)
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
                RebuildIndex();
            });

            UpdateButtons();
            row.Add(clipField);
            row.Add(playButton);
            row.Add(stopButton);
            parent.Add(row);

            if (!DialogueEditorAudioPreview.IsSupported)
            {
                parent.Add(new HelpBox("当前 Unity 版本无法通过 AudioUtil 试听 VoiceClip。", HelpBoxMessageType.Info));
            }
        }

        private void AddConditionCards(VisualElement parent, SerializedProperty conditionsProperty, string title, string timingDescription)
        {
            AddArrayCards(parent, conditionsProperty, title, timingDescription, "Condition", BuildConditionCardTitle, AddConditionCardBody, InitializeConditionElement);
        }

        private void AddActionCards(VisualElement parent, SerializedProperty actionsProperty, string title, string timingDescription)
        {
            AddArrayCards(parent, actionsProperty, title, timingDescription, "Action", BuildActionCardTitle, AddActionCardBody, InitializeActionElement);
        }

        private void AddChoiceCards(VisualElement parent, SerializedProperty choicesProperty)
        {
            if (choicesProperty == null || !choicesProperty.isArray)
            {
                return;
            }

            var foldout = BuildArrayFoldout($"Choices ({choicesProperty.arraySize})", "文字播放完成后显示给玩家的选项。ChoiceId 必填，运行时点击依赖它。", choicesProperty.arraySize);
            AddArrayCommandRow(foldout, choicesProperty, "Choice", InitializeChoiceElement);
            foldout.Add(new HelpBox("Choice 点击顺序：先执行 Choice Actions，再执行当前句子的 Exit Actions，最后应用 Behavior。", HelpBoxMessageType.Info));

            for (var i = 0; i < choicesProperty.arraySize; i++)
            {
                var choice = choicesProperty.GetArrayElementAtIndex(i);
                var card = BuildCardFoldout(BuildChoiceCardTitle(choice, i));
                AddCardDeleteButton(card, choicesProperty, i, "Choice");
                AddRelativeProperty(card, choice, "ChoiceId", "Choice Id");
                AddRelativeProperty(card, choice, "DisplayText", "Display Text");
                AddRelativeProperty(card, choice, "Behavior", "Behavior");
                AddRelativeProperty(card, choice, "NextSentenceId", "Next Sentence Id");
                AddRelativeProperty(card, choice, "HideWhenUnavailable", "Hide When Unavailable");
                AddRelativeProperty(card, choice, "DisabledText", "Disabled Text");
                AddConditionCards(card, choice.FindPropertyRelative("Conditions"), "Choice Conditions", "该选项显示或可点击前需要满足的条件。");
                AddActionCards(card, choice.FindPropertyRelative("Actions"), "Choice Actions", "玩家点击该选项后立即执行，然后再执行当前句子的 Exit Actions。");
                foldout.Add(card);
            }

            parent.Add(foldout);
        }

        private void AddArrayCards(
            VisualElement parent,
            SerializedProperty arrayProperty,
            string title,
            string timingDescription,
            string itemName,
            Func<SerializedProperty, int, string> titleBuilder,
            Action<VisualElement, SerializedProperty> bodyBuilder,
            Action<SerializedProperty> initializer)
        {
            if (arrayProperty == null || !arrayProperty.isArray)
            {
                return;
            }

            var foldout = BuildArrayFoldout($"{title} ({arrayProperty.arraySize})", timingDescription, arrayProperty.arraySize);
            AddArrayCommandRow(foldout, arrayProperty, itemName, initializer);
            for (var i = 0; i < arrayProperty.arraySize; i++)
            {
                var element = arrayProperty.GetArrayElementAtIndex(i);
                var card = BuildCardFoldout(titleBuilder(element, i));
                AddCardDeleteButton(card, arrayProperty, i, itemName);
                bodyBuilder(card, element);
                foldout.Add(card);
            }

            parent.Add(foldout);
        }

        private void AddArrayCommandRow(VisualElement parent, SerializedProperty arrayProperty, string itemName, Action<SerializedProperty> initializer)
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

        private void AddCardDeleteButton(VisualElement parent, SerializedProperty arrayProperty, int index, string itemName)
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

        private static Foldout BuildArrayFoldout(string title, string timingDescription, int count)
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

        private static Foldout BuildCardFoldout(string title)
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

        private void AddConditionCardBody(VisualElement parent, SerializedProperty condition)
        {
            AddRelativeProperty(parent, condition, "ConditionId", "Condition Id");
            AddRelativeProperty(parent, condition, "Type", "Condition Type");
            AddRelativeProperty(parent, condition, "TargetId", "Target Id");
            AddRelativeProperty(parent, condition, "Operator", "Operator");
            AddRelativeProperty(parent, condition, "StringValue", "String Value");
            AddRelativeProperty(parent, condition, "IntValue", "Int Value");
            AddRelativeProperty(parent, condition, "FloatValue", "Float Value");
            AddRelativeProperty(parent, condition, "BoolValue", "Bool Value");
            AddRelativeProperty(parent, condition, "CustomData", "Custom Data");
        }

        private void AddActionCardBody(VisualElement parent, SerializedProperty action)
        {
            AddRelativeProperty(parent, action, "ActionId", "Action Id");
            AddRelativeProperty(parent, action, "Type", "Action Type");
            parent.Add(new HelpBox(GetActionTargetHint(action), HelpBoxMessageType.Info));
            AddRelativeProperty(parent, action, "TargetId", "Target Id");
            AddRelativeProperty(parent, action, "StringValue", "String Value");
            AddRelativeProperty(parent, action, "IntValue", "Int Value");
            AddRelativeProperty(parent, action, "FloatValue", "Float Value");
            AddRelativeProperty(parent, action, "BoolValue", "Bool Value");
            AddRelativeProperty(parent, action, "CustomData", "Custom Data");
        }

        private void AddArrayElement(SerializedProperty arrayProperty, Action<SerializedProperty> initializer)
        {
            var propertyPath = arrayProperty?.propertyPath;
            if (string.IsNullOrWhiteSpace(propertyPath))
            {
                return;
            }

            context.SerializedObject.UpdateIfRequiredOrScript();
            var refreshedArray = context.SerializedObject.FindProperty(propertyPath);
            if (refreshedArray == null || !refreshedArray.isArray)
            {
                return;
            }

            var index = refreshedArray.arraySize;
            refreshedArray.InsertArrayElementAtIndex(index);
            initializer?.Invoke(refreshedArray.GetArrayElementAtIndex(index));
            context.SerializedObject.ApplyModifiedProperties();
            context.SerializedObject.UpdateIfRequiredOrScript();
            RebuildIndex();
        }

        private void DeleteArrayElementAndRefresh(SerializedProperty arrayProperty, int index)
        {
            var propertyPath = arrayProperty?.propertyPath;
            if (string.IsNullOrWhiteSpace(propertyPath))
            {
                return;
            }

            context.SerializedObject.UpdateIfRequiredOrScript();
            var refreshedArray = context.SerializedObject.FindProperty(propertyPath);
            if (refreshedArray == null || !refreshedArray.isArray || index < 0 || index >= refreshedArray.arraySize)
            {
                return;
            }

            DeleteArrayElement(refreshedArray, index);
            context.SerializedObject.ApplyModifiedProperties();
            context.SerializedObject.UpdateIfRequiredOrScript();
            RebuildIndex();
        }

        private void AddSentence()
        {
            var sentencesProperty = GetSentencesProperty();
            if (sentencesProperty == null)
            {
                return;
            }

            context.SerializedObject.UpdateIfRequiredOrScript();
            var index = sentencesProperty.arraySize;
            var sentenceId = MakeUniqueSentenceId(sentencesProperty);
            sentencesProperty.InsertArrayElementAtIndex(index);
            InitializeSentence(sentencesProperty.GetArrayElementAtIndex(index), sentenceId);
            ApplyAndSelect(index);
        }

        private void DuplicateSelectedSentence()
        {
            var index = session.SelectedSentenceIndex;
            var sentencesProperty = GetSentencesProperty();
            if (sentencesProperty == null || index < 0 || index >= sentencesProperty.arraySize)
            {
                return;
            }

            context.SerializedObject.UpdateIfRequiredOrScript();
            var source = sentencesProperty.GetArrayElementAtIndex(index);
            var copyId = MakeUniqueCopyId(sentencesProperty, GetString(source, "SentenceId"));
            sentencesProperty.InsertArrayElementAtIndex(index + 1);
            var duplicated = sentencesProperty.GetArrayElementAtIndex(index + 1);
            var idProperty = duplicated.FindPropertyRelative("SentenceId");
            if (idProperty != null)
            {
                idProperty.stringValue = copyId;
            }
            SetString(duplicated, "EditorGuid", Guid.NewGuid().ToString("N"));

            ApplyAndSelect(index + 1);
        }

        private void DeleteSelectedSentence()
        {
            var index = session.SelectedSentenceIndex;
            var sentencesProperty = GetSentencesProperty();
            if (sentencesProperty == null || index < 0 || index >= sentencesProperty.arraySize)
            {
                return;
            }

            context.SerializedObject.UpdateIfRequiredOrScript();
            DeleteArrayElement(sentencesProperty, index);
            var nextIndex = Mathf.Clamp(index, 0, sentencesProperty.arraySize - 1);
            ApplyAndSelect(sentencesProperty.arraySize > 0 ? nextIndex : -1);
        }

        private void MoveSelectedSentence(int offset)
        {
            var index = session.SelectedSentenceIndex;
            var sentencesProperty = GetSentencesProperty();
            if (sentencesProperty == null || index < 0 || index >= sentencesProperty.arraySize)
            {
                return;
            }

            var target = index + offset;
            if (target < 0 || target >= sentencesProperty.arraySize)
            {
                return;
            }

            context.SerializedObject.UpdateIfRequiredOrScript();
            sentencesProperty.MoveArrayElement(index, target);
            ApplyAndSelect(target);
        }

        private void ApplyAndSelect(int selectedIndex)
        {
            context.SerializedObject.ApplyModifiedProperties();
            context.SerializedObject.UpdateIfRequiredOrScript();
            session.SetSelectedSentence(context.Asset, selectedIndex);
            RebuildIndex();
        }

        private void InitializeSentence(SerializedProperty sentenceProperty, string sentenceId)
        {
            SetString(sentenceProperty, "SentenceId", sentenceId);
            SetString(sentenceProperty, "Speaker", string.Empty);
            SetString(sentenceProperty, "Text", string.Empty);
            SetObject(sentenceProperty, "VoiceClip", null);
            SetEnumIndex(sentenceProperty, "NarrativeCategory", 0);
            SetString(sentenceProperty, "EditorGuid", Guid.NewGuid().ToString("N"));
            ClearArray(sentenceProperty, "Conditions");
            ClearArray(sentenceProperty, "EnterActions");
            ClearArray(sentenceProperty, "ExitActions");
            ClearArray(sentenceProperty, "Choices");
        }

        private static void InitializeConditionElement(SerializedProperty condition)
        {
            SetString(condition, "ConditionId", string.Empty);
            SetEnumIndex(condition, "Type", 0);
            SetString(condition, "TargetId", string.Empty);
            SetString(condition, "Operator", string.Empty);
            SetString(condition, "StringValue", string.Empty);
            SetInt(condition, "IntValue", 0);
            SetFloat(condition, "FloatValue", 0f);
            SetBool(condition, "BoolValue", false);
            ResetCustomData(condition);
        }

        private static void InitializeActionElement(SerializedProperty action)
        {
            SetString(action, "ActionId", string.Empty);
            SetEnumIndex(action, "Type", 0);
            SetString(action, "TargetId", string.Empty);
            SetString(action, "StringValue", string.Empty);
            SetInt(action, "IntValue", 0);
            SetFloat(action, "FloatValue", 0f);
            SetBool(action, "BoolValue", false);
            ResetCustomData(action);
        }

        private static void InitializeChoiceElement(SerializedProperty choice)
        {
            SetString(choice, "ChoiceId", string.Empty);
            SetString(choice, "DisplayText", string.Empty);
            SetEnumIndex(choice, "Behavior", 0);
            SetString(choice, "NextSentenceId", string.Empty);
            SetBool(choice, "HideWhenUnavailable", false);
            SetString(choice, "DisabledText", string.Empty);
            ClearArray(choice, "Conditions");
            ClearArray(choice, "Actions");
        }

        private void UpdateCommandState()
        {
            var sentencesProperty = GetSentencesProperty();
            var selectedIndex = session.SelectedSentenceIndex;
            var hasSentences = sentencesProperty != null && sentencesProperty.arraySize > 0;
            var hasSelection = hasSentences && selectedIndex >= 0 && selectedIndex < sentencesProperty.arraySize;

            if (duplicateButton != null)
            {
                duplicateButton.SetEnabled(hasSelection);
            }

            if (deleteButton != null)
            {
                deleteButton.SetEnabled(hasSelection);
            }

            if (moveUpButton != null)
            {
                moveUpButton.SetEnabled(hasSelection && selectedIndex > 0);
            }

            if (moveDownButton != null)
            {
                moveDownButton.SetEnabled(hasSelection && sentencesProperty != null && selectedIndex < sentencesProperty.arraySize - 1);
            }
        }

        private void UpdateSummary(SerializedProperty sentencesProperty)
        {
            if (summaryLabel == null)
            {
                return;
            }

            var total = sentencesProperty != null && sentencesProperty.isArray ? sentencesProperty.arraySize : 0;
            summaryLabel.text = $"Sentence Count: {total} | Filtered: {sentenceItems.Count}";
        }

        private void UpdateSelectionInfo(SentenceListItem item)
        {
            if (selectionLabel == null)
            {
                return;
            }

            selectionLabel.text = item == null
                ? "Selected Sentence: None"
                : $"Selected Sentence: #{item.OriginalIndex} {item.SentenceId}";
        }

        private SerializedProperty GetSentencesProperty()
        {
            return context.SerializedObject?.FindProperty("Sentences");
        }

        private static SentenceListItem BuildItem(SerializedProperty sentenceProperty, int index)
        {
            return new SentenceListItem
            {
                OriginalIndex = index,
                SentenceId = GetString(sentenceProperty, "SentenceId"),
                Speaker = GetString(sentenceProperty, "Speaker"),
                Text = GetString(sentenceProperty, "Text"),
                HasVoice = sentenceProperty.FindPropertyRelative("VoiceClip")?.objectReferenceValue != null,
                ChoiceCount = sentenceProperty.FindPropertyRelative("Choices")?.arraySize ?? 0
            };
        }

        private static string GetString(SerializedProperty parent, string relativeName)
        {
            return parent?.FindPropertyRelative(relativeName)?.stringValue ?? string.Empty;
        }

        private static void SetString(SerializedProperty parent, string relativeName, string value)
        {
            var property = parent.FindPropertyRelative(relativeName);
            if (property != null)
            {
                property.stringValue = value ?? string.Empty;
            }
        }

        private static void SetObject(SerializedProperty parent, string relativeName, UnityEngine.Object value)
        {
            var property = parent.FindPropertyRelative(relativeName);
            if (property != null)
            {
                property.objectReferenceValue = value;
            }
        }

        private static void SetEnumIndex(SerializedProperty parent, string relativeName, int value)
        {
            var property = parent.FindPropertyRelative(relativeName);
            if (property != null && property.propertyType == SerializedPropertyType.Enum)
            {
                property.enumValueIndex = Mathf.Clamp(value, 0, Math.Max(0, property.enumDisplayNames.Length - 1));
            }
        }

        private static void SetInt(SerializedProperty parent, string relativeName, int value)
        {
            var property = parent.FindPropertyRelative(relativeName);
            if (property != null)
            {
                property.intValue = value;
            }
        }

        private static void SetFloat(SerializedProperty parent, string relativeName, float value)
        {
            var property = parent.FindPropertyRelative(relativeName);
            if (property != null)
            {
                property.floatValue = value;
            }
        }

        private static void SetBool(SerializedProperty parent, string relativeName, bool value)
        {
            var property = parent.FindPropertyRelative(relativeName);
            if (property != null)
            {
                property.boolValue = value;
            }
        }

        private static void ClearArray(SerializedProperty parent, string relativeName)
        {
            var property = parent.FindPropertyRelative(relativeName);
            if (property != null && property.isArray)
            {
                property.ClearArray();
            }
        }

        private static void ResetCustomData(SerializedProperty parent)
        {
            var property = parent?.FindPropertyRelative("CustomData");
            if (property == null)
            {
                return;
            }

            if (property.propertyType == SerializedPropertyType.String)
            {
                property.stringValue = string.Empty;
                return;
            }

            if (property.isArray)
            {
                property.ClearArray();
            }
        }

        private static void DeleteArrayElement(SerializedProperty arrayProperty, int index)
        {
            if (arrayProperty == null || index < 0 || index >= arrayProperty.arraySize)
            {
                return;
            }

            var oldSize = arrayProperty.arraySize;
            arrayProperty.DeleteArrayElementAtIndex(index);
            if (arrayProperty.arraySize == oldSize && index >= 0 && index < arrayProperty.arraySize)
            {
                arrayProperty.DeleteArrayElementAtIndex(index);
            }
        }

        private static bool MatchesSearch(SentenceListItem item, string search)
        {
            if (string.IsNullOrWhiteSpace(search))
            {
                return true;
            }

            if (item == null)
            {
                return false;
            }

            var comparison = StringComparison.OrdinalIgnoreCase;
            return Contains(item.SentenceId, search, comparison)
                || Contains(item.Speaker, search, comparison)
                || Contains(item.Text, search, comparison);
        }

        private static bool Contains(string value, string search, StringComparison comparison)
        {
            return !string.IsNullOrEmpty(value) && value.IndexOf(search, comparison) >= 0;
        }

        private static string BuildSentenceSummary(SentenceListItem item)
        {
            if (item == null)
            {
                return "<missing sentence>";
            }

            var id = string.IsNullOrWhiteSpace(item.SentenceId) ? "<empty id>" : item.SentenceId;
            var speaker = string.IsNullOrWhiteSpace(item.Speaker) ? "Narration" : item.Speaker;
            var summary = BuildTextSummary(item.Text, 40);
            var voice = item.HasVoice ? " | Voice" : string.Empty;
            var choices = item.ChoiceCount > 0 ? $" | Choices:{item.ChoiceCount}" : string.Empty;
            return $"{id} | {speaker} | {summary}{voice}{choices}";
        }

        private static string BuildTextSummary(string text, int maxLength)
        {
            var plain = StripRichTextTags(text);
            plain = plain.Replace("\r\n", "\n").Replace('\r', '\n');
            var firstLine = plain.Split('\n')[0].Trim();
            if (string.IsNullOrEmpty(firstLine))
            {
                return "<empty text>";
            }

            return firstLine.Length <= maxLength
                ? firstLine
                : firstLine.Substring(0, maxLength) + "...";
        }

        private static List<string> BuildSpeakerChoices(DialogueSpeakerCatalog catalog, string currentSpeaker)
        {
            var result = new List<string> { string.Empty };
            if (catalog?.Speakers != null)
            {
                for (var i = 0; i < catalog.Speakers.Length; i++)
                {
                    var speaker = catalog.Speakers[i];
                    if (speaker == null || string.IsNullOrWhiteSpace(speaker.SpeakerKey))
                    {
                        continue;
                    }

                    if (!result.Contains(speaker.SpeakerKey))
                    {
                        result.Add(speaker.SpeakerKey);
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(currentSpeaker) && !result.Contains(currentSpeaker))
            {
                result.Add(currentSpeaker);
            }

            return result;
        }

        private static string FormatSpeakerChoice(DialogueSpeakerCatalog catalog, string speakerKey)
        {
            if (string.IsNullOrWhiteSpace(speakerKey))
            {
                return "(Narration / Empty)";
            }

            var speaker = FindSpeaker(catalog, speakerKey);
            return speaker == null || string.IsNullOrWhiteSpace(speaker.DisplayName)
                ? speakerKey
                : $"{speakerKey} - {speaker.DisplayName}";
        }

        private static DialogueSpeakerEditorData FindSpeaker(DialogueSpeakerCatalog catalog, string speakerKey)
        {
            if (catalog?.Speakers == null || string.IsNullOrWhiteSpace(speakerKey))
            {
                return null;
            }

            for (var i = 0; i < catalog.Speakers.Length; i++)
            {
                var speaker = catalog.Speakers[i];
                if (speaker != null && string.Equals(speaker.SpeakerKey, speakerKey, StringComparison.Ordinal))
                {
                    return speaker;
                }
            }

            return null;
        }

        private static void AddSpeakerPreview(VisualElement parent, DialogueSpeakerEditorData speaker)
        {
            var preview = new VisualElement
            {
                name = "DialogueSpeakerPreview"
            };
            preview.style.flexDirection = FlexDirection.Row;
            preview.style.alignItems = Align.Center;
            preview.style.marginBottom = 6f;

            if (speaker.Portrait != null)
            {
                // TODO(Phase 6+): If Portrait comes from a SpriteAtlas, use sprite-aware rendering
                // instead of speaker.Portrait.texture to avoid showing the whole atlas.
                var portrait = new Image
                {
                    image = speaker.Portrait.texture,
                    scaleMode = ScaleMode.ScaleToFit
                };
                portrait.style.width = 42f;
                portrait.style.height = 42f;
                portrait.style.marginRight = 6f;
                preview.Add(portrait);
            }

            var swatch = new VisualElement
            {
                name = "DialogueSpeakerThemeColor"
            };
            swatch.style.width = 18f;
            swatch.style.height = 18f;
            swatch.style.marginRight = 6f;
            swatch.style.backgroundColor = speaker.ThemeColor;
            preview.Add(swatch);

            var displayName = string.IsNullOrWhiteSpace(speaker.DisplayName) ? speaker.SpeakerKey : speaker.DisplayName;
            preview.Add(new Label($"Speaker Preview: {displayName}"));

            if (speaker.PreviewVoice != null)
            {
                var previewButton = new Button(() =>
                {
                    if (!DialogueEditorAudioPreview.Play(speaker.PreviewVoice, out var error))
                    {
                        Debug.LogWarning($"[NiumaGalEditor] Speaker Voice 试听失败：{error}");
                    }
                })
                {
                    text = "Preview Voice"
                };
                previewButton.SetEnabled(DialogueEditorAudioPreview.IsSupported);
                previewButton.style.marginLeft = 8f;
                preview.Add(previewButton);
            }

            parent.Add(preview);
        }

        private static void ApplyMultilineTextInputStyle(TextField textField)
        {
            var input = textField?.Q<TextElement>();
            if (input != null)
            {
                input.style.whiteSpace = WhiteSpace.Normal;
            }
        }

        private static void UpdateTextStats(Label label, string text)
        {
            if (label == null)
            {
                return;
            }

            var visibleCount = StripRichTextTags(text).Length;
            var readSeconds = Mathf.CeilToInt(visibleCount / 5f);
            var level = visibleCount <= 60 ? "Normal" : visibleCount <= 120 ? "Long" : "Too Long";
            label.text = $"Characters: {visibleCount} | Read: {readSeconds}s | Length: {level}";
        }

        private static string StripRichTextTags(string text)
        {
            return RichTextTagRegex.Replace(text ?? string.Empty, string.Empty);
        }

        private static string BuildConditionCardTitle(SerializedProperty condition, int index)
        {
            var type = GetEnumDisplayName(condition, "Type");
            var id = GetString(condition, "ConditionId");
            var target = GetString(condition, "TargetId");
            return $"{index + 1}. {type} | {Fallback(id, "<no id>")} | Target:{Fallback(target, "<empty>")}";
        }

        private static string BuildActionCardTitle(SerializedProperty action, int index)
        {
            var type = GetEnumDisplayName(action, "Type");
            var id = GetString(action, "ActionId");
            var target = GetString(action, "TargetId");
            return $"{index + 1}. {type} | {Fallback(id, "<no id>")} | Target:{Fallback(target, "<empty>")}";
        }

        private static string BuildChoiceCardTitle(SerializedProperty choice, int index)
        {
            var id = GetString(choice, "ChoiceId");
            var text = GetString(choice, "DisplayText");
            var behavior = GetEnumDisplayName(choice, "Behavior");
            return $"{index + 1}. {Fallback(id, "<empty choice id>")} | {behavior} | {BuildTextSummary(text, 24)}";
        }

        private static string GetActionTargetHint(SerializedProperty action)
        {
            var type = GetEnumName(action, "Type");
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

        private static string GetEnumDisplayName(SerializedProperty parent, string relativeName)
        {
            var property = parent?.FindPropertyRelative(relativeName);
            if (property == null || property.propertyType != SerializedPropertyType.Enum)
            {
                return "None";
            }

            var index = property.enumValueIndex;
            return index >= 0 && index < property.enumDisplayNames.Length
                ? property.enumDisplayNames[index]
                : "None";
        }

        private static string GetEnumName(SerializedProperty parent, string relativeName)
        {
            var property = parent?.FindPropertyRelative(relativeName);
            if (property == null || property.propertyType != SerializedPropertyType.Enum)
            {
                return "None";
            }

            var index = property.enumValueIndex;
            return index >= 0 && index < property.enumNames.Length
                ? property.enumNames[index]
                : "None";
        }

        private static string Fallback(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private static string MakeUniqueSentenceId(SerializedProperty sentencesProperty)
        {
            var nextIndex = 1;
            string candidate;
            do
            {
                candidate = $"sentence_{nextIndex:000}";
                nextIndex++;
            }
            while (ContainsSentenceId(sentencesProperty, candidate));

            return candidate;
        }

        private static string MakeUniqueCopyId(SerializedProperty sentencesProperty, string sourceId)
        {
            var baseId = string.IsNullOrWhiteSpace(sourceId) ? "sentence_copy" : sourceId.Trim() + "_copy";
            var candidate = baseId;
            var suffix = 2;
            while (ContainsSentenceId(sentencesProperty, candidate))
            {
                candidate = $"{baseId}_{suffix}";
                suffix++;
            }

            return candidate;
        }

        private static bool ContainsSentenceId(SerializedProperty sentencesProperty, string sentenceId)
        {
            if (sentencesProperty == null || string.IsNullOrWhiteSpace(sentenceId))
            {
                return false;
            }

            for (var i = 0; i < sentencesProperty.arraySize; i++)
            {
                var element = sentencesProperty.GetArrayElementAtIndex(i);
                if (string.Equals(GetString(element, "SentenceId"), sentenceId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
