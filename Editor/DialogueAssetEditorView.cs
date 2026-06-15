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
    public sealed class DialogueAssetEditorView
    {
        private sealed class SentenceListItem
        {
            public int OriginalIndex;
            public string SentenceId;
            public string Speaker;
            public string Text;
        }

        private readonly DialogueAssetEditorContext context;
        private readonly DialogueAssetEditorSession session;
        private readonly List<SentenceListItem> sentenceItems = new List<SentenceListItem>();

        private VisualElement root;
        private TextField searchField;
        private ListView sentenceListView;
        private Label selectionLabel;
        private Label summaryLabel;
        private VisualElement detailPanel;
        private Button addButton;
        private Button duplicateButton;
        private Button deleteButton;
        private Button moveUpButton;
        private Button moveDownButton;

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

            session.Load(context.Asset);
            BuildToolbar(root);

            if (context.Asset == null || context.SerializedObject == null)
            {
                root.Add(new HelpBox("No DialogueAsset selected.", HelpBoxMessageType.Info));
                return root;
            }

            context.SerializedObject.UpdateIfRequiredOrScript();

            BuildAssetInfo(root);
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
            // Phase 2 rebuilds the sentence list snapshot and selected details. Later phases extend
            // this into validation caches, search summary caches and graph caches.
            // TODO(Phase 3+): Structural operations currently trigger ListView.Rebuild(). This is fine
            // for short dialogues, but large assets should use cached indexes and incremental refreshes.
            RefreshSentenceItems();
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

            var validateButton = new ToolbarButton(() => DialogueEditorLog.PhasePlaceholder("Validate"))
            {
                text = "Validate"
            };

            var focusStartButton = new ToolbarButton(() => DialogueEditorLog.PhasePlaceholder("Focus Start"))
            {
                text = "Focus Start"
            };

            var graphButton = new ToolbarButton(() => DialogueEditorLog.PhasePlaceholder("Open Graph Preview"))
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

            summaryLabel = new Label
            {
                name = "DialogueAssetSummary"
            };
            summaryLabel.style.marginTop = 4f;
            container.Add(summaryLabel);

            container.Bind(context.SerializedObject);
            parent.Add(container);
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

            // TODO(Phase 2): Rebuilding details resets TextField focus and Foldout state.
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
            AddRelativeProperty(detailPanel, sentenceProperty, "Speaker", "Speaker");
            AddRelativeProperty(detailPanel, sentenceProperty, "Text", "Text");
            AddRelativeProperty(detailPanel, sentenceProperty, "VoiceClip", "Voice Clip");
            AddRelativeProperty(detailPanel, sentenceProperty, "Conditions", "Conditions");
            AddRelativeProperty(detailPanel, sentenceProperty, "EnterActions", "Enter Actions");
            AddRelativeProperty(detailPanel, sentenceProperty, "ExitActions", "Exit Actions");
            AddRelativeProperty(detailPanel, sentenceProperty, "Choices", "Choices");

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
            ClearArray(sentenceProperty, "Conditions");
            ClearArray(sentenceProperty, "EnterActions");
            ClearArray(sentenceProperty, "ExitActions");
            ClearArray(sentenceProperty, "Choices");
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
                Text = GetString(sentenceProperty, "Text")
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

        private static void ClearArray(SerializedProperty parent, string relativeName)
        {
            var property = parent.FindPropertyRelative(relativeName);
            if (property != null && property.isArray)
            {
                property.ClearArray();
            }
        }

        private static void DeleteArrayElement(SerializedProperty arrayProperty, int index)
        {
            arrayProperty.DeleteArrayElementAtIndex(index);
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
            return $"{id} | {speaker} | {summary}";
        }

        private static string BuildTextSummary(string text, int maxLength)
        {
            var plain = Regex.Replace(text ?? string.Empty, @"</?[a-zA-Z][^>]*>", string.Empty);
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
