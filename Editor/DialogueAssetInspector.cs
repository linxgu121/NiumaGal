using NiumaGal.Dialogue.Data;
using UnityEditor;
using UnityEngine.UIElements;

namespace NiumaGal.Editor
{
    [CustomEditor(typeof(DialogueAsset))]
    public sealed class DialogueAssetInspector : UnityEditor.Editor
    {
        private DialogueAssetEditorSession session;
        private VisualElement inspectorRoot;

        private void OnDisable()
        {
            inspectorRoot?.Clear();
            DialogueEditorAudioPreview.Stop();
        }

        private void OnDestroy()
        {
            inspectorRoot?.Clear();
            DialogueEditorAudioPreview.Stop();
        }

        public override VisualElement CreateInspectorGUI()
        {
            var asset = target as DialogueAsset;
            DialogueAssetEditorMetadataStore.EnsureSentenceEditorGuids(asset);
            session ??= new DialogueAssetEditorSession(DialogueAssetEditorHostKind.Inspector);

            var context = new DialogueAssetEditorContext
            {
                Asset = asset,
                SerializedObject = serializedObject,
                HostKind = DialogueAssetEditorHostKind.Inspector
            };

            var view = new DialogueAssetEditorView(context, session);
            inspectorRoot = view.Build();
            return inspectorRoot;
        }
    }
}
