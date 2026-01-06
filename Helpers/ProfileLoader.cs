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
        private static Dictionary<string, Curve> _curveCache = new Dictionary<string, Curve>();
        private static string _curvesPath;

        // Eigenschaft, um den Pfad sicher zu holen
        public static string CurvesPath
        {
            get
            {
                if (_curvesPath == null)
                {
                    try
                    {
                        string assemblyPath = Assembly.GetExecutingAssembly().Location;
                        string assemblyDir = Path.GetDirectoryName(assemblyPath);
                        _curvesPath = Path.Combine(assemblyDir, "Curves");
                    }
                    catch
                    {
                        _curvesPath = null;
                    }
                }
                return _curvesPath;
            }
        }

        public static List<string> GetAvailableProfiles()
        {
            var path = CurvesPath;
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                // DEBUG: Sag dem User, wo der Ordner sein sollte
                if (path != null)
                    RhinoApp.WriteLine($"[RingWizard] Curves-Ordner nicht gefunden. Erwartet hier: {path}");

                return new List<string>();
            }

            try
            {
                // Debug Ausgabe
                var files = Directory.GetFiles(path, "*.3dm");
                RhinoApp.WriteLine($"[RingWizard] {files.Length} Profile in '{path}' gefunden.");

                return files.Select(Path.GetFileNameWithoutExtension).ToList();
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"[RingWizard] Fehler beim Lesen der Profile: {ex.Message}");
                return new List<string>();
            }
        }

        public static Curve LoadProfile(string profileName)
        {
            if (_curveCache.ContainsKey(profileName))
                return _curveCache[profileName].DuplicateCurve();

            var path = CurvesPath;
            if (path == null) return null;

            string filePath = Path.Combine(path, profileName + ".3dm");
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
                        _curveCache[profileName] = c;
                        return c.DuplicateCurve();
                    }
                }
            }
            catch
            {
                // Ignorieren
            }
            return null;
        }
    }
}