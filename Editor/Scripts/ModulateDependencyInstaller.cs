using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace DandyDino.Modulate.Bootstrap
{
    internal static class ModulateDependencyInstaller
    {
        private const string LogPrefix = "[Modulate Installer]";
        
        private const string NuGetForUnityKey = "com.github-glitchenzo.nugetforunity";
        private const string NuGetForUnityUrl = "https://github.com/GlitchEnzo/NuGetForUnity.git?path=/src/NuGetForUnity#v4.4.0";
        private const string SelfPackageName = "com.dandydino.modulateinstaller";
        private const string ModulatePackageName = "com.dandydino.modulate";

        private const string R3NuGetId      = "R3";
        private const string R3NuGetVersion = "1.3.0";
        
        private static readonly Dictionary<string, string> RemainingDependencies = new Dictionary<string, string>
        {
            { "com.gustavopsantos.reflex", "https://github.com/gustavopsantos/reflex.git?path=/Assets/Reflex/#14.3.0" },
            { "com.dandydino.elements",    "https://github.com/AlbertoVosgerau/DDElements.git#0.2.0" },
            { "com.cysharp.unitask",       "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.10" },
            { "com.cysharp.r3",            "https://github.com/Cysharp/R3.git?path=src/R3.Unity/Assets/R3.Unity#1.3.0" },
            { "com.dandydino.modulate",    "https://github.com/AlbertoVosgerau/Modulate.git#0.4.3" },
        };
        
        public static bool IsModulateInstalled()
        {
            JObject manifest = LoadManifest();
            if (manifest == null) return false;
            if (manifest["dependencies"] is not JObject deps) return false;
            return deps[ModulatePackageName] != null;
        }

        public static bool IsNuGetForUnityInManifest()
        {
            JObject manifest = LoadManifest();
            if (manifest == null) return false;
            return manifest["dependencies"] is JObject deps && deps[NuGetForUnityKey] != null;
        }

        public static bool IsNuGetForUnityLoaded()
        {
            return AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name.Equals("NuGetForUnity", System.StringComparison.OrdinalIgnoreCase));
        }

        public static bool IsR3NuGetInstalled()
        {
            string packagesConfig = Path.GetFullPath(Path.Combine(Application.dataPath, "packages.config"));
            return File.Exists(packagesConfig) && File.ReadAllText(packagesConfig).Contains($"id=\"{R3NuGetId}\"");
        }

        public static bool AreRemainingDependenciesInstalled()
        {
            JObject manifest = LoadManifest();
            if (manifest == null)
            {
                return false;
            }
            if (manifest["dependencies"] is not JObject deps)
            {
                return false;
            }
            
            return RemainingDependencies.Keys.All(k => deps[k] != null);
        }

        public static void InstallNuGetForUnity()
        {
            try
            {
                JObject manifest = LoadManifest();
                if (manifest == null) return;

                if (manifest["dependencies"] is not JObject deps)
                {
                    deps = new JObject();
                    manifest["dependencies"] = deps;
                }

                if (deps[NuGetForUnityKey] != null)
                {
                    Debug.Log($"{LogPrefix} NuGetForUnity already in manifest.");
                    return;
                }

                deps[NuGetForUnityKey] = NuGetForUnityUrl;
                SaveManifest(manifest);
                Debug.Log($"{LogPrefix} Added NuGetForUnity. Resolving…");
                ModulateDependencyInstallerEditorWindow.CloseWindow();
                Client.Resolve();
            }
            catch (Exception e)
            {
                Debug.LogError($"{LogPrefix} Failed to install NuGetForUnity: {e}");
            }
        }

        public static void InstallR3FromNuGet()
        {
            if (!IsNuGetForUnityLoaded())
            {
                Debug.LogWarning($"{LogPrefix} NuGetForUnity not loaded yet. Wait for the editor to finish reloading.");
                return;
            }

            if (IsR3NuGetInstalled())
            {
                Debug.Log($"{LogPrefix} R3 NuGet package already installed.");
                return;
            }

            if (!TryInstallNuGetPackage(R3NuGetId, R3NuGetVersion))
            {
                if (EditorUtility.DisplayDialog(
                        "Modulate – automatic install failed",
                        "Could not install R3 automatically. Open the NuGet window and search for \"R3\" to install it manually.",
                        "Open NuGet window",
                        "Cancel"))
                {
                    EditorApplication.ExecuteMenuItem("NuGet/Manage NuGet Packages");
                }
                return;
            }

            AssetDatabase.Refresh();
            Debug.Log($"{LogPrefix} R3 NuGet install complete.");
        }

        public static void InstallRemainingDependencies()
        {
            try
            {
                JObject manifest = LoadManifest();
                if (manifest == null) return;

                if (manifest["dependencies"] is not JObject deps)
                {
                    deps = new JObject();
                    manifest["dependencies"] = deps;
                }

                bool changed = false;
                foreach (KeyValuePair<string, string> kvp in RemainingDependencies)
                {
                    if (deps[kvp.Key] == null)
                    {
                        deps[kvp.Key] = kvp.Value;
                        changed = true;
                        Debug.Log($"{LogPrefix} Added dependency: {kvp.Key}");
                    }
                }

                if (!changed)
                {
                    Debug.Log($"{LogPrefix} All remaining dependencies already present.");
                    return;
                }

                SaveManifest(manifest);
                Debug.Log($"{LogPrefix} manifest.json updated. Resolving packages…");
                
                UninstallSelf();
                Client.Resolve();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"{LogPrefix} Failed to install remaining dependencies: {e}");
            }
        }
        
        public static void UninstallSelf()
        {
            try
            {
                Debug.Log($"{LogPrefix} Removing installer package ({SelfPackageName})…");

                // Belt-and-suspenders: also strip it from manifest.json directly, in case
                // Client.Remove gets interrupted by the assembly unloading mid-call.
                JObject manifest = LoadManifest();
                if (manifest?["dependencies"] is JObject deps && deps[SelfPackageName] != null)
                {
                    deps.Remove(SelfPackageName);
                    SaveManifest(manifest);
                }

                // Ask UPM to remove it properly. This kicks off a resolve which will
                // unload this very assembly — anything after this call may not run.
                Client.Remove(SelfPackageName);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"{LogPrefix} Failed to uninstall self: {e}");
            }
        }

        // ---- Internals -------------------------------------------------------------

        private static JObject LoadManifest()
        {
            string path = GetManifestPath();
            if (!File.Exists(path))
            {
                Debug.LogWarning($"{LogPrefix} manifest.json not found at {path}");
                return null;
            }
            return JObject.Parse(File.ReadAllText(path));
        }

        private static void SaveManifest(JObject manifest)
        {
            File.WriteAllText(GetManifestPath(), manifest.ToString(Formatting.Indented));
        }

        private static string GetManifestPath() => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Packages", "manifest.json"));

        private static bool TryInstallNuGetPackage(string packageId, string version)
        {
            try
            {
                Assembly asm = System.AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name.Equals("NuGetForUnity", System.StringComparison.OrdinalIgnoreCase));

                if (asm == null)
                {
                    return false;
                }

                Type installerType = asm.GetType("NugetForUnity.NugetPackageInstaller") ?? asm.GetType("NugetForUnity.NugetHelper");
                Type identifierType = asm.GetType("NugetForUnity.Models.NugetPackageIdentifier") ?? asm.GetType("NugetForUnity.NugetPackageIdentifier");
                if (installerType == null || identifierType == null)
                {
                    return false;
                }

                ConstructorInfo ctor = identifierType.GetConstructor(new[] { typeof(string), typeof(string) });
                object identifier = ctor.Invoke(new object[] { packageId, version });

                MethodInfo installMethod = installerType
                    .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                    .FirstOrDefault(m => m.Name == "InstallIdentifier"
                                         && m.GetParameters().Length >= 1
                                         && m.GetParameters()[0].ParameterType.IsAssignableFrom(identifierType));
                if (installMethod == null)
                {
                    return false;
                }

                ParameterInfo[] parameters = installMethod.GetParameters();
                object[] args = new object[parameters.Length];
                args[0] = identifier;
                
                for (int i = 1; i < parameters.Length; i++)
                {
                    args[i] = parameters[i].HasDefaultValue
                        ? parameters[i].DefaultValue
                        : (parameters[i].ParameterType.IsValueType
                            ? System.Activator.CreateInstance(parameters[i].ParameterType)
                            : null);
                }

                Debug.Log($"{LogPrefix} Installing NuGet package: {packageId} {version}");
                installMethod.Invoke(null, args);
                
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"{LogPrefix} Failed to install NuGet package {packageId}: {e}");
                
                return false;
            }
        }
    }
}