using System;
using NiumaGal.Dialogue.Data;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace NiumaGal.Editor
{
    public sealed class DialogueAssetEditorWindow : EditorWindow
    {
        [SerializeField]
        private DialogueAsset currentAsset;

        [SerializeField]
        private string windowSessionId;

        private SerializedObject currentSerializedObject;
        private DialogueAssetEditorSession session;
        private VisualElement viewHost;

        [MenuItem("Tools/Niuma/Gal/Dialogue Asset Editor")]
        public static void OpenWindow()
        {
            var window = GetWindow<DialogueAssetEditorWindow>();
            window.titleContent = new GUIContent("Dialogue Editor");
            window.Show();
        }

        private void OnEnable()
        {
            if (string.IsNullOrWhiteSpace(windowSessionId))
            {
                windowSessionId = Guid.NewGuid().ToString("N");
            }
        }

        private void OnDisable()
        {
            DialogueEditorAudioPreview.Stop();
            DisposeCurrentSerializedObject();
        }

        private void OnDestroy()
        {
            DialogueEditorAudioPreview.Stop();
            DisposeCurrentSerializedObject();
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.flexGrow = 1f;

            session ??= new DialogueAssetEditorSession(DialogueAssetEditorHostKind.EditorWindow, windowSessionId);

            viewHost = new VisualElement
            {
                name = "DialogueAssetEditorWindowViewHost"
            };
            viewHost.style.flexGrow = 1f;
            rootVisualElement.Add(viewHost);

            RebuildView();
        }

        private void HandleAssetSelected(DialogueAsset asset)
        {
            if (currentAsset == asset)
            {
                return;
            }

            DialogueEditorAudioPreview.Stop();
            currentAsset = asset;
            RebuildView();
        }

        private void RebuildView()
        {
            if (viewHost == null)
            {
                return;
            }

            viewHost.Clear();

            DisposeCurrentSerializedObject();
            if (currentAsset != null)
            {
                DialogueAssetEditorMetadataStore.EnsureSentenceEditorGuids(currentAsset);
                currentSerializedObject = new SerializedObject(currentAsset);
            }

            var context = new DialogueAssetEditorContext
            {
                Asset = currentAsset,
                SerializedObject = currentSerializedObject,
                HostKind = DialogueAssetEditorHostKind.EditorWindow,
                OnAssetSelected = HandleAssetSelected
            };

            var view = new DialogueAssetEditorView(context, session);
            viewHost.Add(view.Build());
        }

        private void DisposeCurrentSerializedObject()
        {
            if (currentSerializedObject == null)
            {
                return;
            }

            currentSerializedObject.Dispose();
            currentSerializedObject = null;
        }
    }
}
