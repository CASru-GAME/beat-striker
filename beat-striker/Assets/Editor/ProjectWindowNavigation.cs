#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Project ウィンドウのフォルダ表示を Finder のように「戻る / 進む」で辿れるようにする。
/// </summary>
[InitializeOnLoad]
internal static class ProjectWindowNavigation {
    const string MenuRoot = "Edit/Project/";

    static readonly Type ProjectBrowserType = typeof(Editor).Assembly.GetType("UnityEditor.ProjectBrowser");
    static readonly FieldInfo LastFoldersField =
        ProjectBrowserType?.GetField("m_LastFolders", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo SearchFieldTextField =
        ProjectBrowserType?.GetField("m_SearchFieldText", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly MethodInfo SetSearchStringMethod =
        ProjectBrowserType?.GetMethod("SetSearch", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(string) }, null);
    static readonly MethodInfo SetFolderSelectionMethod = ProjectBrowserType?.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
        .FirstOrDefault(m => m.Name == "SetFolderSelection" && m.GetParameters().Length == 2);

    static readonly Dictionary<int, NavigationState> LastState = new();
    static readonly Dictionary<int, Stack<NavigationState>> BackStacks = new();
    static readonly Dictionary<int, Stack<NavigationState>> ForwardStacks = new();
    static int? SuppressedBrowserId;

    readonly struct NavigationState : IEquatable<NavigationState> {
        public readonly string[] FolderPaths;
        public readonly string SearchText;

        public NavigationState(string[] folderPaths, string searchText) {
            FolderPaths = folderPaths ?? Array.Empty<string>();
            SearchText = searchText ?? "";
        }

        public bool Equals(NavigationState other) =>
            SearchText == other.SearchText &&
            FolderPaths.Length == other.FolderPaths.Length &&
            FolderPaths.SequenceEqual(other.FolderPaths);

        public override bool Equals(object obj) => obj is NavigationState other && Equals(other);

        public override int GetHashCode() {
            unchecked {
                int h = SearchText?.GetHashCode() ?? 0;
                if (FolderPaths != null) {
                    foreach (var p in FolderPaths) {
                        h = (h * 397) ^ (p?.GetHashCode() ?? 0);
                    }
                }
                return h;
            }
        }
    }

    static ProjectWindowNavigation() {
        if (ProjectBrowserType == null || LastFoldersField == null || SearchFieldTextField == null ||
            SetSearchStringMethod == null || SetFolderSelectionMethod == null) {
            return;
        }
        EditorApplication.update += OnEditorUpdate;
    }

    static void OnEditorUpdate() {
        if (ProjectBrowserType == null) {
            return;
        }
        foreach (var browser in GetAllProjectBrowsers()) {
            int id = browser.GetInstanceID();
            var state = CaptureState(browser);
            if (SuppressedBrowserId == id) {
                LastState[id] = state;
                continue;
            }
            if (!LastState.TryGetValue(id, out var prev)) {
                LastState[id] = state;
                continue;
            }
            if (prev.Equals(state)) {
                continue;
            }
            if (!BackStacks.TryGetValue(id, out var back)) {
                back = new Stack<NavigationState>();
                BackStacks[id] = back;
            }
            back.Push(prev);
            if (ForwardStacks.TryGetValue(id, out var fwd)) {
                fwd.Clear();
            }
            LastState[id] = state;
        }
    }

    [MenuItem(MenuRoot + "前に戻る %[", false, 10)]
    static void NavigateBack() {
        var browser = GetActiveProjectBrowser();
        if (browser == null) {
            return;
        }
        int id = browser.GetInstanceID();
        if (!BackStacks.TryGetValue(id, out var back) || back.Count == 0) {
            return;
        }
        var current = CaptureState(browser);
        var target = back.Pop();
        if (!ForwardStacks.TryGetValue(id, out var fwd)) {
            fwd = new Stack<NavigationState>();
            ForwardStacks[id] = fwd;
        }
        fwd.Push(current);
        SuppressedBrowserId = id;
        ApplyState(browser, target);
        var releaseId = id;
        EditorApplication.delayCall += () => {
            if (SuppressedBrowserId == releaseId) {
                SuppressedBrowserId = null;
            }
        };
    }

    [MenuItem(MenuRoot + "前に戻る %[", true)]
    static bool NavigateBackValidate() {
        var browser = GetActiveProjectBrowser();
        return browser != null && BackStacks.TryGetValue(browser.GetInstanceID(), out var back) && back.Count > 0;
    }

    [MenuItem(MenuRoot + "次に進む %]", false, 11)]
    static void NavigateForward() {
        var browser = GetActiveProjectBrowser();
        if (browser == null) {
            return;
        }
        int id = browser.GetInstanceID();
        if (!ForwardStacks.TryGetValue(id, out var fwd) || fwd.Count == 0) {
            return;
        }
        var current = CaptureState(browser);
        var target = fwd.Pop();
        if (!BackStacks.TryGetValue(id, out var back)) {
            back = new Stack<NavigationState>();
            BackStacks[id] = back;
        }
        back.Push(current);
        SuppressedBrowserId = id;
        ApplyState(browser, target);
        var releaseId = id;
        EditorApplication.delayCall += () => {
            if (SuppressedBrowserId == releaseId) {
                SuppressedBrowserId = null;
            }
        };
    }

    [MenuItem(MenuRoot + "次に進む %]", true)]
    static bool NavigateForwardValidate() {
        var browser = GetActiveProjectBrowser();
        return browser != null && ForwardStacks.TryGetValue(browser.GetInstanceID(), out var fwd) && fwd.Count > 0;
    }

    static IEnumerable<EditorWindow> GetAllProjectBrowsers() {
        if (ProjectBrowserType == null) {
            yield break;
        }
        foreach (var o in Resources.FindObjectsOfTypeAll(ProjectBrowserType)) {
            if (o is EditorWindow w) {
                yield return w;
            }
        }
    }

    static EditorWindow GetActiveProjectBrowser() {
        if (ProjectBrowserType == null) {
            return null;
        }
        if (EditorWindow.focusedWindow != null && EditorWindow.focusedWindow.GetType() == ProjectBrowserType) {
            return EditorWindow.focusedWindow;
        }
        foreach (var w in GetAllProjectBrowsers()) {
            return w;
        }
        return null;
    }

    static NavigationState CaptureState(EditorWindow browser) {
        var folders = (string[])LastFoldersField.GetValue(browser) ?? Array.Empty<string>();
        var search = (string)SearchFieldTextField.GetValue(browser) ?? "";
        return new NavigationState((string[])folders.Clone(), search);
    }

    static void ApplyState(EditorWindow browser, NavigationState state) {
        SetSearchStringMethod.Invoke(browser, new object[] { state.SearchText });
        var ids = PathsToFolderInstanceIds(state.FolderPaths);
        SetFolderSelectionMethod.Invoke(browser, new object[] { ids, false });
        browser.Repaint();
    }

    static int[] PathsToFolderInstanceIds(string[] paths) {
        if (paths == null || paths.Length == 0) {
            var assetsRoot = AssetDatabase.LoadAssetAtPath<Object>("Assets");
            return assetsRoot != null ? new[] { assetsRoot.GetInstanceID() } : Array.Empty<int>();
        }
        var ids = new List<int>(paths.Length);
        foreach (var path in paths) {
            if (string.IsNullOrEmpty(path)) {
                continue;
            }
            var o = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (o != null) {
                ids.Add(o.GetInstanceID());
            }
        }
        return ids.Count > 0 ? ids.ToArray() : PathsToFolderInstanceIds(Array.Empty<string>());
    }
}
#endif
