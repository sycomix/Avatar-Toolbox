using UnityEngine;
using Yueby.Core.Utils;

namespace YuebyAvatarTools.ComponentTransfer.Editor.Plugins
{
    /// <summary>
    /// Material转移插件
    /// </summary>
    public class MaterialTransferPlugin : ComponentTransferBase
    {
        public override string Name => "Material转移";
        public override string Description => "转移Renderer组件的材质";

        public override bool ExecuteTransfer(Transform sourceRoot, Transform targetRoot)
        {
            if (!IsEnabled) return true;

            // 设置根对象
            SetRootObjects(sourceRoot, targetRoot);
            bool success = true;

            var originRenderers = SourceRoot.GetComponentsInChildren<Renderer>(true);

            if (originRenderers.Length <= 0) return true;

            foreach (var originRenderer in originRenderers)
            {
                // 检查是否应该停止
                if (ShouldStopTransfer())
                {
                    YuebyLogger.LogWarning("MaterialTransferPlugin", "转移已被用户停止");
                    return false;
                }
                
                try
                {
                    var targetGo = GetOrCreateTargetObject(originRenderer.transform, TargetRoot);
                    if (targetGo == null)
                    {
                        YuebyLogger.LogWarning("MaterialTransferPlugin", $"无法为目标渲染器创建对象: {originRenderer.name}");
                        success = false;
                        continue;
                    }

                    var targetRenderer = targetGo.GetComponent<Renderer>();
                    if (targetRenderer == null)
                    {
                        YuebyLogger.LogWarning("MaterialTransferPlugin", $"跳过材质转移 {targetGo.name}: 未找到Renderer组件");
                        success = false;
                        continue;
                    }

                    var materials = originRenderer.sharedMaterials;
                    targetRenderer.sharedMaterials = materials;
                    UnityEditor.Undo.RegisterFullObjectHierarchyUndo(targetGo, "TransferMaterial");
                }
                catch (System.Exception e)
                {
                    YuebyLogger.LogError("MaterialTransferPlugin", $"转移材质时发生错误 {originRenderer.name}: {e.Message}");
                    success = false;
                }
            }

            return success;
        }

        public override bool CanTransfer(Transform sourceRoot, Transform targetRoot)
        {
            if (sourceRoot == null || targetRoot == null) return false;

            var renderers = sourceRoot.GetComponentsInChildren<Renderer>(true);
            return renderers.Length > 0;
        }
    }
} 
