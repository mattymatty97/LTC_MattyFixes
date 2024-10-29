using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.ComTypes;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using MattyFixes.Preloader.Cecil;
using Mono.Cecil;
using Mono.Cecil.Cil;
using FieldAttributes = Mono.Cecil.FieldAttributes;
using MethodAttributes = Mono.Cecil.MethodAttributes;

namespace MattyFixes.Preloader
{
    internal class MattyFixes
    {
        internal static ManualLogSource Log { get; } = Logger.CreateLogSource(nameof(MattyFixes));
        
        public static IEnumerable<string> TargetDLLs { get; } = new string[] { "Assembly-CSharp.dll" };

        private static readonly string MainDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        
        public static void Patch(AssemblyDefinition assembly)
        {
            var logHandler = (bool fail, string message) =>
            {
                if (fail)
                    Log.LogWarning(message);
                Log.LogInfo(message);
            };
            
            Log.LogWarning($"Patching {assembly.Name.Name}");
            if (assembly.Name.Name == "Assembly-CSharp")
            {

                var itemDefinition = assembly.MainModule.Types.FirstOrDefault(t => t.FullName == "Item");
                if (itemDefinition == null)
                    return;

                var verticalOffsetField = itemDefinition.Fields.FirstOrDefault(f => f.Name == "verticalOffset");
                if (verticalOffsetField == null)
                    return;

                var grabbableType = assembly.MainModule.Types.FirstOrDefault(t => t.FullName == "GrabbableObject");
                if (grabbableType == null)
                    return;

                var itemPropertiesField = grabbableType.Fields.FirstOrDefault(f => f.Name == "itemProperties");
                if (itemPropertiesField == null)
                    return;

                grabbableType.AddField(FieldAttributes.Assembly, "MattyFixes_localVerticalOffset",
                    verticalOffsetField.FieldType, out var localOffsetField, logHandler);


                AssemblyAnalyzer.ProcessAssembly(assembly,itemPropertiesField, verticalOffsetField, localOffsetField, out var count);


                grabbableType.AddMethod("Awake",out var awake, MethodAttributes.Private, grabbableType.Module.TypeSystem.Void, logCallback: logHandler);

                awake.Body.Instructions.Clear();
                var ilProcessor = awake.Body.GetILProcessor();

                ilProcessor.Emit(OpCodes.Ldarg_0);
                ilProcessor.Emit(OpCodes.Dup);
                ilProcessor.Emit(OpCodes.Ldfld, itemPropertiesField);
                ilProcessor.Emit(OpCodes.Ldfld, verticalOffsetField);
                ilProcessor.Emit(OpCodes.Stfld, localOffsetField);
                ilProcessor.Emit(OpCodes.Ret);
            }
            
            if (!PluginConfig.Enabled.Value) 
                return;
            
            var outputAssembly = $"{PluginConfig.OutputPath.Value}/{assembly.Name.Name}{PluginConfig.OutputExtension.Value}";
            Log.LogWarning($"Saving modified Assembly to {outputAssembly}");
            assembly.Write(outputAssembly);
        }
        
        // Cannot be renamed, method name is important
        public static void Initialize()
        {
            Log.LogInfo($"Prepatcher Started");
            PluginConfig.Init();
        }

        // Cannot be renamed, method name is important
        public static void Finish()
        {
            Log.LogInfo($"Prepatcher Finished");
        }
        
        public static class PluginConfig
        {
            public static void Init()
            {
                var config = new ConfigFile(Utility.CombinePaths(MainDir, "Development.cfg"), true);
                //Initialize Configs
                Enabled = config.Bind("DevelOptions", "Enabled", false, "Enable development dll output");
                OutputPath = config.Bind("DevelOptions", "OutputPath", MainDir, "Folder where to write the modified dlls");
                OutputExtension = config.Bind("DevelOptions", "OutputExtension", ".pdll", "Extension to use for the modified dlls\n( Do not use .dll if outputting inside the BepInEx folders )");

                //remove unused options
                PropertyInfo orphanedEntriesProp = config.GetType()
                    .GetProperty("OrphanedEntries", BindingFlags.NonPublic | BindingFlags.Instance);

                var orphanedEntries = (Dictionary<ConfigDefinition, string>)orphanedEntriesProp!.GetValue(config, null);

                orphanedEntries.Clear(); // Clear orphaned entries (Unbinded/Abandoned entries)
                config.Save(); // Save the config file
            }

            internal static ConfigEntry<bool> Enabled;
            internal static ConfigEntry<string> OutputPath;
            internal static ConfigEntry<string> OutputExtension;
        }

    }
}