using System;
using System.Collections.Generic;
using NiumaGal.Dialogue.Data;
using NiumaGal.Enum;
using UnityEditor;
using UnityEngine;

namespace NiumaGal.Editor
{
    [FilePath("ProjectSettings/NiumaGalEditorSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class NiumaGalEditorSettings : ScriptableSingleton<NiumaGalEditorSettings>
    {
        [Tooltip("默认说话人清单。当前 DialogueAsset 没有单独绑定时使用。")]
        public DialogueSpeakerCatalog DefaultSpeakerCatalog;

        [Tooltip("按 DialogueAsset GUID 绑定的说话人清单。Unity 不序列化 Dictionary，因此这里使用数组。")]
        public DialogueSpeakerCatalogBinding[] SpeakerCatalogBindings = Array.Empty<DialogueSpeakerCatalogBinding>();

        [Tooltip("对话 Graph 节点颜色映射。运行时只保存叙事分类，颜色只用于编辑器显示。")]
        public DialogueNarrativeCategoryColor[] NarrativeCategoryColors =
        {
            new DialogueNarrativeCategoryColor { Category = DialogueNarrativeCategory.None, Color = new Color(0.78f, 0.78f, 0.78f, 1f) },
            new DialogueNarrativeCategoryColor { Category = DialogueNarrativeCategory.Main, Color = new Color(1f, 0.72f, 0.22f, 1f) },
            new DialogueNarrativeCategoryColor { Category = DialogueNarrativeCategory.Branch, Color = new Color(0.32f, 0.58f, 1f, 1f) },
            new DialogueNarrativeCategoryColor { Category = DialogueNarrativeCategory.FamilyLegend, Color = new Color(0.72f, 0.42f, 1f, 1f) },
            new DialogueNarrativeCategoryColor { Category = DialogueNarrativeCategory.Daily, Color = new Color(0.55f, 0.55f, 0.55f, 1f) },
            new DialogueNarrativeCategoryColor { Category = DialogueNarrativeCategory.Custom, Color = new Color(0.36f, 0.78f, 0.46f, 1f) }
        };

        [Tooltip("Speaker 为空时是否在后续校验阶段提示 Warning。允许旁白项目可关闭。")]
        public bool WarnWhenSpeakerEmpty = true;

        public DialogueSpeakerCatalog ResolveSpeakerCatalog(DialogueAsset asset)
        {
            return GetExplicitSpeakerCatalog(asset) ?? DefaultSpeakerCatalog;
        }

        public DialogueSpeakerCatalog GetExplicitSpeakerCatalog(DialogueAsset asset)
        {
            var guid = GetAssetGuid(asset);
            if (string.IsNullOrWhiteSpace(guid) || SpeakerCatalogBindings == null)
            {
                return null;
            }

            for (var i = 0; i < SpeakerCatalogBindings.Length; i++)
            {
                var binding = SpeakerCatalogBindings[i];
                if (binding == null || binding.SpeakerCatalog == null)
                {
                    continue;
                }

                if (string.Equals(binding.DialogueAssetGuid, guid, StringComparison.Ordinal))
                {
                    return binding.SpeakerCatalog;
                }
            }

            return null;
        }

        public Color ResolveNarrativeCategoryColor(DialogueNarrativeCategory category)
        {
            if (NarrativeCategoryColors != null)
            {
                for (var i = 0; i < NarrativeCategoryColors.Length; i++)
                {
                    var entry = NarrativeCategoryColors[i];
                    if (entry != null && entry.Category == category)
                    {
                        return entry.Color;
                    }
                }
            }

            return new Color(0.78f, 0.78f, 0.78f, 1f);
        }

        public void SetSpeakerCatalogForAsset(DialogueAsset asset, DialogueSpeakerCatalog catalog)
        {
            var guid = GetAssetGuid(asset);
            if (string.IsNullOrWhiteSpace(guid))
            {
                Debug.LogWarning("[NiumaGalEditor] DialogueAsset 尚未保存到项目中，无法建立 SpeakerCatalog 映射。");
                return;
            }

            var bindings = new List<DialogueSpeakerCatalogBinding>();
            if (SpeakerCatalogBindings != null)
            {
                for (var i = 0; i < SpeakerCatalogBindings.Length; i++)
                {
                    var binding = SpeakerCatalogBindings[i];
                    if (binding == null || string.IsNullOrWhiteSpace(binding.DialogueAssetGuid))
                    {
                        continue;
                    }

                    if (!string.Equals(binding.DialogueAssetGuid, guid, StringComparison.Ordinal))
                    {
                        bindings.Add(binding);
                    }
                }
            }

            if (catalog != null)
            {
                bindings.Add(new DialogueSpeakerCatalogBinding
                {
                    DialogueAssetGuid = guid,
                    SpeakerCatalog = catalog
                });
            }

            SpeakerCatalogBindings = bindings.ToArray();
            SaveSettings();
        }

        public void SaveSettings()
        {
            Save(true);
        }

        public static string GetAssetGuid(DialogueAsset asset)
        {
            if (asset == null)
            {
                return string.Empty;
            }

            var path = AssetDatabase.GetAssetPath(asset);
            return string.IsNullOrWhiteSpace(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
        }

        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            return new SettingsProvider("Project/Niuma/Gal Editor", SettingsScope.Project)
            {
                label = "Niuma Gal Editor",
                guiHandler = _ =>
                {
                    var settings = instance;
                    var serialized = new SerializedObject(settings);
                    serialized.UpdateIfRequiredOrScript();

                    EditorGUILayout.PropertyField(serialized.FindProperty(nameof(DefaultSpeakerCatalog)));
                    EditorGUILayout.PropertyField(serialized.FindProperty(nameof(SpeakerCatalogBindings)), true);
                    EditorGUILayout.PropertyField(serialized.FindProperty(nameof(NarrativeCategoryColors)), true);
                    EditorGUILayout.PropertyField(serialized.FindProperty(nameof(WarnWhenSpeakerEmpty)));

                    if (serialized.ApplyModifiedProperties())
                    {
                        settings.SaveSettings();
                    }
                }
            };
        }
    }

    [Serializable]
    public sealed class DialogueSpeakerCatalogBinding
    {
        [Tooltip("DialogueAsset 的 GUID。通过编辑器自动写入，通常不需要手填。")]
        public string DialogueAssetGuid;

        [Tooltip("该 DialogueAsset 使用的编辑器说话人清单。")]
        public DialogueSpeakerCatalog SpeakerCatalog;
    }

    [Serializable]
    public sealed class DialogueNarrativeCategoryColor
    {
        [Tooltip("叙事分类。与 DialogueSentence.NarrativeCategory 对应。")]
        public DialogueNarrativeCategory Category;

        [Tooltip("该分类在对话 Graph 节点中的显示颜色。只影响编辑器，不影响运行时。")]
        public Color Color = Color.white;
    }
}
