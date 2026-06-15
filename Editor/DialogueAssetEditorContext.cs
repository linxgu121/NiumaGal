using System;
using NiumaGal.Dialogue.Data;
using UnityEditor;

namespace NiumaGal.Editor
{
    public enum DialogueAssetEditorHostKind
    {
        Inspector = 0,
        EditorWindow = 1,
        Wizard = 2
    }

    public sealed class DialogueAssetEditorContext
    {
        public DialogueAsset Asset;
        public SerializedObject SerializedObject;
        public DialogueAssetEditorHostKind HostKind;
        public Action<DialogueAsset> OnAssetSelected;
    }
}
