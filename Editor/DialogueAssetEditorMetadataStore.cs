using System;
using System.Collections.Generic;
using System.IO;
using NiumaGal.Dialogue.Data;
using UnityEditor;
using UnityEngine;

namespace NiumaGal.Editor
{
    public readonly struct DialogueGraphNodePositionChange
    {
        public DialogueGraphNodePositionChange(string editorGuid, string sentenceId, Vector2 position)
        {
            EditorGuid = editorGuid ?? string.Empty;
            SentenceId = sentenceId ?? string.Empty;
            Position = position;
        }

        public string EditorGuid { get; }
        public string SentenceId { get; }
        public Vector2 Position { get; }
    }

    public static class DialogueAssetEditorMetadataStore
    {
        private const string MetadataSuffix = "_EditorMeta.asset";
        private const string MetadataFolder = "Editor/Metadata";

        public static bool EnsureSentenceEditorGuids(DialogueAsset asset)
        {
            if (asset == null)
            {
                return false;
            }

            if (!AssetDatabase.IsOpenForEdit(asset, StatusQueryOptions.UseCachedIfPossible))
            {
                return false;
            }

            var serialized = new SerializedObject(asset);
            try
            {
                serialized.UpdateIfRequiredOrScript();

                var sentences = serialized.FindProperty("Sentences");
                if (sentences == null || !sentences.isArray)
                {
                    return false;
                }

                var changed = false;
                var usedGuids = new HashSet<string>(StringComparer.Ordinal);
                for (var i = 0; i < sentences.arraySize; i++)
                {
                    var sentence = sentences.GetArrayElementAtIndex(i);
                    var editorGuid = sentence?.FindPropertyRelative("EditorGuid");
                    if (editorGuid == null)
                    {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(editorGuid.stringValue) || usedGuids.Contains(editorGuid.stringValue))
                    {
                        editorGuid.stringValue = CreateEditorGuid(usedGuids);
                        changed = true;
                    }

                    usedGuids.Add(editorGuid.stringValue);
                }

                if (changed)
                {
                    serialized.ApplyModifiedProperties();
                }

                return changed;
            }
            finally
            {
                serialized.Dispose();
            }
        }

        public static string GetMetadataPath(DialogueAsset asset)
        {
            var assetPath = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return string.Empty;
            }

            var moduleRoot = FindNiumaGalModuleRoot(assetPath);
            var fileName = Path.GetFileNameWithoutExtension(assetPath);
            if (string.IsNullOrWhiteSpace(moduleRoot) || string.IsNullOrWhiteSpace(fileName))
            {
                return string.Empty;
            }

            return $"{moduleRoot}/{MetadataFolder}/{fileName}{MetadataSuffix}";
        }

        public static DialogueAssetEditorMetadata Load(DialogueAsset asset)
        {
            var path = GetMetadataPath(asset);
            return string.IsNullOrWhiteSpace(path)
                ? null
                : AssetDatabase.LoadAssetAtPath<DialogueAssetEditorMetadata>(path);
        }

        public static DialogueAssetEditorMetadata GetOrCreate(DialogueAsset asset)
        {
            var path = GetMetadataPath(asset);
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            var metadata = AssetDatabase.LoadAssetAtPath<DialogueAssetEditorMetadata>(path);
            if (metadata != null)
            {
                SyncAssetGuid(asset, metadata);
                return metadata;
            }

            metadata = ScriptableObject.CreateInstance<DialogueAssetEditorMetadata>();
            metadata.name = Path.GetFileNameWithoutExtension(path);
            metadata.DialogueAssetGuid = NiumaGalEditorSettings.GetAssetGuid(asset);
            metadata.Nodes = Array.Empty<DialogueGraphNodeMetadata>();
            metadata.ViewState = new DialogueGraphViewState();

            EnsureFolderExists(Path.GetDirectoryName(path));
            AssetDatabase.CreateAsset(metadata, path);
            AssetDatabase.SaveAssetIfDirty(metadata);
            return metadata;
        }

