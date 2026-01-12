using System.Collections.Generic;
using UnityEditorInternal;
using UnityEngine;
using Yueby.Core.Utils;

namespace YuebyAvatarTools.ComponentTransfer.Editor
{
    /// <summary>
    /// 组件转移基础类
    /// 支持智能骨骼映射和跨目标决策缓存
    /// </summary>
    public abstract class ComponentTransferBase : IComponentTransferPlugin
    {
        public abstract string Name { get; }
        public abstract string Description { get; }
        public bool IsEnabled { get; set; } = true;

        // 源根对象和目标根对象
        protected Transform SourceRoot { get; private set; }
        protected Transform TargetRoot { get; private set; }

        // 缓存字典
        protected Dictionary<Transform, string> _pathCache = new Dictionary<Transform, string>();
        protected Dictionary<Transform, Transform> _targetCache = new Dictionary<Transform, Transform>();

        // 跳过决定缓存（单个目标内使用）
        protected List<Transform> _skippedBones = new List<Transform>();

        // 自动创建的对象集合（用于排除候选项）
        protected HashSet<Transform> _autoCreatedObjects = new HashSet<Transform>();

        // 骨骼映射管理器
        protected BoneMappingManager _boneMappingManager;

        // 是否启用智能骨骼映射
        protected bool _enableSmartBoneMapping = true;

        // 转移是否被用户停止
        protected bool _transferStopped = false;

        // 跨目标的用户决策缓存（静态，在多个目标对象之间共享）
        protected static Dictionary<string, CrossTargetUserDecision> _crossTargetDecisions = new Dictionary<string, CrossTargetUserDecision>();

        // 构造函数
        protected ComponentTransferBase()
        {
            _boneMappingManager = new BoneMappingManager();
        }

        /// <summary>
        /// 检查转移是否应该停止
        /// </summary>
        public bool ShouldStopTransfer()
        {
            return _transferStopped;
        }

        /// <summary>
        /// 重置停止标志
        /// </summary>
        public void ResetStopFlag()
        {
            _transferStopped = false;
            _skippedBones.Clear();
            _autoCreatedObjects.Clear();
        }

        /// <summary>
        /// 清空跨目标决策缓存（静态方法，用于开始新的转移任务时）
        /// </summary>
        public static void ClearCrossTargetDecisions()
        {
            _crossTargetDecisions.Clear();
        }

        protected virtual void ClearCaches()
        {
            _pathCache.Clear();
            _targetCache.Clear();
            _boneMappingManager?.ClearManualMappings();
            ResetStopFlag();
            // 注意：不清空 _crossTargetDecisions，因为它是跨目标共享的
        }

        protected string GetCachedPath(Transform transform, Transform root)
        {
            if (transform == null || root == null)
                return "";

            var cacheKey = transform;
            if (_pathCache.TryGetValue(cacheKey, out var cachedPath))
                return cachedPath;

            var path = GetRelativePath(root, transform);
            _pathCache[cacheKey] = path;
            return path;
        }

        protected Transform FindTargetInCache(Transform source)
        {
            if (source == null)
                return null;

            return _targetCache.TryGetValue(source, out var cachedTarget) ? cachedTarget : null;
        }

