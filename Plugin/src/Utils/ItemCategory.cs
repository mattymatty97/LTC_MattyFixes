using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using MattyFixes.Dependency;
using MattyFixes.Interfaces;
using MattyFixes.ItemVariants;
using UnityEngine;

namespace MattyFixes.Utils;

public static class ItemCategory
{
    public enum ItemType
    {
        Unknown,
        Vanilla,
        Modded
    }

    public enum ItemLibrary
    {
        Unknown,
        Vanilla,
        LethalLib,
        LethalLevelLoader,
        DawnLib,
    }

    extension(Item item)
    {
        // ReSharper disable function SuspiciousTypeConversion.Global
        [NotNull]
        public string GetIdentifier()
        {
            var path = ((IInjectedItem)item).MattyFixes_Path;
            if (path != null)
                return path;

            var type = ItemType.Unknown;
            var library = ItemLibrary.Unknown;
            path = ComputePath(library, item.itemName ?? item.name);

            if (DawnLibProxy.Enabled)
            {
                var dawnType = DawnLibProxy.DefineItem(item, out var dawnPath);
                if (type <= dawnType)
                {
                    type = dawnType;
                    path = dawnPath;
                    library = ItemLibrary.DawnLib;
                }
            }

            ((IInjectedItem)item).MattyFixes_ItemType = type;
            ((IInjectedItem)item).MattyFixes_Library  = library;
            ((IInjectedItem)item).MattyFixes_Path     = path;
            return path;
        }



        public IItemVariant[] GetSupportedVariants()
        {

            ((IInjectedItem)item).MattyFixes_ItemVariant ??= ((IInjectedItem)item).MattyFixes_ItemType switch
            {
                ItemType.Vanilla => new VanillaItem(item),
                ItemType.Unknown => new UnknownItem(item),
                ItemType.Modded => new ModdedItem(item, ((IInjectedItem)item).MattyFixes_Library),
                _ => throw new ArgumentOutOfRangeException()
            };

            if (DawnLibProxy.Enabled)
                return DawnLibProxy.GetItemVariants(item);

            return [((IInjectedItem)item).MattyFixes_ItemVariant];
        }
    }

    extension(GrabbableObject item)
    {
        [NotNull]
        public IItemVariant GetVariant()
        {
            var itemType = item.itemProperties;

            ((IInjectedItem)itemType).MattyFixes_ItemVariant ??= ((IInjectedItem)item).MattyFixes_ItemType switch
            {
                ItemType.Vanilla => new VanillaItem(itemType),
                ItemType.Unknown => new UnknownItem(itemType),
                ItemType.Modded => new ModdedItem(itemType, ((IInjectedItem)itemType).MattyFixes_Library),
                _ => throw new ArgumentOutOfRangeException()
            };

            if (DawnLibProxy.Enabled)
                return DawnLibProxy.GetItemVariant(item);

            return ((IInjectedItem)item).MattyFixes_ItemVariant;
        }

        public Vector3 RestingRotation => item.GetVariant().RestingRotation;

        public float FloorYOffset => item.GetVariant().FloorYOffset;

        public float VerticalOffset => item.GetVariant().VerticalOffset;
    }

    public static string ComputePath(ItemLibrary library, [NotNull] params string[] path)
    {
        var tag = string.Join(Path.AltDirectorySeparatorChar, [
                .. ((List<string>)[library.ToString(), .. path])
                .Select(p =>
                    string.Join("_", p.Split(Path.GetInvalidPathChars(), StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.')
                )
            ]
        ).Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return tag;
    }

    private static readonly Regex ConfigFilterRegex = new Regex(@"[\n\t\\\'\[\]]");

    public static string SanitizeForConfig(string input)
    {
        return ConfigFilterRegex.Replace(input, "").Trim();
    }
}
