using UnityEditor;
using UnityEngine;
using DandyDino.Modulate.Bootstrap;

namespace DandyDino.Modulate
{
    public class ModulateDependencyInstallerEditorWindow : EditorWindow
    {
        [InitializeOnLoadMethod]
        public static void InitializeOnLoadMethod()
        {
            bool hasLaunched = EditorPrefs.GetBool(HAS_LAUNCHED);
            
            if (hasLaunched)
            {
                return;
            }
            EditorPrefs.SetBool(HAS_LAUNCHED, true);
            
            ShowWindow();
        }
        
        private static ModulateDependencyInstallerEditorWindow _window;
        private static Texture _banner;
        
        private static Texture2D _flatButtonTex;
        private static Texture2D _flatButtonHoverTex;
        private static GUIStyle _flatButtonStyle;
        
        private static readonly string HAS_LAUNCHED = "ModulateInstallerLaunched";
        
        private bool _r3InstallQueued;
        private bool _selfUninstallTriggered;

        [MenuItem("Modulate! Installer/Install Modulate! Dependencies")]
        public static void ShowWindow()
        {
            _window = GetWindow<ModulateDependencyInstallerEditorWindow>();
            _window.titleContent = new GUIContent("Install Modulate!");
            _window.minSize = new Vector2(600, 400);
            
            _banner = Resources.Load<Texture>("ModulateInstaller/Banner");
            _window.Show();
            
        }

        private void OnEnable()
        {
            // Reload banner — static fields can be reset by domain reload while the window persists
            if (_banner == null)
            {
                _banner = Resources.Load<Texture>("ModulateInstaller/Banner");
            }
        }

        private void OnGUI()
        {
            if (_banner != null)
            {
                Rect rect = GUILayoutUtility.GetRect(0, 100, GUILayout.ExpandWidth(true));
                GUI.DrawTexture(rect, _banner, ScaleMode.ScaleAndCrop);
            }
            
            GUILayout.Space(10);
            
            GUIStyle centeredBoldLabel = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 16 };
            GUIStyle leftBoldLabel = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleLeft, fontSize = 12 };
            EditorGUILayout.LabelField("Modulate Installer", centeredBoldLabel, GUILayout.Height(20));

            if (ModulateDependencyInstaller.IsModulateInstalled())
            {
                if (!_selfUninstallTriggered)
                {
                    _selfUninstallTriggered = true;
                    // Defer to next editor tick so we don't mutate state mid-OnGUI
                    EditorApplication.delayCall += () =>
                    {
                        ModulateDependencyInstaller.UninstallSelf();
                    };
                }
                EditorGUILayout.HelpBox("Modulate is already installed. Removing installer…", MessageType.Info);
                return;
            }
            
            EditorGUILayout.HelpBox("This window will install all the dependencies for Modulate!. Please don't close this window until the installation is done.", MessageType.Info);
            
            DrawHorizontalLine();
            
            GUILayout.Space(10);

            bool nugetInManifest = ModulateDependencyInstaller.IsNuGetForUnityInManifest();
            bool nugetLoaded     = ModulateDependencyInstaller.IsNuGetForUnityLoaded();
            bool r3Installed     = ModulateDependencyInstaller.IsR3NuGetInstalled();
            bool remainingDone   = ModulateDependencyInstaller.AreRemainingDependenciesInstalled();
            
            GUIStyle bodyText = new GUIStyle(EditorStyles.label)
            {
                wordWrap = true,
                richText = true,
                alignment = TextAnchor.UpperLeft,
                fontSize = 12,
                padding = new RectOffset(4, 4, 4, 4)
            };

            // Step 1 — NuGet for Unity
            if (!nugetInManifest)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Install NuGet for Unity", leftBoldLabel);
                if (GUILayout.Button("Install NuGet for Unity", FlatButtonStyle, GUILayout.Width(180)))
                {
                    ModulateDependencyInstaller.InstallNuGetForUnity();
                }
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(15);
                DrawHorizontalLine();
                
