using UnityEngine;
using UnityEngine.Animations;
using Yueby.Core.Utils;

namespace YuebyAvatarTools.ComponentTransfer.Editor.Plugins
{
    /// <summary>
    /// Constraint转移插件
    /// </summary>
    public class ConstraintTransferPlugin : ComponentTransferBase
    {
        public override string Name => "Constraint转移";
        public override string Description => "转移Parent、Rotation、Position约束组件";

        public override bool ExecuteTransfer(Transform sourceRoot, Transform targetRoot)
        {
            if (!IsEnabled) return true;

            // 设置根对象
            SetRootObjects(sourceRoot, targetRoot);
            bool success = true;

            var allTransforms = SourceRoot.GetComponentsInChildren<Transform>(true);

            foreach (Transform sourceTransform in allTransforms)
            {
                // 检查是否应该停止
                if (ShouldStopTransfer())
                {
                    YuebyLogger.LogWarning("ConstraintTransferPlugin", "转移已被用户停止");
                    return false;
                }

                try
                {
                    if (HasAnyConstraint(sourceTransform))
                    {
                        success &= TransferConstraints(sourceTransform);
                    }
                }
                catch (System.Exception e)
                {
                    YuebyLogger.LogError("ConstraintTransferPlugin", $"转移约束组件时发生错误 {sourceTransform.name}: {e.Message}");
                    success = false;
                }
            }

            return success;
        }

        public override bool CanTransfer(Transform sourceRoot, Transform targetRoot)
        {
            if (sourceRoot == null || targetRoot == null) return false;

            var allTransforms = sourceRoot.GetComponentsInChildren<Transform>(true);
            foreach (Transform transform in allTransforms)
            {
                if (HasAnyConstraint(transform))
                    return true;
            }
            return false;
        }

        private bool HasAnyConstraint(Transform transform)
        {
            return transform.GetComponent<ParentConstraint>() != null ||
                   transform.GetComponent<RotationConstraint>() != null ||
                   transform.GetComponent<PositionConstraint>() != null;
        }

        private bool TransferConstraints(Transform sourceTransform)
        {
            bool success = true;

            var targetGo = GetOrCreateTargetObject(sourceTransform, TargetRoot);
            if (targetGo == null)
            {
                return false;
            }

            // 转移 ParentConstraint
            if (sourceTransform.GetComponent<ParentConstraint>() != null)
            {
                success &= TransferConstraint<ParentConstraint>(sourceTransform.gameObject, targetGo);
                if (ShouldStopTransfer())
                    return false;
            }

            // 转移 RotationConstraint
            if (sourceTransform.GetComponent<RotationConstraint>() != null)
            {
                success &= TransferConstraint<RotationConstraint>(sourceTransform.gameObject, targetGo);
                if (ShouldStopTransfer())
                    return false;
            }

            // 转移 PositionConstraint
            if (sourceTransform.GetComponent<PositionConstraint>() != null)
            {
                success &= TransferConstraint<PositionConstraint>(sourceTransform.gameObject, targetGo);
                if (ShouldStopTransfer())
                    return false;
            }

            return success;
        }

        private bool TransferConstraint<T>(GameObject sourceObj, GameObject target) where T : Component, IConstraint
        {
            var constraints = sourceObj.GetComponents<T>();
            bool success = true;

            foreach (var constraint in constraints)
            {
                var lastLocked = constraint.locked;
                var lastActive = constraint.constraintActive;

                constraint.locked = false;
                constraint.constraintActive = false;

                var targetConstraint = CopyComponentWithUndo<T>(sourceObj, target);
                if (targetConstraint == null)
                {
                    success = false;
                    continue;
                }

                // 恢复源约束的状态
                constraint.locked = lastLocked;
                constraint.constraintActive = lastActive;

                // 处理约束源
                for (var i = 0; i < constraint.sourceCount; i++)
                {
                    var source = constraint.GetSource(i);
                    var targetSourceObject = GetOrCreateTargetObject(source.sourceTransform, TargetRoot);
                    if (targetSourceObject == null)
                    {
                        continue;
                    }

                    targetConstraint.SetSource(i, new ConstraintSource()
                    {
                        sourceTransform = targetSourceObject.transform,
                        weight = source.weight
                    });
                }

                targetConstraint.constraintActive = constraint.constraintActive;
            }

            return success;
        }
    }
} 
