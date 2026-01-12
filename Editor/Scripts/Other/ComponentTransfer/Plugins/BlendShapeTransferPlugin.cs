using UnityEngine;
using Yueby.Core.Utils;

namespace YuebyAvatarTools.ComponentTransfer.Editor.Plugins
{
    /// <summary>
    /// BlendShape转移插件
    /// </summary>
    public class BlendShapeTransferPlugin : ComponentTransferBase
    {
        public override string Name => "BlendShape转移";
        public override string Description => "转移SkinnedMeshRenderer的BlendShape权重";

        public override bool ExecuteTransfer(Transform sourceRoot, Transform targetRoot)
        {
            if (!IsEnabled) return true;

            // 设置根对象
            SetRootObjects(sourceRoot, targetRoot);
            bool success = true;

            var sourceSkinnedMeshRenderers = SourceRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);

            if (sourceSkinnedMeshRenderers.Length <= 0) return true;

            foreach (var sourceRenderer in sourceSkinnedMeshRenderers)
            {
                // 检查是否应该停止
                if (ShouldStopTransfer())
                {
                    YuebyLogger.LogWarning("BlendShapeTransferPlugin", "转移已被用户停止");
                    return false;
                }
                
                try
                {
                    var targetGo = GetOrCreateTargetObject(sourceRenderer.transform, TargetRoot);
                    if (targetGo == null)
                    {
                        YuebyLogger.LogWarning("BlendShapeTransferPlugin", $"无法为目标渲染器创建对象: {sourceRenderer.name}");
                        success = false;
                        continue;
                    }

                    var targetRenderer = targetGo.GetComponent<SkinnedMeshRenderer>();
                    if (targetRenderer == null)
                    {
                        YuebyLogger.LogWarning("BlendShapeTransferPlugin", $"跳过BlendShape转移 {targetGo.name}: 未找到SkinnedMeshRenderer组件");
                        success = false;
                        continue;
                    }

                    // 获取源和目标的网格
                    var sourceMesh = sourceRenderer.sharedMesh;
                    var targetMesh = targetRenderer.sharedMesh;

                    if (sourceMesh == null || targetMesh == null)
                    {
                        YuebyLogger.LogWarning("BlendShapeTransferPlugin", $"跳过BlendShape转移 {targetGo.name}: 源或目标网格为空");
                        success = false;
                        continue;
                    }

                    // 获取源网格的 BlendShape 权重
                    var sourceBlendShapeCount = sourceMesh.blendShapeCount;
                    if (sourceBlendShapeCount == 0)
                    {
                        continue;
                    }

                    // 注册撤销操作
                    UnityEditor.Undo.RegisterCompleteObjectUndo(targetRenderer, "Transfer BlendShapes");

                    int transferredCount = 0;
                    int totalBlendShapes = 0;

                    // 遍历源网格的所有 BlendShape
                    for (int i = 0; i < sourceBlendShapeCount; i++)
                    {
                        var sourceBlendShapeName = sourceMesh.GetBlendShapeName(i);
                        var sourceWeight = sourceRenderer.GetBlendShapeWeight(i);

                        // 在目标网格中查找同名的 BlendShape
                        var targetBlendShapeIndex = targetMesh.GetBlendShapeIndex(sourceBlendShapeName);

                        if (targetBlendShapeIndex >= 0)
                        {
                            // 找到同名的 BlendShape，转移权重
                            targetRenderer.SetBlendShapeWeight(targetBlendShapeIndex, sourceWeight);
                            transferredCount++;
                        }

                        totalBlendShapes++;
                    }

                    // 标记为已修改
                    UnityEditor.EditorUtility.SetDirty(targetRenderer);

                    if (transferredCount == 0)
                    {
                        YuebyLogger.LogWarning("BlendShapeTransferPlugin", $"未找到匹配的BlendShape: {sourceRenderer.name}");
                    }
                }
                catch (System.Exception e)
                {
                    YuebyLogger.LogError("BlendShapeTransferPlugin", $"转移BlendShape权重时发生错误 {sourceRenderer.name}: {e.Message}");
                    success = false;
                }
            }

            return success;
        }

        public override bool CanTransfer(Transform sourceRoot, Transform targetRoot)
        {
            if (sourceRoot == null || targetRoot == null) return false;

            var renderers = sourceRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            return renderers.Length > 0;
        }
    }
} 