        public static DialogueGraphNodeMetadata[] GetOrphanNodes(DialogueAsset asset, DialogueAssetEditorMetadata metadata)
        {
            if (asset == null || metadata?.Nodes == null || metadata.Nodes.Length == 0)
            {
                return Array.Empty<DialogueGraphNodeMetadata>();
            }

            var activeGuids = BuildActiveGuidSet(asset);
            var result = new List<DialogueGraphNodeMetadata>();
            for (var i = 0; i < metadata.Nodes.Length; i++)
            {
                var node = metadata.Nodes[i];
                if (node == null || string.IsNullOrWhiteSpace(node.EditorGuid))
                {
                    continue;
                }

                if (!activeGuids.Contains(node.EditorGuid))
                {
                    result.Add(node);
                }
            }

            return result.ToArray();
        }

        public static int CleanOrphanNodes(DialogueAsset asset)
        {
            var metadata = Load(asset);
            if (asset == null || metadata?.Nodes == null || metadata.Nodes.Length == 0)
            {
                return 0;
            }

            var activeGuids = BuildActiveGuidSet(asset);
            var kept = new List<DialogueGraphNodeMetadata>(metadata.Nodes.Length);
            var removed = 0;
            for (var i = 0; i < metadata.Nodes.Length; i++)
            {
                var node = metadata.Nodes[i];
                if (node == null || string.IsNullOrWhiteSpace(node.EditorGuid))
                {
                    removed++;
                    continue;
                }

                if (activeGuids.Contains(node.EditorGuid))
                {
                    kept.Add(node);
                    continue;
                }

                removed++;
            }

            if (removed <= 0)
            {
                return 0;
            }

            metadata.Nodes = kept.ToArray();
            SaveMetadata(metadata);
            return removed;
        }

        public static bool SetNodePosition(DialogueAsset asset, string editorGuid, string sentenceId, Vector2 position)
        {
            if (asset == null || string.IsNullOrWhiteSpace(editorGuid))
            {
                return false;
            }

            var metadata = GetOrCreate(asset);
            if (metadata == null)
            {
                return false;
            }

            var node = GetOrCreateNode(metadata, editorGuid);
            var changed = !NearlyEqual(node.Position, position) ||
                          !string.Equals(node.LastKnownSentenceId, sentenceId ?? string.Empty, StringComparison.Ordinal);
            if (!changed)
            {
                return false;
            }

            node.Position = position;
            node.LastKnownSentenceId = sentenceId ?? string.Empty;
            SaveMetadata(metadata);
            return true;
        }

        public static bool SetViewState(DialogueAsset asset, Vector3 position, Vector3 scale)
        {
            if (asset == null)
            {
                return false;
            }

            var metadata = GetOrCreate(asset);
            if (metadata == null)
            {
                return false;
            }

            metadata.ViewState ??= new DialogueGraphViewState();
            scale.z = 1f;

            var changed = !NearlyEqual(metadata.ViewState.Position, position) ||
                          !NearlyEqual(metadata.ViewState.Scale, scale);
            if (!changed)
            {
                return false;
            }

            metadata.ViewState.Position = position;
            metadata.ViewState.Scale = scale;
            SaveMetadata(metadata);
            return true;
        }

        public static bool ApplyGraphChanges(
            DialogueAsset asset,
            IList<DialogueGraphNodePositionChange> nodePositions,
            bool hasViewState,
            Vector3 viewPosition,
            Vector3 viewScale)
        {
            if (asset == null)
            {
                return false;
            }

            var hasNodeChanges = nodePositions != null && nodePositions.Count > 0;
            if (!hasNodeChanges && !hasViewState)
            {
                return false;
            }

            var metadata = GetOrCreate(asset);
            if (metadata == null)
            {
                return false;
            }

            var changed = false;
            if (hasNodeChanges)
            {
                for (var i = 0; i < nodePositions.Count; i++)
                {
                    var change = nodePositions[i];
                    if (string.IsNullOrWhiteSpace(change.EditorGuid))
                    {
                        continue;
                    }

                    var node = GetOrCreateNode(metadata, change.EditorGuid);
                    if (!NearlyEqual(node.Position, change.Position) ||
                        !string.Equals(node.LastKnownSentenceId, change.SentenceId, StringComparison.Ordinal))
                    {
                        node.Position = change.Position;
                        node.LastKnownSentenceId = change.SentenceId;
                        changed = true;
                    }
                }
            }

            if (hasViewState)
            {
                metadata.ViewState ??= new DialogueGraphViewState();
                viewScale.z = 1f;
                if (!NearlyEqual(metadata.ViewState.Position, viewPosition) ||
                    !NearlyEqual(metadata.ViewState.Scale, viewScale))
                {
                    metadata.ViewState.Position = viewPosition;
                    metadata.ViewState.Scale = viewScale;
                    changed = true;
                }
            }

            if (changed)
            {
                SaveMetadata(metadata);
            }

            return changed;
        }