                GUILayout.Space(15);
                
                EditorGUILayout.LabelField("NuGet for Unity will be installed so we can properly setup R3, one of Modulate's dependencies. As this includes a few steps, this installer is aimed to automate the installation process.", bodyText);
                return;
            }

            // NuGet is in manifest but not yet compiled in
            if (!nugetLoaded)
            {
                EditorGUILayout.HelpBox("NuGetForUnity is installing… wait for the editor to finish reloading. Please don't close this window.", MessageType.Info);
                return;
            }

            // Step intermediate — R3 from NuGet (auto)
            if (!r3Installed)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Installing R3 from NuGet…");
                EditorGUILayout.EndHorizontal();

                // Kick off automatically once we land here — but only ONCE.
                if (!_r3InstallQueued)
                {
                    _r3InstallQueued = true;
                    EditorApplication.delayCall += ModulateDependencyInstaller.InstallR3FromNuGet;
                }
                return;
            }

            // Step 2 — Everything else
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(remainingDone ? "Installation complete" : "Finish installation", leftBoldLabel);
            using (new EditorGUI.DisabledScope(remainingDone))
            {
                if (GUILayout.Button(remainingDone ? "Done" : "Finish installation", FlatButtonStyle, GUILayout.Width(180)))
                {
                    ModulateDependencyInstaller.InstallRemainingDependencies();
                }
            }
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(15);
            DrawHorizontalLine();
            GUILayout.Space(15);

            string[] packages = new[]
            {
                "com.gustavopsantos.reflex",
                "com.dandydino.elements",
                "com.cysharp.unitask",
                "com.cysharp.r3",
                "com.dandydino.modulate"
            };
            
            string[] descriptions = new[]
            {
                "Used for Dependency Injection",
                "Used for Custom Editors on Modulate!",
                "Useful very performance library for async code",
                "Reactive programming for C# - integrated with Modulate's Event Bus",
                "Modulate! itself. Crate modular projects for Unity easily"
            };
            for (int i = 0; i < 5; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(packages[i], leftBoldLabel);
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField(descriptions[i], bodyText);
                EditorGUILayout.EndHorizontal();
            }
        }

        public static void CloseWindow()
        {
            if (_window != null)
            {
                _window.Close();
                _window = null;
            }
        }

        private void OnInspectorUpdate()
        {
            Repaint();
        }
        
        public static void DrawHorizontalLine(float height = 1f, float verticalSpacing = 4f)
        {
            GUILayout.Space(verticalSpacing);
            Rect rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.Height(height));
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.5f));
            GUILayout.Space(verticalSpacing);
        }
        
        private static Texture2D MakeTex(Color color)
        {
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, color);
            tex.hideFlags = HideFlags.HideAndDontSave;
            tex.Apply();
            return tex;
        }
        
        private static GUIStyle FlatButtonStyle
        {
            get
            {
                if (_flatButtonStyle == null || _flatButtonTex == null)
                {
                    _flatButtonTex = MakeTex(new Color(0.48f, 0.66f, 0.18f));
                    _flatButtonHoverTex = MakeTex(new Color(0.52f, 0.62f, 0.04f));

                    _flatButtonStyle = new GUIStyle(GUI.skin.button)
                    {
                        normal    = { background = _flatButtonTex,      textColor = Color.white },
                        hover     = { background = _flatButtonHoverTex, textColor = Color.white },
                        active    = { background = _flatButtonTex,      textColor = Color.white },
                        focused   = { background = _flatButtonTex,      textColor = Color.white },
                        fontStyle = FontStyle.Bold,
                        border    = new RectOffset(0, 0, 0, 0),
                        padding   = new RectOffset(10, 10, 8, 8),
                        alignment = TextAnchor.MiddleCenter
                    };
                }
                return _flatButtonStyle;
            }
        }
    }
}