using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace NiumaGal.Editor
{
    [InitializeOnLoad]
    public static class DialogueEditorAudioPreview
    {
        private static readonly MethodInfo PlayMethod;
        private static readonly MethodInfo StopMethod;

        static DialogueEditorAudioPreview()
        {
            var audioUtilType = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
            if (audioUtilType != null)
            {
                const BindingFlags Flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                PlayMethod = audioUtilType.GetMethod("PlayPreviewClip", Flags, null, new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null)
                    ?? audioUtilType.GetMethod("PlayPreviewClip", Flags, null, new[] { typeof(AudioClip), typeof(int) }, null)
                    ?? audioUtilType.GetMethod("PlayPreviewClip", Flags, null, new[] { typeof(AudioClip) }, null)
                    ?? audioUtilType.GetMethod("PlayClip", Flags, null, new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null)
                    ?? audioUtilType.GetMethod("PlayClip", Flags, null, new[] { typeof(AudioClip) }, null);

                StopMethod = audioUtilType.GetMethod("StopAllPreviewClips", Flags)
                    ?? audioUtilType.GetMethod("StopAllClips", Flags);
            }

            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorApplication.playModeStateChanged += _ => Stop();
        }

        public static bool IsSupported => PlayMethod != null && StopMethod != null;

        public static bool Play(AudioClip clip, out string error)
        {
            if (clip == null)
            {
                error = "VoiceClip 为空，无法试听。";
                return false;
            }

            if (!IsSupported)
            {
                error = "当前 Unity 版本无法通过 AudioUtil 试听。";
                return false;
            }

            try
            {
                Stop();
                PlayMethod.Invoke(null, BuildPlayArguments(clip));
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.InnerException != null ? exception.InnerException.Message : exception.Message;
                return false;
            }
        }

        public static void Stop()
        {
            if (StopMethod == null)
            {
                return;
            }

            try
            {
                StopMethod.Invoke(null, null);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[NiumaGalEditor] 停止 Voice 试听失败：{exception.Message}");
            }
        }

        private static object[] BuildPlayArguments(AudioClip clip)
        {
            var parameters = PlayMethod.GetParameters();
            if (parameters.Length == 3)
            {
                return new object[] { clip, 0, false };
            }

            if (parameters.Length == 2)
            {
                return new object[] { clip, 0 };
            }

            return new object[] { clip };
        }
    }
}