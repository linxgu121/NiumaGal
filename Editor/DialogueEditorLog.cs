namespace NiumaGal.Editor
{
    internal static class DialogueEditorLog
    {
        public static void PhasePlaceholder(string actionName)
        {
            UnityEngine.Debug.Log($"[NiumaGalEditor] {actionName} is reserved for a later implementation phase.");
        }
    }
}
