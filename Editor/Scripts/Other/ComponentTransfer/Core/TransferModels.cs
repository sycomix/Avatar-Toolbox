using System.Collections.Generic;
using System.Linq;

namespace YuebyAvatarTools.ComponentTransfer.Editor
{
    /// <summary>
    /// 转移配置
    /// </summary>
    public class TransferConfig
    {
        /// <summary>
        /// 启用的插件名称列表（null 表示全部启用）
        /// </summary>
        public List<string> EnabledPlugins { get; set; } = null;
        
        /// <summary>
        /// 是否启用智能骨骼映射
        /// </summary>
        public bool EnableSmartBoneMapping { get; set; } = true;
        
        /// <summary>
        /// 每个插件的自定义配置
        /// </summary>
        public Dictionary<string, object> PluginConfigs { get; set; } = new Dictionary<string, object>();
        
        /// <summary>
        /// 从现有插件列表创建配置
        /// </summary>
        public static TransferConfig FromPlugins(IEnumerable<IComponentTransferPlugin> plugins)
        {
            var config = new TransferConfig();
            config.EnabledPlugins = plugins.Where(p => p.IsEnabled).Select(p => p.Name).ToList();
            return config;
        }
    }
    
    /// <summary>
    /// 转移进度数据
    /// </summary>
    public class TransferProgress
    {
        /// <summary>
        /// 总体进度 (0-1)
        /// </summary>
        public float Progress { get; set; }
        
        /// <summary>
        /// 进度消息
        /// </summary>
        public string Message { get; set; }
        
        /// <summary>
        /// 当前处理的目标对象名称
        /// </summary>
        public string CurrentTarget { get; set; }
        
        /// <summary>
        /// 当前执行的插件名称
        /// </summary>
        public string CurrentPlugin { get; set; }
        
        /// <summary>
        /// 已处理的步骤数
        /// </summary>
        public int ProcessedSteps { get; set; }
        
        /// <summary>
        /// 总步骤数
        /// </summary>
        public int TotalSteps { get; set; }
    }
}