        protected GameObject GetOrCreateTargetObject(Transform source, Transform targetRoot, bool createIfNotExist = true)
        {
            if (source == null || targetRoot == null)
                return null;

            // 1. 检查停止标志
            if (ShouldStopTransfer())
            {
                return null;
            }

            // 2. 检查跳过缓存
            if (_skippedBones.Contains(source))
            {
                return null;
            }

            // 3. 检查缓存
            var cachedTarget = FindTargetInCache(source);
            if (cachedTarget != null)
                return cachedTarget.gameObject;

            // 4. 如果是根对象，直接返回
            var relativePath = GetRelativePath(SourceRoot, source);
            if (string.IsNullOrEmpty(relativePath))
                return targetRoot.gameObject;

            // 3. 使用智能骨骼映射系统
            if (_enableSmartBoneMapping && _boneMappingManager != null)
            {
                // 传递自动创建的对象列表，避免它们成为匹配候选项
                var mappingResult = _boneMappingManager.FindBestMatch(source, SourceRoot, targetRoot, _autoCreatedObjects);

                // 如果有高置信度匹配，直接使用
                if (mappingResult != null && mappingResult.Target != null && mappingResult.IsHighConfidence)
                {
                    _targetCache[source] = mappingResult.Target;
                    return mappingResult.Target.gameObject;
                }

                // 其他情况（低置信度或无匹配）：需要用户决策

                // 1. 检查跨目标决策缓存
                var sourceRelativePath = relativePath;
                CrossTargetUserDecision cachedDecision = null;
                if (_crossTargetDecisions.TryGetValue(sourceRelativePath, out cachedDecision))
                {
                    // 应用缓存的决策
                    YuebyLogger.LogInfo("BoneMapping", $"应用上次决策: {source.name} ({cachedDecision.ChoiceType})");

                    switch (cachedDecision.ChoiceType)
                    {
                        case BoneMappingUserChoice.SelectCandidate:
                            // 根据相对路径查找候选项
                            if (!string.IsNullOrEmpty(cachedDecision.SelectedTargetRelativePath))
                            {
                                var previouslySelectedTarget = FindByRelativePath(targetRoot, cachedDecision.SelectedTargetRelativePath);
                                if (previouslySelectedTarget != null)
                                {
                                    _boneMappingManager.AddManualMapping(source, previouslySelectedTarget);
                                    _targetCache[source] = previouslySelectedTarget;
                                    return previouslySelectedTarget.gameObject;
                                }
                            }
                            break;

                        case BoneMappingUserChoice.CreateNew:
                            var newTarget = CreateTargetObjectSmart(source, targetRoot);
                            if (newTarget != null)
                            {
                                _targetCache[source] = newTarget;
                                return newTarget.gameObject;
                            }
                            return null;

                        case BoneMappingUserChoice.Skip:
                            if (!_skippedBones.Contains(source))
                            {
                                _skippedBones.Add(source);
                            }
                            return null;

                        case BoneMappingUserChoice.Stop:
                            _transferStopped = true;
                            return null;
                    }
                }

                // 2. 没有缓存决策，弹窗让用户选择
                List<Transform> candidates = null;
                if (mappingResult != null && mappingResult.Candidates != null && mappingResult.Candidates.Count > 0)
                {
                    candidates = mappingResult.Candidates;
                }
                else
                {
                    // 完全没有候选项，传递空列表
                    candidates = new List<Transform>();
                }

                BoneMappingUserResult userResult = null;
                bool userMadeChoice = false;

                // 查找最近匹配的父级骨骼
                var nearestMatchedParent = FindNearestMatchedParent(source, SourceRoot, targetRoot);

                BoneMappingRecommendationWindow.Show(
                    source,
                    SourceRoot,
                    targetRoot,
                    candidates,
                    result =>
                    {
                        userResult = result;
                        userMadeChoice = true;
                    },
                    nearestMatchedParent
                );

                // 3. 处理用户选择并记录到跨目标缓存
                if (userMadeChoice && userResult != null)
                {
                    // 记录到跨目标决策缓存
                    var decision = new CrossTargetUserDecision
                    {
                        ChoiceType = userResult.ChoiceType
                    };

                    switch (userResult.ChoiceType)
                    {
                        case BoneMappingUserChoice.SelectCandidate:
                            if (userResult.SelectedTarget != null)
                            {
                                // 记录候选项的相对路径
                                decision.SelectedTargetRelativePath = GetRelativePath(targetRoot, userResult.SelectedTarget);
                                _crossTargetDecisions[sourceRelativePath] = decision;

                                _boneMappingManager.AddManualMapping(source, userResult.SelectedTarget);
                                _targetCache[source] = userResult.SelectedTarget;
                                return userResult.SelectedTarget.gameObject;
                            }
                            break;

                        case BoneMappingUserChoice.CreateNew:
                            _crossTargetDecisions[sourceRelativePath] = decision;

                            // 智能创建：基于父级匹配来创建新骨骼
                            var newTarget = CreateTargetObjectSmart(source, targetRoot);
                            if (newTarget != null)
                            {
                                _targetCache[source] = newTarget;
                                return newTarget.gameObject;
                            }
                            return null;

                        case BoneMappingUserChoice.Skip:
                            _crossTargetDecisions[sourceRelativePath] = decision;
                            if (!_skippedBones.Contains(source))
                            {
                                _skippedBones.Add(source);
                            }
                            return null; // 直接返回 null，不创建对象

                        case BoneMappingUserChoice.Stop:
                            YuebyLogger.LogWarning("BoneMapping",
                                $"用户停止转移流程");
                            _transferStopped = true;
                            return null; // 返回 null 并设置停止标志
                    }
                }

                // 用户没有做出选择（不应该发生），返回 null
                return null;
            }

            // 如果未启用智能映射，返回 null（不再自动创建）
            YuebyLogger.LogWarning("BoneMapping", $"智能骨骼映射未启用，无法为 {source.name} 找到目标");
            return null;
        }

