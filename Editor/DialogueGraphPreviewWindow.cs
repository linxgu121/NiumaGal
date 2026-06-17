using System.Collections.Generic;
using NiumaGal.Dialogue.Data;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NiumaGal.Editor
{
    public sealed class DialogueGraphPreviewWindow : EditorWindow
    {
        [SerializeField]
        private DialogueAsset asset;

        private DialogueAsset cachedAsset;
        private DialogueGraphSnapshot cachedSnapshot;

        public static void Open(DialogueAsset dialogueAsset)
        {
            var window = GetWindow<DialogueGraphPreviewWindow>();
            window.titleContent = new GUIContent("对话图预览");
            if (window.asset != dialogueAsset)
            {
                window.cachedAsset = null;
                window.cachedSnapshot = null;
            }

            window.asset = dialogueAsset;
            window.Show();
            window.Rebuild(true);
        }

        private void OnEnable()
        {
            Rebuild(false);
        }

        private void Rebuild(bool forceRebuild = false)
        {
            if (rootVisualElement == null)
            {
                return;
            }

            rootVisualElement.Clear();
            rootVisualElement.style.flexGrow = 1f;

            var toolbar = new Toolbar();
            toolbar.Add(new Label(asset == null ? "未选择 DialogueAsset" : asset.name));
            toolbar.Add(new ToolbarSpacer());
            toolbar.Add(new ToolbarButton(() => Rebuild(true)) { text = "刷新" });
            rootVisualElement.Add(toolbar);

            if (asset == null)
            {
                cachedAsset = null;
                cachedSnapshot = null;
                rootVisualElement.Add(new HelpBox("未选择 DialogueAsset。请从对话资产编辑器中打开预览。", HelpBoxMessageType.Info));
                return;
            }

            if (forceRebuild || cachedSnapshot == null || cachedAsset != asset)
            {
                cachedAsset = asset;
                cachedSnapshot = DialogueValidationService.BuildGraph(asset);
            }

            var graphView = new ReadOnlyDialogueGraphView(cachedSnapshot);
            graphView.StretchToParentSize();
            rootVisualElement.Add(graphView);
            graphView.FrameAll();
        }

        private sealed class ReadOnlyDialogueGraphView : GraphView
        {
            private const float NodeWidth = 230f;
            private const float NodeHeight = 120f;
            private const float ColumnWidth = 290f;
            private const float RowHeight = 170f;

            public ReadOnlyDialogueGraphView(DialogueGraphSnapshot snapshot)
            {
                style.flexGrow = 1f;
                Insert(0, new GridBackground());
                this.AddManipulator(new ContentZoomer());
                this.AddManipulator(new ContentDragger());
                this.AddManipulator(new SelectionDragger());
                this.AddManipulator(new RectangleSelector());
                Build(snapshot ?? DialogueGraphSnapshot.Empty);
            }

            public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
            {
                return new List<Port>();
            }

            private void Build(DialogueGraphSnapshot snapshot)
            {
                var outputPorts = new Dictionary<int, Port>();
                var inputPorts = new Dictionary<int, Port>();
                var nodes = snapshot.Nodes ?? System.Array.Empty<DialogueGraphNodeData>();
                var edges = snapshot.Edges ?? System.Array.Empty<DialogueGraphEdgeData>();

                for (var i = 0; i < nodes.Length; i++)
                {
                    var data = nodes[i];
                    if (data == null)
                    {
                        continue;
                    }

                    var node = CreateNode(data);
                    AddElement(node);
                    inputPorts[data.Index] = node.inputContainer.Q<Port>();
                    outputPorts[data.Index] = node.outputContainer.Q<Port>();
                }

                var endNode = CreateTerminalNode("结束", new Vector2(ColumnWidth * 2f, Mathf.Max(RowHeight, nodes.Length * 42f)));
                AddElement(endNode);
                var endInput = endNode.inputContainer.Q<Port>();

                var unknownNode = CreateTerminalNode("缺失 / 未知", new Vector2(ColumnWidth * 2f, Mathf.Max(RowHeight * 2f, nodes.Length * 42f + RowHeight)));
                AddElement(unknownNode);
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
                    AddElement(graphEdge);
                }
            }

            private static Node CreateNode(DialogueGraphNodeData data)
            {
                var node = new Node
                {
                    title = data.IsStart ? $"{data.Title}  [起始]" : data.Title
                };

                node.SetPosition(new Rect(ResolveNodePosition(data.Index), new Vector2(NodeWidth, NodeHeight)));
                node.capabilities &= ~Capabilities.Deletable;

                var input = node.InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
                input.portName = string.Empty;
                node.inputContainer.Add(input);

                var output = node.InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(bool));
                output.portName = string.Empty;
                node.outputContainer.Add(output);

                var summary = new Label(data.Summary);
                summary.style.whiteSpace = WhiteSpace.Normal;
                summary.style.marginTop = 4f;
                node.extensionContainer.Add(summary);

                if (!data.IsReachable)
                {
                    var warning = new Label("不可达");
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

                var input = node.InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
                input.portName = string.Empty;
                node.inputContainer.Add(input);
                node.RefreshExpandedState();
                node.RefreshPorts();
                return node;
            }

            private static Vector2 ResolveNodePosition(int index)
            {
                var column = index % 3;
                var row = index / 3;
                return new Vector2(column * ColumnWidth, row * RowHeight);
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
                var label = string.IsNullOrWhiteSpace(edge.Label) ? "连线" : edge.Label;
                if (edge.IsConditional)
                {
                    label += " | 有条件";
                }

                if (edge.IsUnknown)
                {
                    label += " | 目标缺失";
                }

                return label;
            }
        }
    }
}
