using System;
using System.Linq;
using System.Globalization;
using Rhino.DocObjects;
using Rhino.Geometry;
using NewRhinoGold.Core;

namespace NewRhinoGold.Helpers
{
    public static class RhinoGoldHelper
    {
        public static bool TryGetGemData(RhinoObject obj, out Curve girdleCurve, out Plane gemPlane, out double gemSize)
        {
            girdleCurve = null;
            gemPlane = Plane.WorldXY;
            gemSize = 0.0;

            if (obj == null) return false;

            // 1. Smart Data checken
            if (obj.Geometry.UserData.Find(typeof(GemSmartData)) is GemSmartData smartData)
            {
                girdleCurve = smartData.BaseCurve?.DuplicateCurve();
                gemPlane = smartData.GemPlane;
                gemSize = smartData.GemSize;

                // Validierung: Wenn SmartData da ist, aber Geometrie verschoben wurde (z.B. Block),
                // müssen wir sicherstellen, dass die Plane stimmt. 
                // Bei Blocks ist die UserData oft auf der Instanz.
                if (girdleCurve != null && gemPlane.IsValid) return true;
            }

            // 2. Plane ermitteln (auch für Blocks)
            bool planeFound = TryGetGemPlane(obj, out gemPlane);

            // 3. UserStrings (Legacy)
            string rgData = obj.Attributes.GetUserString("RG GEM");
            if (string.IsNullOrEmpty(rgData)) rgData = obj.Attributes.GetUserString("RG GEM CUSTOM");

            if (!string.IsNullOrEmpty(rgData))
            {
                string[] parts = rgData.Split(';');
                if (parts.Length >= 4)
                {
                    if (double.TryParse(parts[3].Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double s))
                        gemSize = s;
                }
            }

            // 4. Geometrie für Kurven-Extraktion vorbereiten
            GeometryBase geoToAnalyze = obj.Geometry;

            // SPECIAL HANDLING FÜR BLOCKS (GemCreator)
            if (obj is InstanceObject iObj)
            {
                // Wir müssen die Geometrie INNEN im Block finden und transformieren
                var def = iObj.InstanceDefinition;
                if (def != null)
                {
                    var objects = def.GetObjects();
                    // Wir suchen das größte Mesh/Brep im Block
                    var mainGeoObj = objects
                        .OrderByDescending(o => GetBoundingBoxVolume(o.Geometry))
                        .FirstOrDefault();

                    if (mainGeoObj != null)
                    {
                        // Geometrie duplizieren und transformieren
                        geoToAnalyze = mainGeoObj.Geometry.Duplicate();
                        geoToAnalyze.Transform(iObj.InstanceXform);
                    }
                }
            }

            // 5. Girdle extrahieren
            girdleCurve = ExtractGirdleCurve(geoToAnalyze, gemPlane);

            if (girdleCurve != null)
            {
                if (!planeFound && girdleCurve.TryGetPlane(out Plane curvePlane))
                {
                    gemPlane = curvePlane;
                    if (gemPlane.Normal * Vector3d.ZAxis < 0) gemPlane.Flip();
                }

                if (gemSize <= 0.001)
                {
                    Box planeBox = Box.Unset;
                    girdleCurve.GetBoundingBox(gemPlane, out planeBox);
                    gemSize = Math.Min(planeBox.X.Length, planeBox.Y.Length);
                }

                if (!gemPlane.IsValid)
                {
                    Point3d center = girdleCurve.GetBoundingBox(true).Center;
                    gemPlane = new Plane(center, Vector3d.ZAxis);
                }

                return true;
            }

            return false;
        }

        private static double GetBoundingBoxVolume(GeometryBase geo)
        {
            var bbox = geo.GetBoundingBox(true);
            return bbox.IsValid ? bbox.Volume : 0;
        }

        public static BezelParameters CalculateDefaults(double stoneSize)
        {
            return new BezelParameters
            {
                Height = stoneSize * 1.2,
                ThicknessTop = Math.Max(stoneSize / 10.0, 0.6),
                ThicknessBottom = Math.Max(stoneSize / 10.0, 0.6),
                Offset = 0.1,
                ZOffset = 0.0,
                SeatDepth = (stoneSize * 1.2) * 0.25,
                SeatLedge = 0.4,
                Chamfer = 0.0,
                Bombing = 0.0
            };
        }

        private static bool TryGetGemPlane(RhinoObject obj, out Plane plane)
        {
            plane = Plane.WorldXY;

            if (obj is InstanceObject iObj)
            {
                Transform xform = iObj.InstanceXform;
                Point3d origin = Point3d.Origin; origin.Transform(xform);
                Vector3d xAxis = Vector3d.XAxis; xAxis.Transform(xform);
                Vector3d yAxis = Vector3d.YAxis; yAxis.Transform(xform);
                plane = new Plane(origin, xAxis, yAxis);
                return true;
            }

            if (obj.Geometry is Brep brep)
            {
                var largestFace = brep.Faces
                    .Where(f => f.IsPlanar())
                    .OrderByDescending(f => AreaMassProperties.Compute(f)?.Area ?? 0)
                    .FirstOrDefault();

                if (largestFace != null)
                {
                    if (largestFace.TryGetPlane(out Plane facePlane))
                    {
                        plane = facePlane;
                        return true;
                    }
                }
            }
            return false;
        }

        private static Curve ExtractGirdleCurve(GeometryBase geo, Plane refPlane)
        {
            Mesh analysisMesh = null;

            if (geo is Mesh m)
            {
                analysisMesh = m;
            }
            else if (geo is Brep brep)
            {
                var meshes = Mesh.CreateFromBrep(brep, MeshingParameters.FastRenderMesh);
                if (meshes == null || meshes.Length == 0)
                    meshes = Mesh.CreateFromBrep(brep, MeshingParameters.QualityRenderMesh);

                if (meshes != null && meshes.Length > 0)
                {
                    analysisMesh = new Mesh();
                    foreach (var part in meshes) analysisMesh.Append(part);
                }
            }

            if (analysisMesh != null)
            {
                var outlines = analysisMesh.GetOutlines(refPlane);
                if (outlines != null && outlines.Length > 0)
                {
                    var bestPoly = outlines.OrderByDescending(x => x.Length).FirstOrDefault();
                    if (bestPoly != null) return bestPoly.ToNurbsCurve();
                }
            }

            if (geo is Curve curveGeo) return curveGeo;
            return null;
        }
    }
}