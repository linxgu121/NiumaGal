using System;
using System.Text;
using NiumaGal.Save;
using NiumaSave.Controller;
using NiumaSave.Data;
using NiumaSave.Provider;
using UnityEngine;

namespace NiumaGal.SaveBridge
{
    /// <summary>
    /// NiumaGal 存档桥接器。
    /// 负责把 Gal 进度事实转换为 NiumaSave 的 Section 数据，并在读档时恢复回进度仓库。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NiumaGalSaveAdapter : MonoBehaviour, ISaveDataProvider
    {
        private const string GalSectionId = "gal";
        private const string GalSectionVersion = "1";
        private const string GalSectionFormat = "json";

        [Header("模块引用")]
        [Tooltip("Gal 进度事实仓库。请拖入场景中的 NiumaGalProgressStore。")]
        [SerializeField] private NiumaGalProgressStore progressStore;

        [Tooltip("存档模块根控制器。开启自动注册时，请拖入场景中的 NiumaSaveController。")]
        [SerializeField] private NiumaSaveController saveController;

        [Header("注册行为")]
        [Tooltip("启用组件时是否自动注册到 NiumaSaveController。正式场景建议开启，并确保 NiumaSaveController 更早初始化，或把本组件挂在存档控制器子物体下。")]
        [SerializeField] private bool registerOnEnable = true;

        [Tooltip("引用为空时是否自动在场景中查找对应组件。调试阶段可以开启，正式场景建议手动绑定。")]
        [SerializeField] private bool autoFindReferences = true;

        private bool _registeredToSaveController;

        /// <summary>
        /// Gal 模块的稳定存档段 ID。
        /// </summary>
        public string SectionId => GalSectionId;

        /// <summary>
        /// Gal 存档段结构版本。
        /// </summary>
        public string SectionVersion => GalSectionVersion;

        /// <summary>
        /// Gal 进度数据版本号。
        /// NiumaSave 通过该值判断 Gal 模块是否发生变化。
        /// </summary>
        public long Revision => progressStore != null ? progressStore.Revision : 0L;

        private void Awake()
        {
            ResolveReferences(false);
        }

        private void OnEnable()
        {
            if (registerOnEnable)
            {
                RegisterToSaveController();
            }
        }

        private void OnDisable()
        {
            if (_registeredToSaveController && saveController != null)
            {
                saveController.UnregisterProvider(SectionId);
            }

            _registeredToSaveController = false;
        }

        /// <summary>
        /// 导出 Gal 进度事实为 NiumaSave Section。
        /// </summary>
        public SaveSectionData ExportSection()
        {
            ResolveReferences(false);
            if (progressStore == null)
            {
                throw new InvalidOperationException("NiumaGalSaveAdapter 缺少 NiumaGalProgressStore，无法导出 Gal 存档。");
            }

            var saveData = progressStore.ExportSaveData();
            var json = JsonUtility.ToJson(saveData);
            var bytes = Encoding.UTF8.GetBytes(json);

            return new SaveSectionData
            {
                SectionId = SectionId,
                SectionVersion = SectionVersion,
                Format = GalSectionFormat,
                DataEncoding = SaveDataEncoding.Base64,
                EncodedData = Convert.ToBase64String(bytes)
            };
        }

        /// <summary>
        /// 从 NiumaSave Section 导入 Gal 进度事实。
        /// </summary>
        public SaveSectionImportResult ImportSection(SaveSectionData section)
        {
            ResolveReferences(false);
            if (progressStore == null)
            {
                return SaveSectionImportResult.Fail(
                    SaveSectionImportErrorCode.ImportFailed,
                    "NiumaGalSaveAdapter 缺少 NiumaGalProgressStore，无法导入 Gal 存档。");
            }

            if (section == null)
            {
                return SaveSectionImportResult.Fail(SaveSectionImportErrorCode.NullSection, "Gal 存档段为空。");
            }

            if (!string.Equals(section.SectionId, SectionId, StringComparison.Ordinal))
            {
                return SaveSectionImportResult.Fail(
                    SaveSectionImportErrorCode.SectionIdMismatch,
                    $"Gal 存档段 ID 不匹配：expected={SectionId}, actual={section.SectionId}");
            }

            if (!string.Equals(section.SectionVersion, SectionVersion, StringComparison.Ordinal))
            {
                return SaveSectionImportResult.Fail(
                    SaveSectionImportErrorCode.VersionUnsupported,
                    $"Gal 存档段版本不支持：{section.SectionVersion}");
            }

            if (!string.Equals(section.DataEncoding, SaveDataEncoding.Base64, StringComparison.Ordinal))
            {
                return SaveSectionImportResult.Fail(
                    SaveSectionImportErrorCode.DataCorrupted,
                    $"Gal 存档段编码不支持：{section.DataEncoding}");
            }

            if (string.IsNullOrWhiteSpace(section.EncodedData))
            {
                return SaveSectionImportResult.Fail(SaveSectionImportErrorCode.DataCorrupted, "Gal 存档段数据为空。");
            }

            try
            {
                var bytes = Convert.FromBase64String(section.EncodedData);
                var json = Encoding.UTF8.GetString(bytes);
                var saveData = JsonUtility.FromJson<GalSaveData>(json);
                progressStore.ImportSaveData(saveData);
                return SaveSectionImportResult.Success();
            }
            catch (Exception ex)
            {
                return SaveSectionImportResult.Fail(
                    SaveSectionImportErrorCode.DataCorrupted,
                    $"Gal 存档段解析失败：{ex.Message}");
            }
        }

        [ContextMenu("NiumaGalSave/注册到存档模块")]
        private void RegisterToSaveController()
        {
            if (_registeredToSaveController)
            {
                return;
            }

            ResolveReferences(true);
            if (saveController == null)
            {
                return;
            }

            var registered = saveController.RegisterProvider(this);
            _registeredToSaveController = registered;
            if (!registered)
            {
                UnityEngine.Debug.LogWarning("[NiumaGalSaveAdapter] 注册 Gal 存档 Provider 失败。", this);
            }
        }

        [ContextMenu("NiumaGalSave/从存档模块取消注册")]
        private void UnregisterFromSaveController()
        {
            ResolveReferences(false);
            if (_registeredToSaveController && saveController != null)
            {
                saveController.UnregisterProvider(SectionId);
            }

            _registeredToSaveController = false;
        }

        private void ResolveReferences(bool logMissing)
        {
            if (!autoFindReferences)
            {
                return;
            }

            if (progressStore == null)
            {
                progressStore = NiumaGalProgressStore.Active ?? FindObjectOfType<NiumaGalProgressStore>();
            }

            if (saveController == null)
            {
#if UNITY_2023_1_OR_NEWER
                saveController = FindFirstObjectByType<NiumaSaveController>();
#else
                saveController = FindObjectOfType<NiumaSaveController>();
#endif
            }

            if (logMissing && progressStore == null)
            {
                UnityEngine.Debug.LogWarning("[NiumaGalSaveAdapter] 未找到 NiumaGalProgressStore，请在 Inspector 中绑定。", this);
            }

            if (logMissing && saveController == null)
            {
                UnityEngine.Debug.LogWarning("[NiumaGalSaveAdapter] 未找到 NiumaSaveController，请在 Inspector 中绑定。", this);
            }
        }
    }
}
