using NiumaGal.Dialogue.Data;
using UnityEditor;
using UnityEngine.UIElements;

namespace NiumaGal.Editor
{
    [CustomEditor(typeof(DialogueAsset))]
    public sealed class DialogueAssetInspector : UnityEditor.Editor
    {
        private DialogueAssetEditorSession session;

        public override VisualElement CreateInspectorGUI()
        {
            var asset = target as DialogueAsset;
            session ??= new DialogueAssetEditorSession(DialogueAssetEditorHostKind.Inspector);

            var context = new DialogueAssetEditorContext
            {
                Asset = asset,
                SerializedObject = serializedObject,
                HostKind = DialogueAssetEditorHostKind.Inspector
            };

            var view = new DialogueAssetEditorView(context, session);
            return view.Build();
        }
    }
}
