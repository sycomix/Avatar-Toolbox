using UnityEngine;
using Yueby.Core.Utils;

namespace YuebyAvatarTools.ComponentTransfer.Editor.Plugins
{
    /// <summary>
    /// 激活状态同步插件
    /// </summary>
    public class ActiveStateTransferPlugin : ComponentTransferBase
    {
        public override string Name => "Active状态同步";
        public override string Description => "同步GameObject的激活状态";

        public override bool ExecuteTransfer(Transform sourceRoot, Transform targetRoot)
        {
            if (!IsEnabled) return true;

            // 设置根对象
            SetRootObjects(sourceRoot, targetRoot);
            bool success = true;

            var sourceTransforms = SourceRoot.GetComponentsInChildren<Transform>(true);

            if (sourceTransforms.Length <= 0) return true;

            int syncedCount = 0;
            int totalObjects = 0;

            foreach (var sourceTransform in sourceTransforms)
            {
                // 检查是否应该停止
                if (ShouldStopTransfer())
                {
                    YuebyLogger.LogWarning("ActiveStateTransferPlugin", "转移已被用户停止");
                    return false;
                }
                
                totalObjects++;
                try
                {
                    var sourceActive = sourceTransform.gameObject.activeSelf;

                    var targetGo = GetOrCreateTargetObject(sourceTransform, TargetRoot);
                    if (targetGo == null)
                    {
                        continue;
                    }

                    var targetActive = targetGo.activeSelf;

                    if (sourceActive != targetActive)
                    {
                        UnityEditor.Undo.RegisterCompleteObjectUndo(targetGo, "Sync Active State");
                        targetGo.SetActive(sourceActive);
                        UnityEditor.EditorUtility.SetDirty(targetGo);
                        syncedCount++;
                    }
                }
                catch (System.Exception e)
                {
                    YuebyLogger.LogError("ActiveStateTransferPlugin", $"同步激活状态时发生错误 {sourceTransform.name}: {e.Message}");
                    success = false;
                }
            }

            return success;
        }

        public override bool CanTransfer(Transform sourceRoot, Transform targetRoot)
        {
            if (sourceRoot == null || targetRoot == null) return false;

            var sourceTransforms = sourceRoot.GetComponentsInChildren<Transform>(true);
            return sourceTransforms.Length > 0;
        }
    }
} 