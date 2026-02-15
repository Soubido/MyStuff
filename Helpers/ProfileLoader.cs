using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rhino.FileIO;
using Rhino.Geometry;
using System.Reflection;
using Rhino;

namespace NewRhinoGold.Helpers
{
    public static class ProfileLoader
    {
        // Cache: "OrdnerName/ProfilName" -> Kurve
        private static Dictionary<string, Curve> _curveCache = new Dictionary<string, Curve>();

        // Hilfsmethode: Holt den Pfad zum Plugin-Verzeichnis (bin/Release)
        private static string GetPluginDir()
        {
            try
            {
                string assemblyPath = Assembly.GetExecutingAssembly().Location;
                return Path.GetDirectoryName(assemblyPath);
            }
            catch { return null; }
        }

        // Lädt die Liste der Dateinamen aus einem bestimmten Ordner (z.B. "Profiles" oder "Curves")
        public static List<string> GetAvailableProfiles(string folderName)
        {
            string root = GetPluginDir();
            if (root == null) return new List<string>();

            // Pfad zusammenbauen: .../bin/Release/Profiles  ODER  .../bin/Release/Curves
            string targetPath = Path.Combine(root, folderName);

            if (!Directory.Exists(targetPath)) return new List<string>();

            try
            {
                var files = Directory.GetFiles(targetPath, "*.3dm");
                return files.Select(Path.GetFileNameWithoutExtension).ToList();
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error reading {folderName}: {ex.Message}");
                return new List<string>();
            }
        }

        // Lädt eine Kurve aus dem angegebenen Ordner
        public static Curve LoadProfile(string profileName, string folderName)
        {
            string cacheKey = $"{folderName}/{profileName}";
            if (_curveCache.ContainsKey(cacheKey)) return _curveCache[cacheKey].DuplicateCurve();

            string root = GetPluginDir();
            if (root == null) return null;

            string filePath = Path.Combine(root, folderName, profileName + ".3dm");
            if (!File.Exists(filePath)) return null;

            try
            {
                var f3dm = File3dm.Read(filePath);
                foreach (var obj in f3dm.Objects)
                {
                    if (obj.Geometry is Curve c)
                    {
                        BoundingBox bbox = c.GetBoundingBox(true);
                        c.Translate(Vector3d.Negate(new Vector3d(bbox.Center)));
                        _curveCache[cacheKey] = c;
                        return c.DuplicateCurve();
                    }
                }
            }
            catch { }
            return null;
        }

        // Speichert eine Kurve in den angegebenen Ordner
        public static bool SaveProfile(string profileName, Curve curve, string folderName)
        {
            if (string.IsNullOrWhiteSpace(profileName) || curve == null) return false;

            try
            {
                string root = GetPluginDir();
                if (root == null) return false;

                string targetPath = Path.Combine(root, folderName);
                if (!Directory.Exists(targetPath)) Directory.CreateDirectory(targetPath);

                string filePath = Path.Combine(targetPath, profileName + ".3dm");

                Curve toSave = curve.DuplicateCurve();
                BoundingBox bbox = toSave.GetBoundingBox(true);
                Vector3d move = Point3d.Origin - bbox.Center;
                toSave.Translate(move);

                var f3dm = new File3dm();
                f3dm.Objects.AddCurve(toSave);
                return f3dm.Write(filePath, 7);
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Save Error: {ex.Message}");
                return false;
            }
        }
    }
}