using System;
using System.Collections.Generic;
using NiumaGal.Dialogue.Data;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NiumaGal.Editor
{
    public sealed class DialogueSpeakerEditorHelper
    {
        private readonly DialogueAssetEditorContext context;
        private readonly Action onChanged;

        public DialogueSpeakerEditorHelper(DialogueAssetEditorContext context, Action onChanged)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            this.onChanged = onChanged;
        }

        public void AddSpeakerCatalogSelector(VisualElement parent)
        {
            var settings = NiumaGalEditorSettings.instance;
            var explicitCatalog = settings.GetExplicitSpeakerCatalog(context.Asset);
            var resolvedCatalog = settings.ResolveSpeakerCatalog(context.Asset);

            var field = new ObjectField("说话人清单（编辑器）")
            {
                name = "DialogueSpeakerCatalogField",
                objectType = typeof(DialogueSpeakerCatalog),
                allowSceneObjects = false,
                value = explicitCatalog,
                tooltip = "可选：给当前 DialogueAsset 单独指定编辑器说话人清单。留空时使用 Project Settings / Niuma / Gal Editor 中的默认清单。"
            };
            field.RegisterValueChangedCallback(evt =>
            {
                DialogueEditorAudioPreview.Stop();
                settings.SetSpeakerCatalogForAsset(context.Asset, evt.newValue as DialogueSpeakerCatalog);
                onChanged?.Invoke();
            });
            parent.Add(field);

            if (explicitCatalog == null && resolvedCatalog != null)
            {
                parent.Add(new HelpBox($"当前使用默认 Speaker Catalog：{resolvedCatalog.name}", HelpBoxMessageType.Info));
            }
            else if (resolvedCatalog == null)
            {
                parent.Add(new HelpBox("未配置 Speaker Catalog。Speaker 将以普通字符串编辑；可在 Project Settings / Niuma / Gal Editor 配置默认清单。", HelpBoxMessageType.Info));
            }
        }

        public void AddSpeakerEditor(VisualElement parent, SerializedProperty sentenceProperty)
        {
            var speakerProperty = sentenceProperty.FindPropertyRelative("Speaker");
            if (speakerProperty == null)
            {
                return;
            }

            var catalog = NiumaGalEditorSettings.instance.ResolveSpeakerCatalog(context.Asset);
            if (catalog == null || catalog.Speakers == null || catalog.Speakers.Length == 0)
            {
                parent.Add(new PropertyField(speakerProperty, "说话人"));
                parent.Add(new HelpBox("未配置可用 Speaker Catalog，当前使用字符串输入。", HelpBoxMessageType.Info));
                return;
            }

            var choices = BuildSpeakerChoices(catalog, speakerProperty.stringValue);
            var currentIndex = Mathf.Max(0, choices.IndexOf(speakerProperty.stringValue ?? string.Empty));
            string FormatSpeaker(string key) => FormatSpeakerChoice(catalog, key);

            var popup = new PopupField<string>("说话人", choices, currentIndex, FormatSpeaker, FormatSpeaker)
            {
                name = "DialogueSpeakerPopup",
                tooltip = "选择说话人。实际写入 DialogueSentence.Speaker 字符串，不会新增运行时字段。"
            };
            popup.RegisterValueChangedCallback(evt =>
            {
                speakerProperty.stringValue = evt.newValue ?? string.Empty;
                context.SerializedObject.ApplyModifiedProperties();
                context.SerializedObject.UpdateIfRequiredOrScript();
                onChanged?.Invoke();
            });
            parent.Add(popup);

            var speaker = FindSpeaker(catalog, speakerProperty.stringValue);
            if (speaker != null)
            {
                AddSpeakerPreview(parent, speaker);
            }
            else if (!string.IsNullOrWhiteSpace(speakerProperty.stringValue))
            {
                parent.Add(new HelpBox($"说话人“{speakerProperty.stringValue}”不在当前 Catalog 中。", HelpBoxMessageType.Warning));
            }
        }

        private static List<string> BuildSpeakerChoices(DialogueSpeakerCatalog catalog, string currentSpeaker)
        {
            var result = new List<string> { string.Empty };
            if (catalog?.Speakers != null)
            {
                for (var i = 0; i < catalog.Speakers.Length; i++)
                {
                    var speaker = catalog.Speakers[i];
                    if (speaker == null || string.IsNullOrWhiteSpace(speaker.SpeakerKey))
                    {
                        continue;
                    }

                    if (!result.Contains(speaker.SpeakerKey))
                    {
                        result.Add(speaker.SpeakerKey);
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(currentSpeaker) && !result.Contains(currentSpeaker))
            {
                result.Add(currentSpeaker);
            }

            return result;
        }

        private static string FormatSpeakerChoice(DialogueSpeakerCatalog catalog, string speakerKey)
        {
            if (string.IsNullOrWhiteSpace(speakerKey))
            {
                return "（旁白 / 留空）";
            }

            var speaker = FindSpeaker(catalog, speakerKey);
            return speaker == null || string.IsNullOrWhiteSpace(speaker.DisplayName)
                ? speakerKey
                : $"{speakerKey} - {speaker.DisplayName}";
        }

        private static DialogueSpeakerEditorData FindSpeaker(DialogueSpeakerCatalog catalog, string speakerKey)
        {
            if (catalog?.Speakers == null || string.IsNullOrWhiteSpace(speakerKey))
            {
                return null;
            }

            for (var i = 0; i < catalog.Speakers.Length; i++)
            {
                var speaker = catalog.Speakers[i];
                if (speaker != null && string.Equals(speaker.SpeakerKey, speakerKey, StringComparison.Ordinal))
                {
                    return speaker;
                }
            }

            return null;
        }

        private static void AddSpeakerPreview(VisualElement parent, DialogueSpeakerEditorData speaker)
        {
            var preview = new VisualElement
            {
                name = "DialogueSpeakerPreview"
            };
            preview.style.flexDirection = FlexDirection.Row;
            preview.style.alignItems = Align.Center;
            preview.style.marginBottom = 6f;

            if (speaker.Portrait != null)
            {
                // TODO(Phase 6+): If Portrait comes from a SpriteAtlas, use sprite-aware rendering
                // instead of speaker.Portrait.texture to avoid showing the whole atlas.
                var portrait = new Image
                {
                    image = speaker.Portrait.texture,
                    scaleMode = ScaleMode.ScaleToFit
                };
                portrait.style.width = 42f;
                portrait.style.height = 42f;
                portrait.style.marginRight = 6f;
                preview.Add(portrait);
            }

            var swatch = new VisualElement
            {
                name = "DialogueSpeakerThemeColor"
            };
            swatch.style.width = 18f;
            swatch.style.height = 18f;
            swatch.style.marginRight = 6f;
            swatch.style.backgroundColor = speaker.ThemeColor;
            preview.Add(swatch);

            var displayName = string.IsNullOrWhiteSpace(speaker.DisplayName) ? speaker.SpeakerKey : speaker.DisplayName;
            preview.Add(new Label($"说话人预览：{displayName}"));

            if (speaker.PreviewVoice != null)
            {
                var previewButton = new Button(() =>
                {
                    if (!DialogueEditorAudioPreview.Play(speaker.PreviewVoice, out var error))
                    {
                        Debug.LogWarning($"[NiumaGalEditor] Speaker 语音试听失败：{error}");
                    }
                })
                {
                    text = "试听语音"
                };
                previewButton.SetEnabled(DialogueEditorAudioPreview.IsSupported);
                previewButton.style.marginLeft = 8f;
                preview.Add(previewButton);
            }

            parent.Add(preview);
        }
    }
}
