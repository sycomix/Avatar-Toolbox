using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Yueby.Tools.AvatarToolbox.MaterialPreset;

namespace Yueby.Tools.AvatarToolbox.MaterialPreset.Editor.Services
{
    public class PresetObjectFactory
    {
        private MaterialPresetManager _manager;

        public PresetObjectFactory(MaterialPresetManager manager)
        {
            _manager = manager;
        }

        public GameObject CreateObjectInstance(PresetGroup group, bool isPreview, Transform parent = null)
        {
            GameObject clone = null;

            var prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(_manager.gameObject);
            if (prefabAsset != null)
            {
                clone = PrefabUtility.InstantiatePrefab(prefabAsset) as GameObject;
            }

            if (clone == null)
            {
                clone = UnityEngine.Object.Instantiate(_manager.gameObject);
            }

            if (clone == null) return null;

            Undo.RegisterCreatedObjectUndo(clone, isPreview ? "Create Preview" : "Create Object");

            clone.name = isPreview ? $"{_manager.name}_Preview_{group.GroupName}" : group.GroupName;

            if (parent != null)
            {
                clone.transform.SetParent(parent, false);
            }
            else if (isPreview)
            {
                clone.transform.position = _manager.transform.position;
                clone.transform.rotation = _manager.transform.rotation;
            }
            else
            {
                clone.transform.position = Vector3.zero;
                clone.transform.rotation = Quaternion.identity;
                clone.transform.localScale = Vector3.one;
            }

            var manager = clone.GetComponent<MaterialPresetManager>();
            if (manager != null)
            {
                Undo.RecordObject(manager, "Disable Manager");
                manager.enabled = false;
            }

            ApplyGroupToRoot(group, clone.transform);

            return clone;
        }

        public void CreateObject(PresetGroup group)
        {
            if (group == null || _manager == null) return;

            CreateObjectInstance(group, false);
        }

        public void CreateFolderObjects(PresetFolder folder)
        {
            if (folder == null || _manager == null) return;

            var groups = folder.GetAllGroupsRecursive();
            if (groups.Count == 0) return;

            if (groups.Count > 10)
            {
                if (!EditorUtility.DisplayDialog("警告", $"您即将创建 {groups.Count} 个新对象。是否继续?", "是", "取消"))
                    return;
            }

            var container = new GameObject(folder.Name);
            container.transform.position = Vector3.zero;
            container.transform.rotation = Quaternion.identity;
            container.transform.localScale = Vector3.one;
            Undo.RegisterCreatedObjectUndo(container, "Create Folder Objects");

            foreach (var group in groups)
            {
                var instance = CreateObjectInstance(group, false, container.transform);
                if (instance != null)
                {
                    instance.transform.localPosition = Vector3.zero;
                    instance.transform.localRotation = Quaternion.identity;
                    instance.transform.localScale = Vector3.one;
                }
            }
        }

        public void ApplyGroup(PresetGroup group)
        {
            if (group == null || _manager == null) return;
            Undo.RecordObject(_manager.gameObject, "应用材质预设");
            ApplyGroupToRoot(group, _manager.transform);
        }

        public void ApplyGroupToRoot(PresetGroup group, Transform root)
        {
            foreach (var config in group.RendererConfigs)
            {
                Renderer target = null;
                if (!string.IsNullOrEmpty(config.RendererPath))
                {
                    var node = root.Find(config.RendererPath);
                    if (node) target = node.GetComponent<Renderer>();
                }
                if (target == null) continue;

                Undo.RecordObject(target, "应用材质预设");
                var currentMaterials = target.sharedMaterials;
                var newMaterials = new Material[currentMaterials.Length];
                Array.Copy(currentMaterials, newMaterials, currentMaterials.Length);

                // 记录已经被替换的槽位索引
                var replacedIndices = new HashSet<int>();

                // 第一遍：智能匹配
                foreach (var slot in config.MaterialSlots)
                {
                    var match = MaterialMatcher.FindBestMatch(slot, currentMaterials, config.MatchMode);
                    if (match != null)
                    {
                        int index = Array.IndexOf(currentMaterials, match);
                        if (index >= 0)
                        {
                            newMaterials[index] = slot.MaterialRef;
                            replacedIndices.Add(index);
                        }
                    }
                }

                // 第二遍：对于没有匹配到的，按索引回退替换
                foreach (var slot in config.MaterialSlots)
                {
                    // 检查是否已经通过智能匹配替换过
                    bool alreadyReplaced = false;
                    for (int i = 0; i < newMaterials.Length; i++)
                    {
                        if (newMaterials[i] == slot.MaterialRef)
                        {
                            alreadyReplaced = true;
                            break;
                        }
                    }

                    // 如果没有被替换，且槽位索引有效，且该索引未被占用
                    if (!alreadyReplaced &&
                        slot.SlotIndex >= 0 &&
                        slot.SlotIndex < newMaterials.Length &&
                        !replacedIndices.Contains(slot.SlotIndex))
                    {
                        newMaterials[slot.SlotIndex] = slot.MaterialRef;
                        replacedIndices.Add(slot.SlotIndex);
                    }
                }

                target.sharedMaterials = newMaterials;
                EditorUtility.SetDirty(target);
            }
        }
    }
}

