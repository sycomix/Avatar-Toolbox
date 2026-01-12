using UnityEngine;
using Yueby.Core.Utils;

namespace YuebyAvatarTools.ComponentTransfer.Editor.Plugins
{
    /// <summary>
    /// ParticleSystem转移插件
    /// </summary>
    public class ParticleSystemTransferPlugin : ComponentTransferBase
    {
        public override string Name => "ParticleSystem转移";
        public override string Description => "转移ParticleSystem和ParticleSystemRenderer组件";

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
                    YuebyLogger.LogWarning("ParticleSystemTransferPlugin", "转移已被用户停止");
                    return false;
                }
                
                try
                {
                    if (HasParticleSystem(sourceTransform))
                    {
                        success &= TransferParticleSystems(sourceTransform);
                    }
                }
                catch (System.Exception e)
                {
                    YuebyLogger.LogError("ParticleSystemTransferPlugin", $"Error transferring particle systems for {sourceTransform.name}: {e.Message}");
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
                if (HasParticleSystem(transform))
                    return true;
            }
            return false;
        }

        private bool HasParticleSystem(Transform transform)
        {
            return transform.GetComponent<ParticleSystem>() != null;
        }

        private bool TransferParticleSystems(Transform sourceTransform)
        {
            bool success = true;

            var targetGo = GetOrCreateTargetObject(sourceTransform, TargetRoot);
            if (targetGo == null)
            {
                YuebyLogger.LogWarning("ParticleSystemTransferPlugin", $"无法为目标粒子系统创建对象: {sourceTransform.name}");
                return false;
            }

            try
            {
                // 主粒子系统
                if (sourceTransform.TryGetComponent<ParticleSystem>(out var sourcePS))
                {
                    var particleSystem = CopyComponentWithUndo<ParticleSystem>(sourceTransform.gameObject, targetGo);
                    if (particleSystem != null)
                    {
                        // 转移 ParticleSystemRenderer 组件
                        if (sourceTransform.TryGetComponent<ParticleSystemRenderer>(out var renderer))
                        {
                            var rendererComponent = CopyComponentWithUndo<ParticleSystemRenderer>(sourceTransform.gameObject, targetGo);
                            if (rendererComponent == null)
                            {
                                YuebyLogger.LogError("ParticleSystemTransferPlugin", $"复制渲染器组件失败: {sourceTransform.name}");
                            }
                        }
                    }
                    else
                    {
                        YuebyLogger.LogError("ParticleSystemTransferPlugin", $"复制粒子系统组件失败: {sourceTransform.name}");
                        success = false;
                    }
                }

                // 处理子粒子系统
                foreach (var childPS in sourceTransform.GetComponentsInChildren<ParticleSystem>(true))
                {
                    if (childPS.transform == sourceTransform.transform) continue;

                    var childTargetGo = GetOrCreateTargetObject(childPS.transform, TargetRoot);
                    if (childTargetGo == null)
                    {
                        YuebyLogger.LogWarning("ParticleSystemTransferPlugin", $"无法为目标子粒子系统创建对象: {childPS.name}");
                        success = false;
                        continue;
                    }

                    var childParticleSystem = CopyComponentWithUndo<ParticleSystem>(childPS.gameObject, childTargetGo);
                    if (childParticleSystem != null)
                    {
                        if (childPS.TryGetComponent<ParticleSystemRenderer>(out var childRenderer))
                        {
                            var childRendererComponent = CopyComponentWithUndo<ParticleSystemRenderer>(childPS.gameObject, childTargetGo);
                            if (childRendererComponent == null)
                            {
                                YuebyLogger.LogError("ParticleSystemTransferPlugin", $"复制子渲染器组件失败: {childPS.name}");
                            }
                        }
                    }
                    else
                    {
                        YuebyLogger.LogError("ParticleSystemTransferPlugin", $"复制子粒子系统组件失败: {childPS.name}");
                        success = false;
                    }
                }
            }
            catch (System.Exception e)
            {
                YuebyLogger.LogError("ParticleSystemTransferPlugin", $"转移粒子系统时发生错误 {sourceTransform.name}: {e.Message}");
                success = false;
            }

            return success;
        }
    }
} 
