using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Yueby.Core.Utils;
using Yueby.Utils;

namespace YuebyAvatarTools.ComponentTransfer.Editor
{
    public class ComponentTransferWindow : EditorWindow
    {
        private Vector2 _scrollPos;
        private Transform _sourceRoot;
        private List<IComponentTransferPlugin> _plugins = new List<IComponentTransferPlugin>();
        private bool _showTargetList = false; // 控制目标对象列表显示

        // 进度相关
        private bool _isTransferring = false;
        private float _currentProgress = 0f;
        private string _currentProgressText = "";

        private void OnEnable()
        {
            InitializePlugins();
        }

        private void InitializePlugins()
        {
            _plugins.Clear();

            // 添加所有插件
            _plugins.Add(new Plugins.PhysBoneTransferPlugin());
            _plugins.Add(new Plugins.MaterialTransferPlugin());
            _plugins.Add(new Plugins.BlendShapeTransferPlugin());
            _plugins.Add(new Plugins.ConstraintTransferPlugin());
            _plugins.Add(new Plugins.ParticleSystemTransferPlugin());
            _plugins.Add(new Plugins.ActiveStateTransferPlugin());
            _plugins.Add(new Plugins.AnimatorTransferPlugin());
            _plugins.Add(new Plugins.ModularAvatarTransferPlugin());
        }

        [MenuItem("Tools/YuebyTools/VRChat/Avatar/Component Transfer", false, 11)]
        public static void OpenWindow()
        {
            var window = CreateWindow<ComponentTransferWindow>();
            window.titleContent = new GUIContent("Component Transfer");
            window.minSize = new Vector2(500, 600);
            window.Show();
        }

        private void OnGUI()
        {
            var selections = Selection.gameObjects;
            var isSelectedObject = selections.Length > 0;
            var enabledPlugins = _plugins.Where(p => p.IsEnabled).ToList();

            EditorGUILayout.BeginVertical();

            // 标题
            EditorUI.DrawEditorTitle("组件转移工具");

            // 可滚动内容区域（除了底部按钮和进度条）
            var bottomHeight = 100f; // 底部区域高度
            var scrollViewHeight = position.height - bottomHeight - 35; // 35是标题高度

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.Height(scrollViewHeight));
            {
                // 源对象选择
                EditorUI.VerticalEGLTitled("源对象", () =>
                {
                    _sourceRoot = (Transform)EditorGUILayout.ObjectField("源根对象", _sourceRoot, typeof(Transform), true);
                });

            // 插件设置
            EditorUI.VerticalEGLTitled("转移插件", () =>
            {
                for (int i = 0; i < _plugins.Count; i++)
                {
                    var plugin = _plugins[i];

                    // 标题行：Toggle + 插件名（带 Tooltip）
                    EditorUI.HorizontalEGL(() =>
                    {
                        plugin.IsEnabled = EditorGUILayout.Toggle(plugin.IsEnabled, GUILayout.Width(20));
                            var labelContent = new GUIContent(plugin.Name, plugin.Description);
                            EditorGUILayout.LabelField(labelContent, EditorStyles.boldLabel);
                        });

                    if (plugin.IsEnabled)
                            {
                        EditorGUI.indentLevel++;
                        plugin.DrawSettings();
                        EditorGUI.indentLevel--;
                    }

                    // 插件之间添加分隔线（最后一个除外）
                    if (i < _plugins.Count - 1)
                    {
                        EditorGUILayout.Space(2);
                        EditorUI.Line(LineType.Horizontal);
                        EditorGUILayout.Space(2);
                    }
                }
            });

                // 目标对象信息
                EditorUI.VerticalEGLTitled("目标对象", () =>
                {
                    EditorGUILayout.LabelField($"已选择: {selections.Length} 个对象", EditorStyles.miniLabel);
                    EditorGUILayout.LabelField($"启用插件: {enabledPlugins.Count} 个", EditorStyles.miniLabel);

                    // 目标对象列表（可折叠）
                    if (isSelectedObject)
                    {
                        EditorUI.Line(LineType.Horizontal);
                        _showTargetList = EditorGUILayout.Foldout(_showTargetList, "目标对象列表");
                        if (_showTargetList)
                        {
                            for (int i = 0; i < selections.Length; i++)
                            {
                                EditorUI.HorizontalEGL(() =>
                                {
                                    EditorGUILayout.LabelField($"{i + 1}.", GUILayout.Width(25));
                                    EditorGUILayout.ObjectField(selections[i], typeof(GameObject), true);
                                });
                            }
                        }
                    }
                    else
                    {
                        EditorUI.Line(LineType.Horizontal);
                        EditorGUILayout.HelpBox("请在场景中选择目标对象", MessageType.Warning);
                    }
                });
            }
            EditorGUILayout.EndScrollView();

            // 底部固定区域：分隔线
            EditorUI.Line(LineType.Horizontal);

            // 底部固定区域：按钮和进度条
            EditorGUILayout.BeginVertical(GUILayout.Height(bottomHeight));
            {
                // 开始转移按钮
                EditorGUI.BeginDisabledGroup(_isTransferring);
                if (GUILayout.Button(_isTransferring ? "转移中..." : "开始转移", GUILayout.Height(35)))
                {
                    if (_sourceRoot == null)
                    {
                        EditorUtility.DisplayDialog("错误", "请先选择源根对象", "确定");
                    }
                    else if (!isSelectedObject)
                    {
                        EditorUtility.DisplayDialog("错误", "请在场景中选择目标对象", "确定");
                    }
                    else
                    {
                        ExecuteTransfer(selections);
                    }
                }
                EditorGUI.EndDisabledGroup();

                // 进度条
                if (_isTransferring)
                {
                    EditorGUILayout.Space(5);
                    EditorGUILayout.LabelField(_currentProgressText, EditorStyles.miniLabel);
                    var progressRect = EditorGUILayout.GetControlRect(false, 20);
                    EditorGUI.ProgressBar(progressRect, _currentProgress, $"{(_currentProgress * 100):F0}%");
                }
                else
                {
                    GUILayout.FlexibleSpace();
                }
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndVertical();
        }

        private async void ExecuteTransfer(GameObject[] targets)
        {
            _isTransferring = true;
            _currentProgress = 0f;
            _currentProgressText = "准备中...";
            Repaint();

            var config = TransferConfig.FromPlugins(_plugins);

            var progressReporter = new Progress<TransferProgress>(p =>
            {
                _currentProgress = p.Progress;
                _currentProgressText = p.Message;
                Repaint();
            });

            try
            {
                bool success = await ComponentTransfer.Transfer(
                    _sourceRoot,
                    targets.Select(s => s.transform).ToArray(),
                    config,
                    progressReporter
                );

                if (success)
                {
                    YuebyLogger.LogInfo("ComponentTransfer", "转移完成");
                }
                else
                {
                    YuebyLogger.LogWarning("ComponentTransfer", "转移过程中发生了一些错误");
                }
            }
            catch (OperationCanceledException)
            {
                YuebyLogger.LogWarning("ComponentTransfer", "转移已被用户停止");
            }
            catch (Exception e)
            {
                YuebyLogger.LogError("ComponentTransfer", $"转移过程中发生错误: {e.Message}");
            }
            finally
            {
                _isTransferring = false;
                _currentProgress = 0f;
                _currentProgressText = "";
                Repaint();
            }
        }
    }
}
