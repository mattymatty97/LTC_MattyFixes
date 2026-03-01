using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using MattyFixes.Dependency;
using MattyFixes.Interfaces;

namespace MattyFixes.Utils;

public static class ItemCategory
{
    public enum ItemType
    {
        Unknown,
        Vanilla,
        Modded
    }
    
    // ReSharper disable function SuspiciousTypeConversion.Global
    [NotNull] 
    public static string GetPath(this Item item)
    {
        var path = ((IInjectedItem)item).MattyFixes_Path;
        if (path != null)
            return path;
        
        var type = ItemType.Unknown;
        path = item.ComputePath("Unknown");
            
        if (DawnLibProxy.Enabled)
        {
            var dawnType = DawnLibProxy.DefineItem(item, out var dawnPath);
            if (type <= dawnType)
            {
                type = dawnType;
                path = dawnPath;
            }
        }

        ((IInjectedItem)item).MattyFixes_ItemType = type;
        ((IInjectedItem)item).MattyFixes_Path     = path;
        return path;
    }
    
    
    public static string ComputePath(this Item item, string library, params string[] path)
    {
        return Path.Combine(((List<string>)[library, ..path, item.itemName]).Select(p => string.Join("_",
                p.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries))
            .TrimEnd('.')).ToArray()).Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
    

    private static readonly Regex ConfigFilterRegex = new Regex(@"[\n\t\\\'\[\]]");

    public static string SanitizeForConfig(string input)
    {
        return ConfigFilterRegex.Replace(input, "").Trim();
    }
}
