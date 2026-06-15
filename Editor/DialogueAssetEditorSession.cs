using System;
using NiumaGal.Dialogue.Data;
using UnityEditor;

namespace NiumaGal.Editor
{
    public sealed class DialogueAssetEditorSession
    {
        private const string RootKey = "NiumaGal.DialogueEditor";

        private readonly DialogueAssetEditorHostKind hostKind;
        private readonly string windowSessionId;

        public int SelectedSentenceIndex { get; private set; } = -1;
        public string SearchText { get; private set; } = string.Empty;

        public DialogueAssetEditorSession(DialogueAssetEditorHostKind hostKind, string windowSessionId = null)
        {
            this.hostKind = hostKind;
            this.windowSessionId = string.IsNullOrWhiteSpace(windowSessionId)
                ? (hostKind == DialogueAssetEditorHostKind.EditorWindow ? Guid.NewGuid().ToString("N") : string.Empty)
                : windowSessionId;
        }

        public void Load(DialogueAsset asset)
        {
            if (asset == null)
            {
                SelectedSentenceIndex = -1;
                SearchText = string.Empty;
                return;
            }

            SelectedSentenceIndex = SessionState.GetInt(BuildKey(asset, "SelectedSentenceIndex"), -1);
            SearchText = SessionState.GetString(BuildKey(asset, "SearchText"), string.Empty);
        }

        public void SetSelectedSentence(DialogueAsset asset, int index)
        {
            if (asset == null)
            {
                SelectedSentenceIndex = index;
                return;
            }

            SelectedSentenceIndex = index;
            SessionState.SetInt(BuildKey(asset, "SelectedSentenceIndex"), index);
        }

        public void SetSearchText(DialogueAsset asset, string value)
        {
            SearchText = value ?? string.Empty;
            if (asset != null)
            {
                SessionState.SetString(BuildKey(asset, "SearchText"), SearchText);
            }
        }

        private string BuildKey(DialogueAsset asset, string field)
        {
            var assetKey = ResolveAssetKey(asset);
            if (hostKind == DialogueAssetEditorHostKind.EditorWindow)
            {
                return $"{RootKey}/Window/{windowSessionId}/{assetKey}/{field}";
            }

            return $"{RootKey}/{hostKind}/{assetKey}/{field}";
        }

        private static string ResolveAssetKey(DialogueAsset asset)
        {
            if (asset == null)
            {
                return "None";
            }

            var path = AssetDatabase.GetAssetPath(asset);
            if (!string.IsNullOrWhiteSpace(path))
            {
                var guid = AssetDatabase.AssetPathToGUID(path);
                if (!string.IsNullOrWhiteSpace(guid))
                {
                    return guid;
                }
            }

            return $"Instance_{asset.GetInstanceID()}";
        }
    }
}
