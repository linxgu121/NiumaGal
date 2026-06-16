using System;
using UnityEngine;

namespace NiumaGal.Editor
{
    public sealed class DialogueAssetEditorMetadata : ScriptableObject
    {
        [Tooltip("对应 DialogueAsset 的 GUID。由编辑器自动维护，策划通常不需要手动修改。")]
        public string DialogueAssetGuid;

        [Tooltip("Graph 节点位置数据。以 DialogueSentence.EditorGuid 为主键。")]
        public DialogueGraphNodeMetadata[] Nodes = Array.Empty<DialogueGraphNodeMetadata>();

        [Tooltip("Graph 视图平移与缩放状态。")]
        public DialogueGraphViewState ViewState = new DialogueGraphViewState();
    }

    [Serializable]
    public sealed class DialogueGraphNodeMetadata
    {
        [Tooltip("对应 DialogueSentence.EditorGuid。SentenceId 改名后仍用它保持节点位置。")]
        public string EditorGuid;

        [Tooltip("最后一次记录到的 SentenceId，仅用于调试和孤儿项提示。")]
        public string LastKnownSentenceId;

        [Tooltip("Graph 节点位置。")]
        public Vector2 Position;
    }

    [Serializable]
    public sealed class DialogueGraphViewState
    {
        [Tooltip("GraphView viewTransform.position。")]
        public Vector3 Position;

        [Tooltip("GraphView viewTransform.scale。Scale.x / Scale.y 通常相同，Scale.z 保持 1。")]
        public Vector3 Scale = Vector3.one;
    }
}
