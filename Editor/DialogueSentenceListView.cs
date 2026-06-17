using System;
using System.Collections.Generic;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace NiumaGal.Editor
{
    public sealed class DialogueSentenceListView
    {
        private readonly List<DialogueSentenceListItem> items;
        private readonly Func<DialogueSentenceListItem, string> formatItem;
        private readonly Action<DialogueSentenceListItem> onSelected;
        private readonly Action onAdd;
        private readonly Action onDuplicate;
        private readonly Action onDelete;
        private readonly Action onMoveUp;
        private readonly Action onMoveDown;

        private ListView listView;
        private Label selectionLabel;
        private Button duplicateButton;
        private Button deleteButton;
        private Button moveUpButton;
        private Button moveDownButton;

        public DialogueSentenceListView(
            List<DialogueSentenceListItem> items,
            Func<DialogueSentenceListItem, string> formatItem,
            Action<DialogueSentenceListItem> onSelected,
            Action onAdd,
            Action onDuplicate,
            Action onDelete,
            Action onMoveUp,
            Action onMoveDown)
        {
            this.items = items ?? throw new ArgumentNullException(nameof(items));
            this.formatItem = formatItem ?? throw new ArgumentNullException(nameof(formatItem));
            this.onSelected = onSelected ?? throw new ArgumentNullException(nameof(onSelected));
            this.onAdd = onAdd ?? throw new ArgumentNullException(nameof(onAdd));
            this.onDuplicate = onDuplicate ?? throw new ArgumentNullException(nameof(onDuplicate));
            this.onDelete = onDelete ?? throw new ArgumentNullException(nameof(onDelete));
            this.onMoveUp = onMoveUp ?? throw new ArgumentNullException(nameof(onMoveUp));
            this.onMoveDown = onMoveDown ?? throw new ArgumentNullException(nameof(onMoveDown));
        }

        public VisualElement Build()
        {
            var foldout = new Foldout
            {
                name = "DialogueSentenceListFoldout",
                text = "句子列表",
                value = true
            };
            foldout.style.width = 420f;
            foldout.style.minWidth = 320f;
            foldout.style.marginRight = 8f;
            foldout.style.flexShrink = 0f;

            var buttonRow = new Toolbar
            {
                name = "DialogueSentenceCommandToolbar"
            };
            buttonRow.Add(new ToolbarButton(onAdd) { text = "新增" });
            duplicateButton = new ToolbarButton(onDuplicate) { text = "复制" };
            deleteButton = new ToolbarButton(onDelete) { text = "删除" };
            moveUpButton = new ToolbarButton(onMoveUp) { text = "上移" };
            moveDownButton = new ToolbarButton(onMoveDown) { text = "下移" };
            buttonRow.Add(duplicateButton);
            buttonRow.Add(deleteButton);
            buttonRow.Add(moveUpButton);
            buttonRow.Add(moveDownButton);
            foldout.Add(buttonRow);

            listView = new ListView
            {
                name = "DialogueSentenceList",
                itemsSource = items,
                fixedItemHeight = 30f,
                selectionType = SelectionType.Single,
                makeItem = MakeItem,
                bindItem = BindItem
            };
            listView.style.flexGrow = 1f;
            listView.selectionChanged += HandleSelectionChanged;
            foldout.Add(listView);

            selectionLabel = new Label
            {
                name = "DialogueSentenceSelectionInfo"
            };
            selectionLabel.style.marginTop = 6f;
            foldout.Add(selectionLabel);

            return foldout;
        }

        public void Rebuild()
        {
            listView?.Rebuild();
        }

        public void ClearSelection()
        {
            listView?.ClearSelection();
            UpdateSelectionInfo(null);
        }

        public void SetSelectionWithoutNotify(int filteredIndex)
        {
            if (listView == null || filteredIndex < 0 || filteredIndex >= items.Count)
            {
                ClearSelection();
                return;
            }

            listView.SetSelectionWithoutNotify(new[] { filteredIndex });
            UpdateSelectionInfo(items[filteredIndex]);
        }

        public void UpdateCommandState(bool hasSelection, bool canMoveUp, bool canMoveDown)
        {
            duplicateButton?.SetEnabled(hasSelection);
            deleteButton?.SetEnabled(hasSelection);
            moveUpButton?.SetEnabled(canMoveUp);
            moveDownButton?.SetEnabled(canMoveDown);
        }

        public void UpdateSelectionInfo(DialogueSentenceListItem item)
        {
            if (selectionLabel == null)
            {
                return;
            }

            selectionLabel.text = item == null
                ? "当前选中：无"
                : $"当前选中：#{item.OriginalIndex} {item.SentenceId}";
        }

        private static VisualElement MakeItem()
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

        private void BindItem(VisualElement element, int index)
        {
            var item = index >= 0 && index < items.Count ? items[index] : null;

            var indexLabel = element.Q<Label>("SentenceIndex");
            if (indexLabel != null)
            {
                indexLabel.text = item != null ? $"#{item.OriginalIndex}" : "#-";
            }

            var contentLabel = element.Q<Label>("SentenceSummary");
            if (contentLabel != null)
            {
                contentLabel.text = formatItem(item);
            }
        }

        private void HandleSelectionChanged(IEnumerable<object> selectedItems)
        {
            DialogueSentenceListItem selected = null;
            foreach (var item in selectedItems)
            {
                selected = item as DialogueSentenceListItem;
                break;
            }

            UpdateSelectionInfo(selected);
            onSelected(selected);
        }
    }
}
