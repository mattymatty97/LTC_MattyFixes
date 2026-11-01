using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using JetBrains.Annotations;
using MattyFixes.Config;
using MattyFixes.Dependency;
using MonoMod.RuntimeDetour;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using LogType = VertexLibrary.LogType;

namespace MattyFixes;

[BepInPlugin(GUID, NAME, VERSION)]
[BepInDependency("com.github.lethalcompanymodding.vertexlibrary", "1.0.0")]
[BepInDependency("TeamBMX.LobbyCompatibility", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("ainavt.lc.lethalconfig", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("imabatby.lethallevelloader", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("evaisa.lethallib", BepInDependency.DependencyFlags.SoftDependency)]
internal partial class MattyFixes : BaseUnityPlugin
{
    public const string GUID = MyPluginInfo.PLUGIN_GUID;
    public const string NAME = MyPluginInfo.PLUGIN_NAME;
    public const string VERSION = MyPluginInfo.PLUGIN_VERSION;
    internal static ManualLogSource Log;

    internal static Harmony Harmony { get; private set; }
    [UsedImplicitly]
    internal static HashSet<Hook> Hooks { get; } = [];

    internal static MattyFixes Instance { get; private set; }
    internal ConfigFile RotationsConfig { get; private set; }
    internal ConfigFile OffsetConfig { get; private set; }

    internal static readonly Dictionary<string, OverrideHolder> ConfigOverrides = [];

    private static int? _visibleLayerMask;
    public static int VisibleLayerMask
    {
        get {
            _visibleLayerMask ??= LayerMask.GetMask("Default",
                "Player", "Water",
                "Props", "Room", "InteractableObject", "Foliage", "PhysicsObject", "Enemies", "PlayerRagdoll",
                "MapHazards", "MiscLevelGeometry", "Terrain");

            return _visibleLayerMask.Value;
        }
    }

    private void Awake()
    {
        RotationsConfig = new ConfigFile(Utility.CombinePaths(Paths.ConfigPath, NAME, "Rotations.cfg"), true, Info.Metadata);
        OffsetConfig = new ConfigFile(Utility.CombinePaths(Paths.ConfigPath, NAME, "Offsets.cfg"), true, Info.Metadata);

        if (Instance == null)
        {
            Instance = this;
        }

        Log = Logger;
        try
        {

            if (LobbyCompatibilityChecker.Enabled)
                LobbyCompatibilityChecker.Init();

            if (DawnLibProxy.Enabled)
                DawnLibProxy.Init();

            Log.LogInfo("Initializing Configs");

            PluginConfig.Init();

            Log.LogInfo("Loading Overrides");

            LoadOverrides();

            Log.LogInfo("Patching Methods");
            Harmony = new Harmony(GUID);
            Harmony.PatchAll(Assembly.GetExecutingAssembly());

            Log.LogInfo(NAME + " v" + VERSION + " Loaded!");

        }
        catch (Exception ex)
        {
            Log.LogError("Exception while initializing: \n" + ex);
        }
    }


    internal static void VerboseMeshLog(LogType logLevel, Func<string> message)
    {
        var level = logLevel switch
        {
            LogType.Fatal => LogLevel.Fatal,
            LogType.Error => LogLevel.Error,
            LogType.Warning => LogLevel.Warning,
            LogType.Info1 or LogType.Info2 or LogType.Info3 or LogType.Info4 or LogType.Info=> LogLevel.Info,
            LogType.Debug1 or LogType.Debug2 or LogType.Debug3 or LogType.Debug4 or LogType.Debug=> LogLevel.Debug,
            LogType.All => LogLevel.All,
            _ => LogLevel.None
        };
        VerboseMeshLog(level, message);
    }

    internal static void VerboseMeshLog(LogLevel logLevel, Func<string> message)
    {
        if ((PluginConfig.Debug.VerboseMeshes.Value & logLevel) != 0)
            Log.Log(logLevel, message());
    }

    internal static void VerboseCupboardLog(LogLevel logLevel, Func<string> message)
    {
        if ((PluginConfig.Debug.VerboseCupboard.Value & logLevel) != 0)
            Log.Log(logLevel, message());
    }

    internal static void VerboseItemsLog(LogLevel logLevel, Func<string> message)
    {
        if ((PluginConfig.Debug.VerboseItems.Value & logLevel) != 0)
            Log.Log(logLevel, message());
    }

    private static void LoadOverrides()
    {

        foreach (var directory in Directory.EnumerateDirectories(Paths.PluginPath)
                     .SelectMany(d => Directory.EnumerateDirectories(d, NAME)))
        {
            var source = Path.GetFileName(Path.GetDirectoryName(directory));
            ProcessDirectory(directory, source);
        }

        return;

        void ProcessDirectory(string directory, string source)
        {
            var relativeDir = Path.GetRelativePath(Paths.BepInExRootPath, directory);

            Log.LogDebug($"Searching {relativeDir}");

            Dictionary<string, OverrideHolder> localMap = new();

            foreach (var jsonPath in Directory.EnumerateFiles(directory, "*.json", SearchOption.AllDirectories))
            {
                var key = Path.GetRelativePath(directory, Path.ChangeExtension(jsonPath, null))
                    .Replace(Path.DirectorySeparatorChar, '/');

                Log.LogDebug($"[{source}] Reading {key}.json");

                try
                {
                    using var file = File.OpenText(jsonPath);
                    using var reader = new JsonTextReader(file);

                    var root = (JObject)JToken.ReadFrom(reader);

                    ProcessOverride(localMap, key, source, root);
                }
                catch (Exception ex)
                {
                    Log.LogError($"[{source}] Exception reading {key}.json\n{ex}");
                }
            }

            var overridesPath = Path.Combine(directory, "overrides.json");

            if (File.Exists(overridesPath))
            {
                Log.LogDebug($"[{source}] Found overrides.json");
                try
                {
                    using var file = File.OpenText(overridesPath);
                    using var reader = new JsonTextReader(file);

                    var root = (JObject)JToken.ReadFrom(reader);

                    foreach (var property in root.Properties())
                    {
                        var key = property.Name;

                        if (property.Value.Type != JTokenType.Object)
                        {
                            Log.LogDebug($"[{source}] overrides.json Key {key} has wrong type={property.Value.Type} Expected={JTokenType.Object}");
                            continue;
                        }

                        var value = (JObject)property.Value;

                        ProcessOverride(localMap, key, source, value);
                    }
                }
                catch (Exception ex)
                {
                    Log.LogError($"[{source}] Exception reading overrides.json\n{ex}");
                }
            }

            Log.LogDebug($"[{source}] Applying overrides");

            foreach (var pair in localMap)
            {
                if (!pair.Key.Contains('/'))
                    continue;

                if (ConfigOverrides.TryGetValue(pair.Key, out var old))
                    if (old.Priority > pair.Value.Priority)
                        continue;

                Log.LogDebug($"[{source}] Overriding {pair.Key} with priority {pair.Value.Priority}");

                ConfigOverrides[pair.Key] = pair.Value;
            }
        }

        void ProcessOverride(Dictionary<string, OverrideHolder> localMap, string key, string source, JObject value)
        {
            if (!localMap.TryGetValue(key, out var holder))
            {
                holder = new OverrideHolder();
                holder.Source = source;
            }

            var priority = 0;

            if (value.TryGetValue("priority", out var token) && token.Type == JTokenType.Integer)
            {
                priority = token.Value<int>();
            }

            if (priority < holder.Priority)
                return;

            var valueChanged = false;

            if (value.TryGetValue("resting_rotation", out token) && token.Type != JTokenType.Array)
            {
                try
                {
                    var array = (List<float>)JsonConvert.DeserializeObject(token.ToString(), typeof(List<float>));
                    if (array is { Count: 3 })
                    {
                        holder.RestingRotation = new Vector3(array[0], array[1], array[2]);
                        valueChanged = true;
                    }
                }
                catch
                {
                    // ignored
                }
            }

            if (value.TryGetValue("vertical_offset", out token) && token.Type != JTokenType.Float)
            {
                holder.VerticalOffset = token.Value<float>();
                valueChanged = true;
            }

            if (!valueChanged)
                return;

            holder.Priority = priority;
            localMap[key] = holder;
        }
    }
}
