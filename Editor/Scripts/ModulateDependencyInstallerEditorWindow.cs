using System.Linq;
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
            ShowWindow();
        }
        
        // THIS CLASS ONLY CALLS MODULATE DEPENDENCY, DOES NOT PERFORM ANYTHING, ONLY CALLS
        // ORDER: NUGET, THEN INSTALL R3 AUTOMATICALLY IF NOT INSTALLED. FINALLY, INSTALL EVERYTHING ELSE.
        private static ModulateDependencyInstallerEditorWindow _window;

        [MenuItem("Modulate! Installer/Install Modulate! Dependencies")]
        public static void ShowWindow()
        {
            _window = GetWindow<ModulateDependencyInstallerEditorWindow>();
            _window.titleContent = new GUIContent("ModulateDependencyInstallerEditorWindow");
            _window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Modulate Installer");
            GUILayout.Space(10);

            bool nugetInManifest = ModulateDependencyInstaller.IsNuGetForUnityInManifest();
            bool nugetLoaded     = ModulateDependencyInstaller.IsNuGetForUnityLoaded();
            bool r3Installed     = ModulateDependencyInstaller.IsR3NuGetInstalled();
            bool remainingDone   = ModulateDependencyInstaller.AreRemainingDependenciesInstalled();

            // Step 1 — NuGet for Unity
            if (!nugetInManifest)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("1 - Install NuGet for Unity");
                if (GUILayout.Button("Install NuGet for Unity", GUILayout.Width(180)))
                {
                    ModulateDependencyInstaller.InstallNuGetForUnity();
                }
                EditorGUILayout.EndHorizontal();
                return;
            }

            // NuGet is in manifest but not yet compiled in
            if (!nugetLoaded)
            {
                EditorGUILayout.HelpBox("NuGetForUnity is installing… wait for the editor to finish reloading.", MessageType.Info);
                return;
            }

            // Step intermediate — R3 from NuGet (auto)
            if (!r3Installed)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Installing R3 from NuGet…");
                EditorGUILayout.EndHorizontal();

                // Kick off automatically once we land here.
                EditorApplication.delayCall += ModulateDependencyInstaller.InstallR3FromNuGet;
                return;
            }

            // Step 2 — Everything else
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(remainingDone ? "✓ 2 - Installation complete" : "2 - Finish installation");
            using (new EditorGUI.DisabledScope(remainingDone))
            {
                if (GUILayout.Button(remainingDone ? "Done" : "Finish installation", GUILayout.Width(180)))
                {
                    ModulateDependencyInstaller.InstallRemainingDependencies();
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void OnInspectorUpdate()
        {
            // Repaint so the UI reflects state changes after Unity finishes reloads.
            Repaint();
        }
    }
}