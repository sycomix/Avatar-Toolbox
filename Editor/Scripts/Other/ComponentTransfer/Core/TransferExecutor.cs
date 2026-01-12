using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Yueby.Core.Utils;

namespace YuebyAvatarTools.ComponentTransfer.Editor
{
    internal class TransferExecutor
    {
        private List<IComponentTransferPlugin> _plugins;
        
        public TransferExecutor()
        {
            InitializePlugins();
        }
        
        private void InitializePlugins()
        {
            _plugins = new List<IComponentTransferPlugin>
            {
                new Plugins.PhysBoneTransferPlugin(),
                new Plugins.MaterialTransferPlugin(),
                new Plugins.BlendShapeTransferPlugin(),
                new Plugins.ConstraintTransferPlugin(),
                new Plugins.ParticleSystemTransferPlugin(),
                new Plugins.ActiveStateTransferPlugin(),
                new Plugins.AnimatorTransferPlugin(),
                new Plugins.ModularAvatarTransferPlugin()
            };
        }
        
        public async Task<bool> ExecuteAsync(
            Transform sourceRoot,
            Transform[] targetRoots,
            TransferConfig config,
            IProgress<TransferProgress> progress,
            CancellationToken cancellationToken)
        {
            // 应用配置
            ApplyConfig(config);
            
            // 清空跨目标决策缓存
            ComponentTransferBase.ClearCrossTargetDecisions();
            
            // 重置所有插件
            ResetPlugins();
            
            var enabledPlugins = _plugins.Where(p => p.IsEnabled).ToList();
            
            // 计算总步骤数
            int totalSteps = CalculateTotalSteps(sourceRoot, targetRoots, enabledPlugins);
            int currentStep = 0;
            
            bool overallSuccess = true;
            
            foreach (var target in targetRoots)
            {
                if (target == null) continue;
                
                currentStep++;
                
                // 报告进度（每个目标对象一次）
                progress?.Report(new TransferProgress
                {
                    Progress = (float)currentStep / totalSteps,
                    Message = $"正在转移: {target.name}",
                    CurrentTarget = target.name,
                    CurrentPlugin = "",
                    ProcessedSteps = currentStep,
                    TotalSteps = totalSteps
                });
                
                await Task.Yield(); // 让出控制权，更新UI
                
                foreach (var plugin in enabledPlugins)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    if (!plugin.CanTransfer(sourceRoot, target))
                        continue;
                    
                    try
                    {
                        bool success = plugin.ExecuteTransfer(sourceRoot, target);
                        
                        if (!success)
                            overallSuccess = false;
                        
                        // 检查停止标志
                        if (plugin is ComponentTransferBase basePlugin && basePlugin.ShouldStopTransfer())
                        {
                            YuebyLogger.LogWarning("ComponentTransfer", "用户已停止转移流程");
                            return false;
                        }
                    }
                    catch (Exception e)
                    {
                        YuebyLogger.LogError("ComponentTransfer", $"插件 {plugin.Name} 执行时发生错误: {e.Message}");
                        overallSuccess = false;
                    }
                }
            }
            
            // 最终进度
            progress?.Report(new TransferProgress
            {
                Progress = 1f,
                Message = "转移完成",
                ProcessedSteps = totalSteps,
                TotalSteps = totalSteps
            });
            
            return overallSuccess;
        }
        
        private void ApplyConfig(TransferConfig config)
        {
            if (config == null) return;
            
            if (config.EnabledPlugins != null)
            {
                foreach (var plugin in _plugins)
                {
                    plugin.IsEnabled = config.EnabledPlugins.Contains(plugin.Name);
                }
            }
        }
        
        private void ResetPlugins()
        {
            foreach (var plugin in _plugins)
            {
                if (plugin is ComponentTransferBase basePlugin)
                {
                    basePlugin.ResetStopFlag();
                }
            }
            BoneMappingRecommendationWindow.ResetGlobalStopFlag();
        }
        
        private int CalculateTotalSteps(Transform sourceRoot, Transform[] targetRoots, List<IComponentTransferPlugin> enabledPlugins)
        {
            // 每个目标对象算一步
            return targetRoots.Count(t => t != null);
        }
    }
}

