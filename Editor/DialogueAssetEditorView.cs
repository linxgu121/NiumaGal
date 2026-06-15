using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NiumaGal.Dialogue.Data;
using UnityEditor;
using UnityEditor.UIElements;
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
            BuildSentenceList(root);
            BuildSelectionInfo(root);
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
                return;
            }

            context.SerializedObject.UpdateIfRequiredOrScript();
            // Phase 1 only rebuilds the sentence list snapshot. Later phases extend this
            // into SentenceId indexes, validation caches, search summary caches and graph caches.
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
            title.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
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

        private void BuildSentenceList(VisualElement parent)
        {
            var container = new VisualElement
            {
                name = "DialogueSentenceListPanel"
            };
            container.style.flexGrow = 1f;
            container.style.minHeight = 260f;

            var title = new Label("Sentences")
            {
                name = "DialogueSentenceListTitle"
            };
            title.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
            container.Add(title);

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
            parent.Add(container);
        }

        private void BuildSelectionInfo(VisualElement parent)
        {
            selectionLabel = new Label
            {
                name = "DialogueSentenceSelectionInfo"
            };
            selectionLabel.style.marginTop = 6f;
            parent.Add(selectionLabel);
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
        }

        private void RefreshSentenceItems()
        {
            sentenceItems.Clear();

            var sentencesProperty = context.SerializedObject.FindProperty("Sentences");
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
        }

        private void RestoreSelection()
        {
            if (sentenceListView == null || session.SelectedSentenceIndex < 0)
            {
                UpdateSelectionInfo(null);
                return;
            }

            var filteredIndex = sentenceItems.FindIndex(item => item.OriginalIndex == session.SelectedSentenceIndex);
            if (filteredIndex >= 0)
            {
                sentenceListView.SetSelectionWithoutNotify(new[] { filteredIndex });
                UpdateSelectionInfo(sentenceItems[filteredIndex]);
                return;
            }

            sentenceListView.ClearSelection();
            UpdateSelectionInfo(null);
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
    }
}
