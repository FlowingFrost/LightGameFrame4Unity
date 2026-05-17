using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectRecovery
{
    [InitializeOnLoad]
    public static class RecoveryHelper
    {
        private const string MenuPath = "Tools/Project Recovery";
        private const string SuppressKey = "ProjectRecovery_Suppress";

        private static string _pendingErrors;
        private static string _currentErrors;
        private static bool _waiting;

        private static bool _initialized;

        static RecoveryHelper()
        {
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompiled;
            EditorApplication.update += WaitForEditorReady;
        }

        private static void WaitForEditorReady()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            EditorApplication.update -= WaitForEditorReady;
            if (_initialized) return;
            _initialized = true;

            bool suppress = SessionState.GetBool(SuppressKey, false);
            Debug.Log($"[ProjectRecovery] WaitForEditorReady: suppress={suppress}");
            if (!suppress)
                ShowWindow();
        }

        private static void OnAssemblyCompiled(string assemblyPath, CompilerMessage[] messages)
        {
            if (SessionState.GetBool(SuppressKey, false))
                return;

            foreach (var msg in messages)
            {
                if (msg.type == CompilerMessageType.Error)
                {
                    _pendingErrors = FormatErrors(messages);
                    if (!_waiting)
                    {
                        _waiting = true;
                        EditorApplication.update += WaitForIdle;
                    }
                    return;
                }
            }

        }

        private static void WaitForIdle()
        {
            if (EditorApplication.isCompiling)
                return;

            EditorApplication.update -= WaitForIdle;
            _waiting = false;

            if (!string.IsNullOrEmpty(_pendingErrors))
            {
                string errors = _pendingErrors;
                _pendingErrors = null;
                _currentErrors = errors;
                ShowErrorPanel(errors);
            }
        }

        private static string FormatErrors(CompilerMessage[] messages)
        {
            var sb = new StringBuilder();
            int i = 0;
            foreach (var msg in messages)
            {
                if (msg.type != CompilerMessageType.Error) continue;
                i++;
                sb.AppendLine($"[{i}] {msg.message}");
                if (!string.IsNullOrEmpty(msg.file))
                    sb.AppendLine($"    文件: {msg.file} (行 {msg.line})");
                sb.AppendLine();
            }
            return sb.ToString();
        }

        // ========== 窗口管理 ==========

        [MenuItem(MenuPath)]
        public static void ShowWindow()
        {
            Debug.Log("[ProjectRecovery] ShowWindow called");
            var existing = FindExistingWindow();
            if (existing != null)
            {
                existing.Focus();
                TryMaximize(existing, 0);
                return;
            }

            var window = EditorWindow.CreateWindow<RecoveryHelperWindow>(typeof(SceneView));
            window.titleContent = new GUIContent("Project Recovery");
            window.Show();
        }

        private static void ShowErrorPanel(string errors)
        {
            var window = FindExistingWindow();
            if (window == null)
            {
                ShowWindow();
                window = FindExistingWindow();
            }
            window?.ShowError(errors);
            window?.Focus();
            TryMaximize(window, 0);
        }

        private static RecoveryHelperWindow FindExistingWindow()
        {
            var windows = Resources.FindObjectsOfTypeAll<RecoveryHelperWindow>();
            return windows.Length > 0 ? windows[0] : null;
        }

        private static void TryMaximize(EditorWindow window, int attempt)
        {
            if (window == null) return;
            window.maximized = true;
            if (attempt < 3)
                EditorApplication.delayCall += () => TryMaximize(window, attempt + 1);
        }

        // ========== 分析逻辑 ==========

        private static readonly (string label, string path, string hint)[] KnownPlugins =
        {
            ("DOTween", "Assets/Plugins/DOTween",
                "请从 Asset Store 或 http://dotween.demigiant.com 下载并放入 Assets/Plugins/DOTween/"),
            ("Odin Inspector", "Assets/Plugins/Sirenix",
                "请从 Asset Store 或 https://odininspector.com 下载并放入 Assets/Plugins/Sirenix/"),
        };

        private static readonly (string label, string path)[] CoreDirs =
        {
            ("LightGameFrame 框架", "Assets/LightGameFrame"),
            ("MusicTogether 游戏代码", "Assets/MusicTogether"),
        };

        private static readonly Dictionary<string, string> NsToPath = new Dictionary<string, string>
        {
            { "DG.Tweening", "Assets/Plugins/DOTween" },
            { "Sirenix", "Assets/Plugins/Sirenix" },
            { "TMPro", null },
            { "Unity.VisualScripting", null },
            { "UnityEditor", null },
            { "UnityEngine", null },
            { "System", null },
            { "JetBrains", null },
        };

        private static string GetProjectRoot()
        {
            return Path.GetDirectoryName(Application.dataPath);
        }

        internal static string RunAnalysis()
        {
            var sb = new StringBuilder();
            string root = GetProjectRoot();

            sb.AppendLine("=== 缺失插件 ===");
            bool anyMissing = false;
            foreach (var (label, path, hint) in KnownPlugins)
            {
                if (!Directory.Exists(Path.Combine(root, path)))
                {
                    sb.AppendLine($"  X {label} - 未安装");
                    sb.AppendLine($"    {hint}");
                    sb.AppendLine();
                    anyMissing = true;
                }
            }
            if (!anyMissing) sb.AppendLine("  OK 所有已知插件均已安装");
            sb.AppendLine();

            sb.AppendLine("=== 代码完整性 ===");
            bool allOk = true;
            foreach (var (label, path) in CoreDirs)
            {
                if (!Directory.Exists(Path.Combine(root, path)))
                {
                    sb.AppendLine($"  X {label} - 目录缺失 ({path})");
                    allOk = false;
                }
            }
            if (allOk) sb.AppendLine("  OK 所有核心目录正常");
            sb.AppendLine();

            sb.AppendLine("=== 命名空间检查 ===");
            var namespaces = CollectNamespaces();
            var checked_ = new HashSet<string>();
            bool allNsOk = true;

            foreach (var ns in namespaces.OrderBy(n => n))
            {
                string matchKey = null;
                string matchPath = null;
                foreach (var kv in NsToPath)
                {
                    if ((ns == kv.Key || ns.StartsWith(kv.Key + ".")) &&
                        (matchKey == null || kv.Key.Length > matchKey.Length))
                    {
                        matchKey = kv.Key;
                        matchPath = kv.Value;
                    }
                }

                if (matchKey == null)
                {
                    if (checked_.Add(ns))
                    {
                        string local = FindLocalNsPath(ns);
                        if (local != null && !Directory.Exists(Path.Combine(root, local)))
                        {
                            sb.AppendLine($"  X {ns} - 项目代码缺失 ({local})");
                            allNsOk = false;
                        }
                    }
                    continue;
                }

                if (checked_.Add(matchKey))
                {
                    if (matchPath == null)
                        sb.AppendLine($"  OK {matchKey} - Unity 包管理器管理");
                    else if (Directory.Exists(Path.Combine(root, matchPath)))
                        sb.AppendLine($"  OK {matchKey} - 已就绪");
                    else
                    {
                        sb.AppendLine($"  X {matchKey} - 插件缺失 ({matchPath})");
                        allNsOk = false;
                    }
                }
            }
            if (allNsOk && checked_.Count > 0)
                sb.AppendLine("\n  所有引用的命名空间均可正常解析。");

            return sb.ToString();
        }

        private static string FindLocalNsPath(string ns)
        {
            if (ns.StartsWith("LightGameFrame")) return "Assets/LightGameFrame";
            if (ns.StartsWith("MusicTogether")) return "Assets/MusicTogether";
            return null;
        }

        private static HashSet<string> CollectNamespaces()
        {
            var result = new HashSet<string>();
            foreach (var f in Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories))
            {
                if (f.Contains(Path.Combine("Assets", "Plugins", "ProjectRecovery"))) continue;
                foreach (var line in File.ReadLines(f))
                {
                    string t = line.Trim();
                    if (!t.StartsWith("using ") || !t.EndsWith(";")) continue;
                    string ns = t.Substring(6, t.Length - 7).Trim();
                    if (string.IsNullOrEmpty(ns) || (ns.Contains(" ") && !ns.Contains("."))) continue;
                    result.Add(ns.Split('.')[0]);
                }
            }
            return result;
        }

        // ========== 窗口 ==========

        public class RecoveryHelperWindow : EditorWindow
        {
            private VisualElement _panelNormal;
            private VisualElement _panelError;
            private Label _checkResult;
            private Label _errorDetail;

            public void CreateGUI()
            {
                var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                    "Assets/Plugins/ProjectRecovery/Editor/RecoveryWindow.uxml");
                if (uxml == null)
                {
                    EditorApplication.delayCall += () =>
                    {
                        if (this != null)
                        {
                            Close();
                            ShowWindow();
                        }
                    };
                    return;
                }
                uxml.CloneTree(rootVisualElement);

                var uss = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                    "Assets/Plugins/ProjectRecovery/Editor/RecoveryWindow.uss");
                if (uss != null) rootVisualElement.styleSheets.Add(uss);

                _panelNormal = rootVisualElement.Q<VisualElement>("panel-normal");
                _panelError = rootVisualElement.Q<VisualElement>("panel-error");
                _checkResult = rootVisualElement.Q<Label>("check-result");
                _errorDetail = rootVisualElement.Q<Label>("error-detail");

                var btnCheck = rootVisualElement.Q<Button>("btn-check");
                var btnDismissNormal = rootVisualElement.Q<Button>("btn-dismiss-normal");
                var btnAnalyze = rootVisualElement.Q<Button>("btn-analyze");
                var btnDismissError = rootVisualElement.Q<Button>("btn-dismiss-error");
                if (btnCheck != null) btnCheck.clicked += OnCheck;
                if (btnDismissNormal != null) btnDismissNormal.clicked += OnDismiss;
                if (btnAnalyze != null) btnAnalyze.clicked += OnAnalyze;
                if (btnDismissError != null) btnDismissError.clicked += OnDismiss;

                Debug.Log($"[ProjectRecovery] CreateGUI: scriptCompilationFailed={EditorUtility.scriptCompilationFailed}");
                if (EditorUtility.scriptCompilationFailed)
                    ShowError(_currentErrors ?? "");
                else
                    ShowNormal();

                TryMaximize(this, 0);
            }

            private void ShowNormal()
            {
                Debug.Log("[ProjectRecovery] ShowNormal");
                if (_panelNormal != null) _panelNormal.style.display = DisplayStyle.Flex;
                if (_panelError != null) _panelError.style.display = DisplayStyle.None;
            }

            public void ShowError(string errors)
            {
                Debug.Log($"[ProjectRecovery] ShowError called, errors='{errors}'");
                if (_panelNormal != null) _panelNormal.style.display = DisplayStyle.None;
                if (_panelError != null) _panelError.style.display = DisplayStyle.Flex;

                if (_errorDetail != null) _errorDetail.text = "[ 待分析 ]";
            }

            private void OnCheck()
            {
                if (_checkResult != null)
                    _checkResult.text = RunAnalysis();
            }

            private void OnAnalyze()
            {
                var sb = new StringBuilder();
                string errors = _currentErrors ?? "";
                if (!string.IsNullOrEmpty(errors))
                {
                    sb.AppendLine("=== 编译错误 ===");
                    sb.AppendLine(errors);
                }
                sb.Append(RunAnalysis());
                if (_errorDetail != null) _errorDetail.text = sb.ToString();
            }

            private void OnDismiss()
            {
                SessionState.SetBool(SuppressKey, true);
                Close();
            }
        }
    }
}
