using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rhino.FileIO;
using Rhino.Geometry;
using System.Reflection;

namespace NewRhinoGold.Helpers
{
    public static class ProfileLoader
    {
        // Cache: Damit wir die Datei nicht 1000x lesen, speichern wir geladene Kurven
        private static Dictionary<string, Curve> _curveCache = new Dictionary<string, Curve>();
        private static string _profilesPath;

        // Initialisierung: Findet den Pfad
        static ProfileLoader()
        {
            string assemblyPath = Assembly.GetExecutingAssembly().Location;
            string assemblyDir = Path.GetDirectoryName(assemblyPath);
            _profilesPath = Path.Combine(assemblyDir, "Profiles");

            // Ordner erstellen, falls nicht existent
            if (!Directory.Exists(_profilesPath))
            {
                Directory.CreateDirectory(_profilesPath);
            }
        }

        // Gibt eine Liste aller .3dm Dateien zurück (ohne Endung)
        public static List<string> GetAvailableProfiles()
        {
            if (!Directory.Exists(_profilesPath)) return new List<string>();

            return Directory.GetFiles(_profilesPath, "*.3dm")
                            .Select(Path.GetFileNameWithoutExtension)
                            .ToList();
        }

        // Lädt die Kurve aus der Datei
        public static Curve LoadProfile(string profileName)
        {
            // 1. Ist es schon im Cache?
            if (_curveCache.ContainsKey(profileName)) 
                return _curveCache[profileName].DuplicateCurve();

            // 2. Pfad bauen
            string path = Path.Combine(_profilesPath, profileName + ".3dm");
            if (!File.Exists(path)) return CreateFallbackCircle(); // Datei fehlt? Nimm Kreis.

            try
            {
                // 3. Rhino Datei lesen (File3dm liest ohne Rhino zu öffnen!)
                var f3dm = File3dm.Read(path);
                
                // Wir nehmen die ERSTE Kurve, die wir finden
                foreach (var obj in f3dm.Objects)
                {
                    if (obj.Geometry is Curve c)
                    {
                        // 4. SANITIZE: Kurve auf 0,0,0 zentrieren!
                        // Das ist extrem wichtig, egal wo du sie gezeichnet hast.
                        BoundingBox bbox = c.GetBoundingBox(true);
                        c.Translate(Vector3d.Negate(new Vector3d(bbox.Center)));
                        
                        // Optional: Auf 1x1 Einheit normalisieren? 
                        // Besser nicht, wir vertrauen darauf, dass Width/Height im Wizard das regelt.
                        
                        _curveCache[profileName] = c;
                        return c.DuplicateCurve();
                    }
                }
            }
            catch
            {
                // Fehler beim Lesen
            }

            return CreateFallbackCircle();
        }

        private static Curve CreateFallbackCircle()
        {
            return new Circle(Plane.WorldXY, 0.5).ToNurbsCurve();
        }
    }
}