using UnityEngine;
using Yueby.Core.Utils;

namespace YuebyAvatarTools.ComponentTransfer.Editor.Plugins
{
    /// <summary>
    /// Animator转移插件
    /// </summary>
    public class AnimatorTransferPlugin : ComponentTransferBase
    {
        public override string Name => "Animator转移";
        public override string Description => "转移Animator组件";

        public override bool ExecuteTransfer(Transform sourceRoot, Transform targetRoot)
        {
            if (!IsEnabled) return true;

            // 设置根对象
            SetRootObjects(sourceRoot, targetRoot);
            bool success = true;

            var sourceAnimators = SourceRoot.GetComponentsInChildren<Animator>(true);

            if (sourceAnimators.Length <= 0) return true;

            foreach (var sourceAnimator in sourceAnimators)
            {
                // 检查是否应该停止
                if (ShouldStopTransfer())
                {
                    YuebyLogger.LogWarning("AnimatorTransferPlugin", "转移已被用户停止");
                    return false;
                }
                
                try
                {
                    var targetGo = GetOrCreateTargetObject(sourceAnimator.transform, TargetRoot);
                    if (targetGo == null)
                    {
                        YuebyLogger.LogWarning("AnimatorTransferPlugin", $"无法为目标Animator创建对象: {sourceAnimator.name}");
                        success = false;
                        continue;
                    }

                    var targetAnimator = CopyComponentWithUndo<Animator>(sourceAnimator.gameObject, targetGo);
                    if (targetAnimator == null)
                    {
                        YuebyLogger.LogError("AnimatorTransferPlugin", $"复制Animator组件失败: {sourceAnimator.name}");
                        success = false;
                        continue;
                    }
                }
                catch (System.Exception e)
                {
                    YuebyLogger.LogError("AnimatorTransferPlugin", $"转移Animator组件时发生错误 {sourceAnimator.name}: {e.Message}");
                    success = false;
                }
            }

            return success;
        }

        public override bool CanTransfer(Transform sourceRoot, Transform targetRoot)
        {
            if (sourceRoot == null || targetRoot == null) return false;

            var animators = sourceRoot.GetComponentsInChildren<Animator>(true);
            return animators.Length > 0;
        }
    }
} 