        private static DialogueGraphNodeMetadata GetOrCreateNode(DialogueAssetEditorMetadata metadata, string editorGuid)
        {
            if (metadata.Nodes != null)
            {
                for (var i = 0; i < metadata.Nodes.Length; i++)
                {
                    var node = metadata.Nodes[i];
                    if (node != null && string.Equals(node.EditorGuid, editorGuid, StringComparison.Ordinal))
                    {
                        return node;
                    }
                }
            }

            var nodes = metadata.Nodes == null
                ? new List<DialogueGraphNodeMetadata>()
                : new List<DialogueGraphNodeMetadata>(metadata.Nodes);
            var created = new DialogueGraphNodeMetadata
            {
                EditorGuid = editorGuid,
                Position = Vector2.zero
            };
            nodes.Add(created);
            metadata.Nodes = nodes.ToArray();
            return created;
        }

        private static HashSet<string> BuildActiveGuidSet(DialogueAsset asset)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            var sentences = asset?.Sentences;
            if (sentences == null)
            {
                return result;
            }

            for (var i = 0; i < sentences.Count; i++)
            {
                var guid = sentences[i]?.EditorGuid;
                if (!string.IsNullOrWhiteSpace(guid))
                {
                    result.Add(guid);
                }
            }

            return result;
        }

        private static string FindNiumaGalModuleRoot(string assetPath)
        {
            var normalized = assetPath?.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return string.Empty;
            }

            const string marker = "/NiumaGal/";
            var markerIndex = normalized.IndexOf(marker, StringComparison.Ordinal);
            if (markerIndex >= 0)
            {
                return normalized.Substring(0, markerIndex + "/NiumaGal".Length);
            }

            if (normalized.EndsWith("/NiumaGal", StringComparison.Ordinal))
            {
                return normalized;
            }

            var segments = normalized.Split('/');
            for (var i = segments.Length - 1; i >= 0; i--)
            {
                if (string.Equals(segments[i], "NiumaGal", StringComparison.Ordinal))
                {
                    return string.Join("/", segments, 0, i + 1);
                }
            }

            return string.Empty;
        }

        private static void EnsureFolderExists(string folderPath)
        {
            var normalized = folderPath?.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(normalized) || AssetDatabase.IsValidFolder(normalized))
            {
                return;
            }

            var parts = normalized.Split('/');
            if (parts.Length == 0)
            {
                return;
            }

            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private static void SyncAssetGuid(DialogueAsset asset, DialogueAssetEditorMetadata metadata)
        {
            var guid = NiumaGalEditorSettings.GetAssetGuid(asset);
            if (metadata == null || string.Equals(metadata.DialogueAssetGuid, guid, StringComparison.Ordinal))
            {
                return;
            }

            metadata.DialogueAssetGuid = guid;
            SaveMetadata(metadata);
        }

        private static void SaveMetadata(DialogueAssetEditorMetadata metadata)
        {
            if (metadata == null)
            {
                return;
            }

            EditorUtility.SetDirty(metadata);
            AssetDatabase.SaveAssetIfDirty(metadata);
        }

        private static string CreateEditorGuid(HashSet<string> usedGuids)
        {
            string guid;
            do
            {
                guid = Guid.NewGuid().ToString("N");
            }
            while (usedGuids != null && usedGuids.Contains(guid));

            return guid;
        }

        private static bool NearlyEqual(Vector2 left, Vector2 right)
        {
            return Mathf.Abs(left.x - right.x) < 0.001f &&
                   Mathf.Abs(left.y - right.y) < 0.001f;
        }

        private static bool NearlyEqual(Vector3 left, Vector3 right)
        {
            return Mathf.Abs(left.x - right.x) < 0.001f &&
                   Mathf.Abs(left.y - right.y) < 0.001f &&
                   Mathf.Abs(left.z - right.z) < 0.001f;
        }
    }
}