        /// <summary>
        /// 智能创建目标对象：基于父级匹配来确定创建位置
        /// </summary>
        protected Transform CreateTargetObjectSmart(Transform source, Transform targetRoot)
        {
            if (source == null || targetRoot == null)
                return null;

            // 如果是根对象，直接返回
            if (source == SourceRoot)
                return targetRoot;

            // 找到源骨骼的父级
            var sourceParent = source.parent;
            if (sourceParent == null)
                return null;

            // 递归找到父级在目标模型中的对应位置
            Transform targetParent = null;

            if (sourceParent == SourceRoot)
            {
                // 父级是根，直接使用目标根
                targetParent = targetRoot;
            }
            else
            {
                // 递归查找父级的映射（不创建新对象，只查找）
                var parentObject = GetOrCreateTargetObject(sourceParent, targetRoot, false);
                if (parentObject != null)
                {
                    targetParent = parentObject.transform;
                }
                else
                {
                    // 如果父级没有找到，尝试递归创建父级
                    targetParent = CreateTargetObjectSmart(sourceParent, targetRoot);
                }
            }

            if (targetParent == null)
            {
                YuebyLogger.LogWarning("BoneMapping", $"无法找到父级映射，使用目标根: {source.name}");
                targetParent = targetRoot;
            }

            // 在找到的父级下创建新骨骼
            var existingChild = targetParent.Find(source.name);
            if (existingChild != null)
            {
                return existingChild;
            }

            var newGo = new GameObject(source.name);
            newGo.transform.SetParent(targetParent, false);

            // 复制Transform信息
            newGo.transform.localPosition = source.localPosition;
            newGo.transform.localRotation = source.localRotation;
            newGo.transform.localScale = source.localScale;

            UnityEditor.Undo.RegisterCreatedObjectUndo(newGo, $"Create Object: {source.name}");

            // 记录自动创建的对象，避免被当作匹配候选项
            _autoCreatedObjects.Add(newGo.transform);

            return newGo.transform;
        }

        protected string GetRelativePath(Transform root, Transform target)
        {
            if (target == root)
                return "";

            var path = new System.Text.StringBuilder();
            var current = target;

            while (current != null && current != root)
            {
                if (path.Length > 0)
                    path.Insert(0, "/");
                path.Insert(0, current.name);
                current = current.parent;
            }

            if (current != root)
                return "";

            return path.ToString();
        }

