using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace NewRhinoGold
{
    public enum MaterialType
    {
        Gem,
        Metal,
        Other
    }

    public class DensityInfo
    {
        public string Id { get; }
        public string Name { get; }
        public MaterialType Type { get; }
        public double Density { get; }
        public Color DisplayColor { get; }

        public DensityInfo(string id, string name, MaterialType type, double density, Color displayColor)
        {
            Id = id;
            Name = name;
            Type = type;
            Density = density;
            DisplayColor = displayColor;
        }
    }

    public static class Densities
    {
        private static readonly Dictionary<string, DensityInfo> _byKey;

        static Densities()
        {
            var items = new List<DensityInfo>();

            // --- GEMS ---
            items.Add(new DensityInfo("gem.diamond", "Diamond", MaterialType.Gem, 3.52, Color.White));
            items.Add(new DensityInfo("gem.ruby", "Ruby", MaterialType.Gem, 4.00, Color.Red));
            items.Add(new DensityInfo("gem.sapphire", "Sapphire", MaterialType.Gem, 4.00, Color.RoyalBlue));
            items.Add(new DensityInfo("gem.emerald", "Emerald", MaterialType.Gem, 2.72, Color.LimeGreen));
            items.Add(new DensityInfo("gem.amethyst", "Amethyst", MaterialType.Gem, 2.65, Color.MediumPurple));
            items.Add(new DensityInfo("gem.aquamarine", "Aquamarine", MaterialType.Gem, 2.70, Color.LightSkyBlue));
            items.Add(new DensityInfo("gem.topaz", "Topaz", MaterialType.Gem, 3.55, Color.Goldenrod));
            items.Add(new DensityInfo("gem.citrine", "Citrine", MaterialType.Gem, 2.65, Color.Orange));
            items.Add(new DensityInfo("gem.garnet", "Garnet", MaterialType.Gem, 3.80, Color.DarkRed));
            items.Add(new DensityInfo("gem.opal", "Opal", MaterialType.Gem, 2.10, Color.LightCyan));
            items.Add(new DensityInfo("gem.tourmaline", "Tourmaline", MaterialType.Gem, 3.10, Color.Teal));
            items.Add(new DensityInfo("gem.peridot", "Peridot", MaterialType.Gem, 3.30, Color.YellowGreen));
            items.Add(new DensityInfo("gem.tanzanite", "Tanzanite", MaterialType.Gem, 3.35, Color.SlateBlue));

            // --- METALS (Standard) ---
            items.Add(new DensityInfo("metal.ag925", "Ag 925", MaterialType.Metal, 10.3, Color.Silver));
            items.Add(new DensityInfo("metal.au750", "Au 750", MaterialType.Metal, 15.5, Color.Gold));
            items.Add(new DensityInfo("metal.pt950", "Pt 950", MaterialType.Metal, 21.4, Color.LightSteelBlue));
            items.Add(new DensityInfo("metal.pd950", "Pd 950", MaterialType.Metal, 12.0, Color.LightGray));

            // --- METALS (Spezifisch aus Goldgewicht.py) ---
            // "Gelbgold 750": 15.4
            items.Add(new DensityInfo("metal.au750.yellow", "Gelbgold 750", MaterialType.Metal, 15.4, Color.Gold));
            // "Weissgold 750": 17.9
            items.Add(new DensityInfo("metal.au750.white", "Weissgold 750", MaterialType.Metal, 17.9, Color.LightGray));
            // "Rosegold 750": 15.3
            items.Add(new DensityInfo("metal.au750.rose", "Rosegold 750", MaterialType.Metal, 15.3, Color.RosyBrown));
            // "Rotgold 5N 750": 15
            items.Add(new DensityInfo("metal.au750.red5n", "Rotgold 5N 750", MaterialType.Metal, 15.0, Color.IndianRed));
            // "Rotgold 6N 750": 15.1
            items.Add(new DensityInfo("metal.au750.red6n", "Rotgold 6N 750", MaterialType.Metal, 15.1, Color.DarkRed));
            // "Platin 950": 21.45 (Präziser als Standard)
            items.Add(new DensityInfo("metal.pt950.prec", "Platin 950 (Spezial)", MaterialType.Metal, 21.45, Color.LightSteelBlue));
            // "Silber 925": 10.49 (Präziser als Standard)
            items.Add(new DensityInfo("metal.ag925.prec", "Silber 925 (Spezial)", MaterialType.Metal, 10.49, Color.Silver));


            _byKey = new Dictionary<string, DensityInfo>(StringComparer.OrdinalIgnoreCase);

            foreach (var info in items)
            {
                if (!_byKey.ContainsKey(info.Id)) _byKey.Add(info.Id, info);
                if (!string.IsNullOrEmpty(info.Name) && !_byKey.ContainsKey(info.Name)) _byKey.Add(info.Name, info);
            }
        }

        public static IEnumerable<DensityInfo> All => _byKey.Values.Distinct();
        public static IEnumerable<DensityInfo> Gems => All.Where(d => d.Type == MaterialType.Gem);
        public static IEnumerable<DensityInfo> Metals => All.Where(d => d.Type == MaterialType.Metal);

        public static string[] GetAllMaterialNames()
        {
            return All.Select(d => d.Name).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().OrderBy(n => n).ToArray();
        }

        public static double GetDensity(string nameOrId)
        {
            if (string.IsNullOrEmpty(nameOrId)) return 0.0;
            if (_byKey.TryGetValue(nameOrId, out var info)) return info.Density;
            return 0.0;
        }

        public static DensityInfo Get(string nameOrId)
        {
            if (string.IsNullOrEmpty(nameOrId)) return null;
            _byKey.TryGetValue(nameOrId, out var info);
            return info;
        }
    }
}