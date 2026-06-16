using System;
using System.Collections.Generic;
using NiumaGal.Dialogue.Data;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NiumaGal.Editor
{
    // TODO(Phase 5): Replace Play Mode Simulator placeholder with DialogueEditorSimulator.
    public sealed class DialogueAssetEditorView
    {
        private readonly DialogueAssetEditorContext context;
        private readonly DialogueAssetEditorSession session;
        private readonly List<DialogueSentenceListItem> sentenceItems = new List<DialogueSentenceListItem>();

        private VisualElement root;
        private TextField searchField;
        private Label summaryLabel;
        private VisualElement validationPanel;
        private VisualElement detailPanel;
        private DialogueSentenceListView sentenceListView;
        private DialogueSentenceDetailView sentenceDetailView;
        private DialogueGraphWorkspace graphWorkspace;
        private DialogueSpeakerEditorHelper speakerEditorHelper;
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
            speakerEditorHelper = new DialogueSpeakerEditorHelper(context, RebuildIndex);

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
                sentenceListView?.UpdateSelectionInfo(null);
                UpdateSummary(null);
                RebuildDetails(-1);
                return;
            }

            context.SerializedObject.UpdateIfRequiredOrScript();
            // Phase 4 rebuilds the sentence list snapshot, validation report and embedded graph.
            // Later phases extend this into search summary caches and simulator state.
            // TODO(Phase 6+): Structural operations currently trigger ListView.Rebuild(). This is fine
            // for short dialogues, but large assets should use cached indexes and incremental refreshes.
            RefreshSentenceItems();
            RunValidation(false);
            graphWorkspace?.Refresh(validationReport?.Graph ?? DialogueValidationService.BuildGraph(context.Asset));
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

            var rearrangeButton = new ToolbarButton(RearrangeGraph)
            {
                text = "Rearrange"
            };

            var cleanMetadataButton = new ToolbarButton(CleanGraphMetadata)
            {
                text = "Clean Metadata"
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
            toolbar.Add(rearrangeButton);
            toolbar.Add(cleanMetadataButton);
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
            var assetActionCards = new DialogueActionCardBuilder(context.SerializedObject, RebuildIndex);
            assetActionCards.AddCards(container, context.SerializedObject.FindProperty("OnStartActions"), "On Start Actions", "对话开始时执行，适合锁输入、切镜头、播放音效等入口行为。");
            assetActionCards.AddCards(container, context.SerializedObject.FindProperty("OnCompleteActions"), "On Complete Actions", "对话正常完成时执行，适合推进任务、进入小游戏、切换剧情节点。");
            assetActionCards.AddCards(container, context.SerializedObject.FindProperty("OnAbortActions"), "On Abort Actions", "对话被强制关闭或中断时执行。通常可以留空，避免误写进度。");
            speakerEditorHelper?.AddSpeakerCatalogSelector(container);

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

        private void RearrangeGraph()
        {
            if (graphWorkspace == null)
            {
                Debug.LogWarning("[NiumaGalEditor] Graph Workspace is not ready. Rearrange skipped.");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                "Rearrange Dialogue Graph",
                "Rearrange 会覆盖当前手动摆放的节点位置，并写入该 DialogueAsset 的 Graph Metadata。是否继续？",
                "Rearrange",
                "Cancel"))
            {
                return;
            }

            graphWorkspace.Rearrange();
        }

        private void CleanGraphMetadata()
        {
            if (context.Asset == null)
            {
                Debug.LogWarning("[NiumaGalEditor] No DialogueAsset selected. Metadata cleanup skipped.");
                return;
            }

            var metadata = DialogueAssetEditorMetadataStore.Load(context.Asset);
            var orphanCount = DialogueAssetEditorMetadataStore.GetOrphanNodes(context.Asset, metadata).Length;
            if (orphanCount <= 0)
            {
                return;
            }

            if (!EditorUtility.DisplayDialog(
                "Clean Dialogue Graph Metadata",
                $"将删除 {orphanCount} 条已经找不到对应句子的 Graph Metadata。是否继续？",
                "Clean",
                "Cancel"))
            {
                return;
            }

            var removed = graphWorkspace?.CleanMetadata() ?? DialogueAssetEditorMetadataStore.CleanOrphanNodes(context.Asset);
            if (removed > 0)
            {
                Debug.Log($"[NiumaGalEditor] Clean Metadata removed {removed} orphan graph nodes.");
            }

            RebuildIndex();
        }

        private void BuildEditorBody(VisualElement parent)
        {
            var mainArea = new VisualElement
            {
                name = "DialogueEditorMainArea"
            };
            mainArea.style.flexDirection = FlexDirection.Column;
            mainArea.style.flexGrow = 1f;
            mainArea.style.minHeight = 520f;

            var body = new VisualElement
            {
                name = "DialogueEditorBody"
            };
            body.style.flexDirection = FlexDirection.Row;
            body.style.flexGrow = 1f;
            body.style.minHeight = 360f;

            sentenceListView = new DialogueSentenceListView(
                sentenceItems,
                BuildSentenceSummary,
                HandleSentenceSelected,
                AddSentence,
                DuplicateSelectedSentence,
                DeleteSelectedSentence,
                () => MoveSelectedSentence(-1),
                () => MoveSelectedSentence(1));
            body.Add(sentenceListView.Build());

            body.Add(BuildGraphWorkspace());

            sentenceDetailView = new DialogueSentenceDetailView(context, speakerEditorHelper, RebuildIndex);
            detailPanel = sentenceDetailView.Build();
            body.Add(detailPanel);

            mainArea.Add(body);
            mainArea.Add(BuildSimulatorPlaceholder());
            parent.Add(mainArea);
        }

        private VisualElement BuildGraphWorkspace()
        {
            try
            {
                graphWorkspace = new DialogueGraphWorkspace(context.Asset, HandleGraphSentenceSelected);
                return graphWorkspace.Build();
            }
            catch (Exception ex)
            {
                graphWorkspace = null;
                var container = new VisualElement
                {
                    name = "DialogueGraphWorkspaceFallback"
                };
                container.style.flexGrow = 1f;
                container.style.minWidth = 420f;
                container.style.marginRight = 8f;
                container.style.paddingLeft = 8f;
                container.style.paddingRight = 8f;
                container.style.paddingTop = 8f;
                container.style.paddingBottom = 8f;
                container.Add(new HelpBox(
                    $"GraphView 当前不可用，已降级为左侧列表 + 右侧详情编辑。原因：{ex.GetType().Name}: {ex.Message}",
                    HelpBoxMessageType.Warning));
                return container;
            }
        }

        private VisualElement BuildSimulatorPlaceholder()
        {
            var container = new VisualElement
            {
                name = "DialogueSimulatorPlaceholder"
            };
            container.style.marginTop = 8f;
            container.style.paddingLeft = 8f;
            container.style.paddingRight = 8f;
            container.style.paddingTop = 6f;
            container.style.paddingBottom = 6f;
            container.style.borderLeftWidth = 1f;
            container.style.borderRightWidth = 1f;
            container.style.borderTopWidth = 1f;
            container.style.borderBottomWidth = 1f;
            container.style.borderLeftColor = new Color(0.25f, 0.25f, 0.25f);
            container.style.borderRightColor = new Color(0.25f, 0.25f, 0.25f);
            container.style.borderTopColor = new Color(0.25f, 0.25f, 0.25f);
            container.style.borderBottomColor = new Color(0.25f, 0.25f, 0.25f);

            var title = new Label("Play Mode Simulator")
            {
                name = "DialogueSimulatorTitle"
            };
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            container.Add(title);
            container.Add(new HelpBox("阶段 3 只预留底部模拟器容器。Play / Pause / Stop / 打字机 / 选项跳转将在阶段 5 接入。", HelpBoxMessageType.Info));
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

        private void HandleSentenceSelected(DialogueSentenceListItem selected)
        {
            var originalIndex = selected?.OriginalIndex ?? -1;
            DialogueEditorAudioPreview.Stop();
            SelectSentence(originalIndex);
        }

        private void HandleGraphSentenceSelected(int originalIndex)
        {
            DialogueEditorAudioPreview.Stop();
            SelectSentence(originalIndex);
        }

        private void SelectSentence(int originalIndex)
        {
            session.SetSelectedSentence(context.Asset, originalIndex);
            UpdateListSelection(originalIndex);
            RebuildDetails(originalIndex);
            graphWorkspace?.SelectSentence(originalIndex);
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
                sentenceListView?.UpdateSelectionInfo(null);
                RebuildDetails(-1);
                graphWorkspace?.SelectSentence(-1);
                return;
            }

            var filteredIndex = sentenceItems.FindIndex(item => item.OriginalIndex == session.SelectedSentenceIndex);
            if (filteredIndex >= 0)
            {
                UpdateListSelection(session.SelectedSentenceIndex);
                RebuildDetails(session.SelectedSentenceIndex);
                graphWorkspace?.SelectSentence(session.SelectedSentenceIndex);
                return;
            }

            sentenceListView.ClearSelection();
            sentenceListView.UpdateSelectionInfo(null);
            RebuildDetails(-1);
            graphWorkspace?.SelectSentence(session.SelectedSentenceIndex);
        }

        private void UpdateListSelection(int originalIndex)
        {
            if (sentenceListView == null)
            {
                return;
            }

            var filteredIndex = sentenceItems.FindIndex(item => item.OriginalIndex == originalIndex);
            if (filteredIndex < 0)
            {
                sentenceListView.ClearSelection();
                sentenceListView.UpdateSelectionInfo(null);
                return;
            }

            sentenceListView.SetSelectionWithoutNotify(filteredIndex);
            sentenceListView.UpdateSelectionInfo(sentenceItems[filteredIndex]);
        }

        private void RebuildDetails(int originalIndex)
        {
            sentenceDetailView?.Rebuild(originalIndex);
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
            var copyId = MakeUniqueCopyId(sentencesProperty, DialogueSerializedPropertyUtility.GetString(source, "SentenceId"));
            sentencesProperty.InsertArrayElementAtIndex(index + 1);
            var duplicated = sentencesProperty.GetArrayElementAtIndex(index + 1);
            var idProperty = duplicated.FindPropertyRelative("SentenceId");
            if (idProperty != null)
            {
                idProperty.stringValue = copyId;
            }
            DialogueSerializedPropertyUtility.SetString(duplicated, "EditorGuid", Guid.NewGuid().ToString("N"));

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
            DialogueSerializedPropertyUtility.DeleteArrayElement(sentencesProperty, index);
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
            DialogueSerializedPropertyUtility.SetString(sentenceProperty, "SentenceId", sentenceId);
            DialogueSerializedPropertyUtility.SetString(sentenceProperty, "Speaker", string.Empty);
            DialogueSerializedPropertyUtility.SetString(sentenceProperty, "Text", string.Empty);
            DialogueSerializedPropertyUtility.SetObject(sentenceProperty, "VoiceClip", null);
            DialogueSerializedPropertyUtility.SetEnumIndex(sentenceProperty, "NarrativeCategory", 0);
            DialogueSerializedPropertyUtility.SetString(sentenceProperty, "EditorGuid", Guid.NewGuid().ToString("N"));
            DialogueSerializedPropertyUtility.ClearArray(sentenceProperty, "Conditions");
            DialogueSerializedPropertyUtility.ClearArray(sentenceProperty, "EnterActions");
            DialogueSerializedPropertyUtility.ClearArray(sentenceProperty, "ExitActions");
            DialogueSerializedPropertyUtility.ClearArray(sentenceProperty, "Choices");
        }

        private void UpdateCommandState()
        {
            var sentencesProperty = GetSentencesProperty();
            var selectedIndex = session.SelectedSentenceIndex;
            var hasSentences = sentencesProperty != null && sentencesProperty.arraySize > 0;
            var hasSelection = hasSentences && selectedIndex >= 0 && selectedIndex < sentencesProperty.arraySize;

            sentenceListView?.UpdateCommandState(
                hasSelection,
                hasSelection && selectedIndex > 0,
                hasSelection && sentencesProperty != null && selectedIndex < sentencesProperty.arraySize - 1);
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

        private SerializedProperty GetSentencesProperty()
        {
            return context.SerializedObject?.FindProperty("Sentences");
        }

        private static DialogueSentenceListItem BuildItem(SerializedProperty sentenceProperty, int index)
        {
            return new DialogueSentenceListItem
            {
                OriginalIndex = index,
                SentenceId = DialogueSerializedPropertyUtility.GetString(sentenceProperty, "SentenceId"),
                Speaker = DialogueSerializedPropertyUtility.GetString(sentenceProperty, "Speaker"),
                Text = DialogueSerializedPropertyUtility.GetString(sentenceProperty, "Text"),
                HasVoice = sentenceProperty.FindPropertyRelative("VoiceClip")?.objectReferenceValue != null,
                ChoiceCount = sentenceProperty.FindPropertyRelative("Choices")?.arraySize ?? 0
            };
        }

        private static bool MatchesSearch(DialogueSentenceListItem item, string search)
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

        private static string BuildSentenceSummary(DialogueSentenceListItem item)
        {
            if (item == null)
            {
                return "<missing sentence>";
            }

            var id = string.IsNullOrWhiteSpace(item.SentenceId) ? "<empty id>" : item.SentenceId;
            var speaker = string.IsNullOrWhiteSpace(item.Speaker) ? "Narration" : item.Speaker;
            var summary = DialogueEditorTextUtility.BuildTextSummary(item.Text, 40);
            var voice = item.HasVoice ? " | Voice" : string.Empty;
            var choices = item.ChoiceCount > 0 ? $" | Choices:{item.ChoiceCount}" : string.Empty;
            return $"{id} | {speaker} | {summary}{voice}{choices}";
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
                if (string.Equals(DialogueSerializedPropertyUtility.GetString(element, "SentenceId"), sentenceId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