        /// <summary>
        /// 根据相对路径查找Transform
        /// </summary>
        protected Transform FindByRelativePath(Transform root, string relativePath)
        {
            if (root == null || string.IsNullOrEmpty(relativePath))
                return null;

            var pathParts = relativePath.Split('/');
            var current = root;

            foreach (var part in pathParts)
            {
                var child = current.Find(part);
                if (child == null)
                    return null;
                current = child;
            }

            return current;
        }

        /// <summary>
        /// 查找源骨骼的最近匹配父级（在目标对象上）
        /// 从源骨骼的父级开始向上遍历，返回第一个成功匹配的父级骨骼（不包括根对象）
        /// </summary>
        public Transform FindNearestMatchedParent(Transform source, Transform sourceRoot, Transform targetRoot)
        {
            if (source == null || sourceRoot == null || targetRoot == null)
                return null;

            var current = source.parent;

            // 从当前骨骼的父级开始向上遍历
            while (current != null && current != sourceRoot)
            {
                // 尝试使用骨骼映射管理器查找匹配
                if (_boneMappingManager != null)
                {
                    var mappingResult = _boneMappingManager.FindBestMatch(current, sourceRoot, targetRoot, _autoCreatedObjects);
                    if (mappingResult != null && mappingResult.Target != null && mappingResult.Confidence >= 0.8f)
                    {
                        // 找到第一个高置信度匹配且不是根对象的父级，立即返回
                        if (mappingResult.Target != targetRoot)
                        {
                            YuebyLogger.LogInfo("BoneMapping", 
                                $"找到最近匹配父级: {current.name} -> {mappingResult.Target.name} (置信度: {mappingResult.Confidence:P0})");
                            return mappingResult.Target;
                        }
                    }
                }

                current = current.parent;
            }

            return null;
        }

        protected T CopyComponentWithUndo<T>(GameObject source, GameObject target, string undoName = null) where T : Component
        {
            if (source == null || target == null)
            {
                YuebyLogger.LogError("ComponentTransfer", $"Source or target is null when copying {typeof(T).Name}");
                return null;
            }

            var sourceComponent = source.GetComponent<T>();
            if (sourceComponent == null)
                return null;

            try
            {
                UnityEditor.Undo.RegisterCompleteObjectUndo(target, undoName ?? $"Transfer {typeof(T).Name}");

                ComponentUtility.CopyComponent(sourceComponent);
                var targetComponent = target.GetComponent<T>();

                if (targetComponent != null)
                {
                    UnityEditor.Undo.RegisterCompleteObjectUndo(targetComponent, $"Modify {typeof(T).Name}");
                    ComponentUtility.PasteComponentValues(targetComponent);
                }
                else
                {
                    ComponentUtility.PasteComponentAsNew(target);
                    targetComponent = target.GetComponent<T>();
                    if (targetComponent != null)
                    {
                        UnityEditor.Undo.RegisterCreatedObjectUndo(targetComponent, $"Create {typeof(T).Name}");
                    }
                }

                if (targetComponent != null)
                {
                    UnityEditor.EditorUtility.SetDirty(target);
                    UnityEditor.EditorUtility.SetDirty(targetComponent);
                }

                return targetComponent;
            }
            catch (System.Exception e)
            {
                YuebyLogger.LogError("ComponentTransfer", $"Exception while copying {typeof(T).Name} from {source.name} to {target.name}: {e.Message}");
                return null;
            }
        }

        public virtual void DrawSettings()
        {
            // 默认空实现，子类可选择性重写
        }
        
        public abstract bool ExecuteTransfer(Transform sourceRoot, Transform targetRoot);
        
        public abstract bool CanTransfer(Transform sourceRoot, Transform targetRoot);

        // 设置根对象的方法
        protected void SetRootObjects(Transform sourceRoot, Transform targetRoot)
        {
            SourceRoot = sourceRoot;
            TargetRoot = targetRoot;
            ClearCaches();

            // 重置窗口的全局停止标志
            BoneMappingRecommendationWindow.ResetGlobalStopFlag();
        }
    }
}