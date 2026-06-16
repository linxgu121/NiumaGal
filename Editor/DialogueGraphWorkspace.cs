using System;
using System.Collections.Generic;
using System.Linq;
using NiumaGal.Dialogue.Data;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NiumaGal.Editor
{
    public sealed class DialogueGraphWorkspace
    {
        private const float NodeWidth = 230f;
        private const float NodeHeight = 130f;
        private const float ColumnWidth = 320f;
        private const float RowHeight = 170f;
        private const double MetadataSaveDelaySeconds = 0.35d;

        private readonly DialogueAsset asset;
        private readonly Action<int> onSentenceSelected;
        private readonly Dictionary<int, DialogueGraphNode> nodeByIndex = new Dictionary<int, DialogueGraphNode>();
        private readonly Dictionary<string, DialogueGraphNodePositionChange> pendingNodePositions = new Dictionary<string, DialogueGraphNodePositionChange>(StringComparer.Ordinal);

        private GraphView graphView;
        private DialogueGraphSnapshot snapshot = DialogueGraphSnapshot.Empty;
        private int selectedIndex = -1;
        private bool isApplyingGraph;
        private bool delayedSaveQueued;
        private bool hasPendingViewState;
        private double nextMetadataSaveAt;
        private Vector3 pendingViewPosition;
        private Vector3 pendingViewScale;

        public DialogueGraphWorkspace(DialogueAsset asset, Action<int> onSentenceSelected)
        {
            this.asset = asset;
            this.onSentenceSelected = onSentenceSelected;
        }

        public VisualElement Build()
        {
            var container = new VisualElement
            {
                name = "DialogueGraphWorkspace"
            };
            container.style.flexGrow = 1f;
            container.style.minWidth = 420f;
            container.style.marginRight = 8f;
            container.style.borderLeftWidth = 1f;
            container.style.borderRightWidth = 1f;
            container.style.borderTopWidth = 1f;
            container.style.borderBottomWidth = 1f;
            container.style.borderLeftColor = new Color(0.3f, 0.3f, 0.3f);
            container.style.borderRightColor = new Color(0.3f, 0.3f, 0.3f);
            container.style.borderTopColor = new Color(0.3f, 0.3f, 0.3f);
            container.style.borderBottomColor = new Color(0.3f, 0.3f, 0.3f);
            container.RegisterCallback<DetachFromPanelEvent>(_ => FlushPendingMetadata());

            if (!IsGraphViewAvailable())
            {
                AddGraphUnavailableMessage(container, "当前环境不支持 GraphView，请使用左侧列表和右侧详情编辑。");
                return container;
            }

            try
            {
                graphView = new DialogueWorkspaceGraphView
                {
                    name = "DialogueGraphView"
                };
                graphView.style.flexGrow = 1f;
                graphView.Insert(0, new GridBackground());
                graphView.AddManipulator(new ContentZoomer());
                graphView.AddManipulator(new ContentDragger());
                graphView.AddManipulator(new SelectionDragger());
                graphView.AddManipulator(new RectangleSelector());
                graphView.graphViewChanged = OnGraphViewChanged;
                graphView.viewTransformChanged = OnViewTransformChanged;
                graphView.StretchToParentSize();
            }
            catch (Exception ex)
            {
                graphView = null;
                AddGraphUnavailableMessage(container, $"GraphView 初始化失败，请使用左侧列表和右侧详情编辑。原因：{ex.GetType().Name}: {ex.Message}");
                return container;
            }

            container.Add(graphView);
            return container;
        }

        private static bool IsGraphViewAvailable()
        {
            return Type.GetType("UnityEditor.Experimental.GraphView.GraphView, UnityEditor.GraphViewModule", false) != null
                || Type.GetType("UnityEditor.Experimental.GraphView.GraphView, UnityEditor", false) != null
                || typeof(GraphView) != null;
        }

        private static void AddGraphUnavailableMessage(VisualElement container, string message)
        {
            container.style.paddingLeft = 8f;
            container.style.paddingRight = 8f;
            container.style.paddingTop = 8f;
            container.style.paddingBottom = 8f;
            container.Add(new HelpBox(message, HelpBoxMessageType.Warning));
        }

        public void Refresh(DialogueGraphSnapshot graphSnapshot)
        {
            snapshot = graphSnapshot ?? DialogueGraphSnapshot.Empty;
            RebuildGraph();
        }

        public void SelectSentence(int index, bool frameSelection = true)
        {
            selectedIndex = index;
            if (graphView == null)
            {
                return;
            }

            graphView.ClearSelection();
            if (nodeByIndex.TryGetValue(index, out var node))
            {
                graphView.AddToSelection(node);
                if (frameSelection)
                {
                    graphView.FrameSelection();
                }
            }
        }

        public void Rearrange()
        {
            if (asset == null || snapshot == null)
            {
                return;
            }

            FlushPendingMetadata();
            var positions = CalculateAutoLayout(snapshot);
            var nodes = snapshot.Nodes ?? Array.Empty<DialogueGraphNodeData>();
            var changes = new List<DialogueGraphNodePositionChange>(nodes.Length);
            for (var i = 0; i < nodes.Length; i++)
            {
                var node = nodes[i];
                if (node == null || string.IsNullOrWhiteSpace(node.EditorGuid) || !positions.TryGetValue(node.Index, out var position))
                {
                    continue;
                }

                changes.Add(new DialogueGraphNodePositionChange(node.EditorGuid, node.SentenceId, position));
            }

            DialogueAssetEditorMetadataStore.ApplyGraphChanges(asset, changes, false, Vector3.zero, Vector3.one);
            RebuildGraph();
            SelectSentence(selectedIndex, false);
        }

        public int CleanMetadata()
        {
            FlushPendingMetadata();
            return DialogueAssetEditorMetadataStore.CleanOrphanNodes(asset);
        }

        private void RebuildGraph()
        {
            if (graphView == null)
            {
                return;
            }

            isApplyingGraph = true;
            try
            {
                FlushPendingMetadata();
                graphView.DeleteElements(graphView.graphElements.ToList());
                nodeByIndex.Clear();

                var outputPorts = new Dictionary<int, Port>();
                var inputPorts = new Dictionary<int, Port>();
                var nodes = snapshot.Nodes ?? Array.Empty<DialogueGraphNodeData>();
                var edges = snapshot.Edges ?? Array.Empty<DialogueGraphEdgeData>();
                var metadata = DialogueAssetEditorMetadataStore.Load(asset);
                var fallbackPositions = CalculateAutoLayout(snapshot);
                var maxNodePosition = Vector2.zero;

                for (var i = 0; i < nodes.Length; i++)
                {
                    var data = nodes[i];
                    if (data == null)
                    {
                        continue;
                    }

                    var position = ResolveNodePosition(metadata, data, fallbackPositions);
                    maxNodePosition.x = Mathf.Max(maxNodePosition.x, position.x);
                    maxNodePosition.y = Mathf.Max(maxNodePosition.y, position.y);
                    var node = CreateNode(data, position);
                    graphView.AddElement(node);
                    nodeByIndex[data.Index] = node;
                    inputPorts[data.Index] = node.InputPort;
                    outputPorts[data.Index] = node.OutputPort;
                }

                var terminalX = maxNodePosition.x + ColumnWidth;
                var terminalStartY = Mathf.Max(0f, maxNodePosition.y - RowHeight * 0.5f);
                var endNode = CreateTerminalNode("End", new Vector2(terminalX, terminalStartY));
                graphView.AddElement(endNode);
                var endInput = endNode.inputContainer.Q<Port>();

                var unknownNode = CreateTerminalNode("Missing / Unknown", new Vector2(terminalX, terminalStartY + RowHeight));
                graphView.AddElement(unknownNode);
                var unknownInput = unknownNode.inputContainer.Q<Port>();

                for (var i = 0; i < edges.Length; i++)
                {
                    var edge = edges[i];
                    if (edge == null || !outputPorts.TryGetValue(edge.FromIndex, out var output))
                    {
                        continue;
                    }

                    var input = ResolveTargetPort(edge, inputPorts, endInput, unknownInput);
                    if (input == null)
                    {
                        continue;
                    }

                    var graphEdge = output.ConnectTo(input);
                    graphEdge.tooltip = BuildEdgeTooltip(edge);
                    graphEdge.capabilities &= ~Capabilities.Deletable;
                    graphView.AddElement(graphEdge);
                }

                RestoreViewState(metadata);
            }
            finally
            {
                isApplyingGraph = false;
            }

            SelectSentence(selectedIndex, false);
        }

        private DialogueGraphNode CreateNode(DialogueGraphNodeData data, Vector2 position)
        {
            var node = new DialogueGraphNode(data)
            {
                title = data.IsStart ? $"{data.Title}  [Start]" : data.Title
            };
            node.SetPosition(new Rect(position, new Vector2(NodeWidth, NodeHeight)));
            node.capabilities &= ~Capabilities.Deletable;
            node.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button != 0 || evt.clickCount < 2)
                {
                    return;
                }

                onSentenceSelected?.Invoke(data.Index);
                evt.StopPropagation();
            });

            var color = NiumaGalEditorSettings.instance.ResolveNarrativeCategoryColor(data.NarrativeCategory);
            node.titleContainer.style.backgroundColor = color;

            var input = node.InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
            input.portName = string.Empty;
            node.inputContainer.Add(input);
            node.InputPort = input;

            var output = node.InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(bool));
            output.portName = string.Empty;
            node.outputContainer.Add(output);
            node.OutputPort = output;

            var summary = new Label(data.Summary)
            {
                name = "DialogueGraphNodeSummary"
            };
            summary.style.whiteSpace = WhiteSpace.Normal;
            summary.style.marginTop = 4f;
            node.extensionContainer.Add(summary);

            var category = new Label(data.NarrativeCategory.ToString())
            {
                name = "DialogueGraphNodeCategory"
            };
            category.style.fontSize = 10f;
            category.style.marginTop = 4f;
            node.extensionContainer.Add(category);

            if (!data.IsReachable)
            {
                var warning = new Label("Unreachable")
                {
                    name = "DialogueGraphNodeWarning"
                };
                warning.style.color = new Color(1f, 0.65f, 0.2f);
                warning.style.marginTop = 4f;
                node.extensionContainer.Add(warning);
            }

            node.RefreshExpandedState();
            node.RefreshPorts();
            return node;
        }

        private static Node CreateTerminalNode(string title, Vector2 position)
        {
            var node = new Node
            {
                title = title
            };
            node.SetPosition(new Rect(position, new Vector2(NodeWidth, 80f)));
            node.capabilities &= ~Capabilities.Deletable;
            node.capabilities &= ~Capabilities.Movable;

            var input = node.InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
            input.portName = string.Empty;
            node.inputContainer.Add(input);
            node.RefreshExpandedState();
            node.RefreshPorts();
            return node;
        }

        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            if (isApplyingGraph || asset == null || change.movedElements == null)
            {
                return change;
            }

            for (var i = 0; i < change.movedElements.Count; i++)
            {
                if (change.movedElements[i] is not DialogueGraphNode node || node.Data == null)
                {
                    continue;
                }

                var rect = node.GetPosition();
                QueueNodePosition(node.Data.EditorGuid, node.Data.SentenceId, rect.position);
            }

            return change;
        }

        private void OnViewTransformChanged(GraphView view)
        {
            if (isApplyingGraph || asset == null || view == null)
            {
                return;
            }

            QueueViewState(view.viewTransform.position, view.viewTransform.scale);
        }

        private void QueueNodePosition(string editorGuid, string sentenceId, Vector2 position)
        {
            if (string.IsNullOrWhiteSpace(editorGuid))
            {
                return;
            }

            pendingNodePositions[editorGuid] = new DialogueGraphNodePositionChange(editorGuid, sentenceId, position);
            ScheduleDelayedMetadataSave();
        }

        private void QueueViewState(Vector3 position, Vector3 scale)
        {
            pendingViewPosition = position;
            pendingViewScale = scale;
            hasPendingViewState = true;
            ScheduleDelayedMetadataSave();
        }

        private void ScheduleDelayedMetadataSave()
        {
            nextMetadataSaveAt = EditorApplication.timeSinceStartup + MetadataSaveDelaySeconds;
            if (delayedSaveQueued)
            {
                return;
            }

            delayedSaveQueued = true;
            EditorApplication.update += FlushPendingMetadataWhenReady;
        }

        private void FlushPendingMetadataWhenReady()
        {
            if (EditorApplication.timeSinceStartup < nextMetadataSaveAt)
            {
                return;
            }

            FlushPendingMetadata();
        }

        private void FlushPendingMetadata()
        {
            if (delayedSaveQueued)
            {
                EditorApplication.update -= FlushPendingMetadataWhenReady;
                delayedSaveQueued = false;
            }

            if (asset == null || (pendingNodePositions.Count == 0 && !hasPendingViewState))
            {
                pendingNodePositions.Clear();
                hasPendingViewState = false;
                return;
            }

            var nodeChanges = pendingNodePositions.Values.ToList();
            pendingNodePositions.Clear();
            var writeViewState = hasPendingViewState;
            var viewPosition = pendingViewPosition;
            var viewScale = pendingViewScale;
            hasPendingViewState = false;
            DialogueAssetEditorMetadataStore.ApplyGraphChanges(asset, nodeChanges, writeViewState, viewPosition, viewScale);
        }

        private void RestoreViewState(DialogueAssetEditorMetadata metadata)
        {
            var state = metadata?.ViewState;
            if (graphView == null || state == null || state.Scale == Vector3.zero)
            {
                return;
            }

            graphView.UpdateViewTransform(state.Position, state.Scale);
        }

        private static Vector2 ResolveNodePosition(DialogueAssetEditorMetadata metadata, DialogueGraphNodeData data, Dictionary<int, Vector2> fallbackPositions)
        {
            var nodes = metadata?.Nodes;
            if (nodes != null && !string.IsNullOrWhiteSpace(data.EditorGuid))
            {
                for (var i = 0; i < nodes.Length; i++)
                {
                    var node = nodes[i];
                    if (node != null && string.Equals(node.EditorGuid, data.EditorGuid, StringComparison.Ordinal))
                    {
                        return node.Position;
                    }
                }
            }

            return fallbackPositions != null && fallbackPositions.TryGetValue(data.Index, out var fallback)
                ? fallback
                : new Vector2(data.Index * ColumnWidth, 0f);
        }

        private static Dictionary<int, Vector2> CalculateAutoLayout(DialogueGraphSnapshot graphSnapshot)
        {
            var result = new Dictionary<int, Vector2>();
            var nodes = graphSnapshot?.Nodes ?? Array.Empty<DialogueGraphNodeData>();
            if (nodes.Length == 0)
            {
                return result;
            }

            var edges = graphSnapshot?.Edges ?? Array.Empty<DialogueGraphEdgeData>();
            var levels = new Dictionary<int, int>();
            var queue = new Queue<int>();
            var start = graphSnapshot != null && graphSnapshot.StartIndex >= 0 && graphSnapshot.StartIndex < nodes.Length
                ? graphSnapshot.StartIndex
                : 0;
            queue.Enqueue(start);
            levels[start] = 0;

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                var nextLevel = levels[current] + 1;
                for (var i = 0; i < edges.Length; i++)
                {
                    var edge = edges[i];
                    if (edge == null || edge.FromIndex != current || edge.IsEnd || edge.IsUnknown || edge.ToIndex < 0 || levels.ContainsKey(edge.ToIndex))
                    {
                        continue;
                    }

                    levels[edge.ToIndex] = nextLevel;
                    queue.Enqueue(edge.ToIndex);
                }
            }

            var maxReachableLevel = 0;
            foreach (var pair in levels)
            {
                maxReachableLevel = Math.Max(maxReachableLevel, pair.Value);
            }

            var unreachableLevel = maxReachableLevel + 2;
            var rowsPerLevel = new Dictionary<int, int>();
            // Keep same-level nodes ordered by original Sentences index to avoid layout jitter.
            foreach (var data in nodes.OrderBy(node => node?.Index ?? int.MaxValue))
            {
                var index = data?.Index ?? 0;
                var level = levels.TryGetValue(index, out var value)
                    ? value
                    : unreachableLevel;
                var row = rowsPerLevel.TryGetValue(level, out var currentRow) ? currentRow : 0;
                rowsPerLevel[level] = row + 1;
                result[index] = new Vector2(level * ColumnWidth, row * RowHeight);
            }

            return result;
        }

        private static Port ResolveTargetPort(DialogueGraphEdgeData edge, Dictionary<int, Port> inputPorts, Port endInput, Port unknownInput)
        {
            if (edge.IsEnd)
            {
                return endInput;
            }

            if (edge.IsUnknown || edge.ToIndex < 0 || !inputPorts.TryGetValue(edge.ToIndex, out var input))
            {
                return unknownInput;
            }

            return input;
        }

        private static string BuildEdgeTooltip(DialogueGraphEdgeData edge)
        {
            var label = string.IsNullOrWhiteSpace(edge.Label) ? "Edge" : edge.Label;
            if (edge.IsConditional)
            {
                label += " | Conditional";
            }

            if (edge.IsUnknown)
            {
                label += " | Missing Target";
            }

            return label;
        }

        private sealed class DialogueGraphNode : Node
        {
            public DialogueGraphNode(DialogueGraphNodeData data)
            {
                Data = data;
            }

            public DialogueGraphNodeData Data { get; }
            public Port InputPort { get; set; }
            public Port OutputPort { get; set; }
        }

        private sealed class DialogueWorkspaceGraphView : GraphView
        {
            public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
            {
                return new List<Port>();
            }
        }
    }
}
