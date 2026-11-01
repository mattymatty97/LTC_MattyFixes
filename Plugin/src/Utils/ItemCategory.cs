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
        public ItemCategoryHolder MattyFixes_Category
        {
            get
            {
                var category = ((IInjectedItem)item).MattyFixes_ItemCategory;
                if (category.HasValue)
                    return category.Value;

                var type = ItemType.Unknown;
                var library = ItemLibrary.Unknown;
                var identifier = ComputePath(library, item.itemName ?? item.name);

                if (DawnLibProxy.Enabled)
                {
                    var dawnType = DawnLibProxy.DefineItem(item, out var dawnIdentifier);
                    if (type <= dawnType)
                    {
                        type = dawnType;
                        identifier = dawnIdentifier;
                        library = ItemLibrary.DawnLib;
                    }
                }

                var holder = new ItemCategoryHolder(item, type, library, identifier);

                ((IInjectedItem)item).MattyFixes_ItemCategory = holder;

                return holder;
            }
        }

        [NotNull] public string MattyFixes_Identifier => item.MattyFixes_Category.Identifier;

        [NotNull]
        public IItemVariant[] MattyFixes_Variants
        {
            get
            {
                {

                    if (DawnLibProxy.Enabled)
                        return DawnLibProxy.GetItemVariants(item);

                    return [item.MattyFixes_Category.BaseVariant];
                }
            }
        }
    }

    extension(GrabbableObject item)
    {
        [NotNull]
        public IItemVariant MattyFixes_Variant
        {
            get
            {
                if (DawnLibProxy.Enabled)
                    return DawnLibProxy.GetItemVariant(item);

                return item.itemProperties.MattyFixes_Category.BaseVariant;
            }
        }

        public Vector3 MattyFixes_RestingRotation => item.MattyFixes_Variant.RestingRotation;

        public float MattyFixes_FloorYOffset => item.MattyFixes_Variant.FloorYOffset;

        public float MattyFixes_VerticalOffset => item.MattyFixes_Variant.VerticalOffset;
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

    public readonly struct ItemCategoryHolder(Item itemProperties, ItemType type, ItemLibrary library, string identifier)
    {
        public Item ItemProperties => itemProperties;
        public ItemType Type => type;
        public ItemLibrary Library => library;

        public string Identifier => identifier;

        public IItemVariant BaseVariant { get; } = type switch
        {
            ItemType.Vanilla => new VanillaItem(itemProperties, identifier),
            ItemType.Unknown => new UnknownItem(itemProperties, identifier),
            ItemType.Modded => new ModdedItem(itemProperties, library, identifier),
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}
