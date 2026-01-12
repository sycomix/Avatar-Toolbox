using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace YuebyAvatarTools.ComponentTransfer.Editor
{
    /// <summary>
    /// 组件转移公共 API
    /// </summary>
    public static class ComponentTransfer
    {
        /// <summary>
        /// 执行组件转移
        /// </summary>
        /// <param name="sourceRoot">源根对象</param>
        /// <param name="targetRoots">目标根对象数组</param>
        /// <param name="config">转移配置（可选）</param>
        /// <param name="progress">进度回调（可选）</param>
        /// <param name="cancellationToken">取消令牌（可选）</param>
        /// <returns>转移是否成功</returns>
        /// <example>
        /// // 基础使用
        /// bool success = await ComponentTransfer.Transfer(sourceRoot, new[] { target1, target2 });
        /// 
        /// // 带配置和进度
        /// var config = new TransferConfig { EnableSmartBoneMapping = true };
        /// var progress = new Progress&lt;TransferProgress&gt;(p => Debug.Log($"{p.Progress:P0}: {p.Message}"));
        /// await ComponentTransfer.Transfer(sourceRoot, targets, config, progress);
        /// </example>
        public static async Task<bool> Transfer(
            Transform sourceRoot,
            Transform[] targetRoots,
            TransferConfig config = null,
            IProgress<TransferProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            if (sourceRoot == null || targetRoots == null || targetRoots.Length == 0)
                return false;
            
            var executor = new TransferExecutor();
            return await executor.ExecuteAsync(sourceRoot, targetRoots, config, progress, cancellationToken);
        }
    }
}

