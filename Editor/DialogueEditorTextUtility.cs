using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UIElements;

namespace NiumaGal.Editor
{
    internal static class DialogueEditorTextUtility
    {
        private static readonly Regex RichTextTagRegex = new Regex(@"</?[a-zA-Z][^>]*>", RegexOptions.Compiled);

        public static string BuildTextSummary(string text, int maxLength)
        {
            var plain = StripRichTextTags(text);
            plain = plain.Replace("\r\n", "\n").Replace('\r', '\n');
            var firstLine = plain.Split('\n')[0].Trim();
            if (string.IsNullOrEmpty(firstLine))
            {
                return "<empty text>";
            }

            return firstLine.Length <= maxLength
                ? firstLine
                : firstLine.Substring(0, maxLength) + "...";
        }

        public static void ApplyMultilineTextInputStyle(TextField textField)
        {
            var input = textField?.Q<TextElement>();
            if (input != null)
            {
                input.style.whiteSpace = WhiteSpace.Normal;
            }
        }

        public static void UpdateTextStats(Label label, string text)
        {
            if (label == null)
            {
                return;
            }

            var visibleCount = StripRichTextTags(text).Length;
            var readSeconds = Mathf.CeilToInt(visibleCount / 5f);
            var level = visibleCount <= 60 ? "Normal" : visibleCount <= 120 ? "Long" : "Too Long";
            label.text = $"Characters: {visibleCount} | Read: {readSeconds}s | Length: {level}";
        }

        public static string StripRichTextTags(string text)
        {
            return RichTextTagRegex.Replace(text ?? string.Empty, string.Empty);
        }
    }
}
