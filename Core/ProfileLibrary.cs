using System;
using System.Collections.Generic;
using System.IO;
using System.Linq; // Wichtig für .First()
using System.Xml.Serialization;
using Eto.Drawing;
using Rhino;
using Rhino.Geometry;

namespace NewRhinoGold.Core
{
    [Serializable]
    public class SavedProfileData
    {
        public string Name { get; set; }
        public string CurveBase64 { get; set; }
    }

    public class ProfileItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public Curve BaseCurve { get; set; }
        public Image Preview { get; set; }

        public ProfileItem(string name, Curve curve)
        {
            Id = Guid.NewGuid();
            Name = name;

            if (curve != null)
            {
                var c = curve.DuplicateCurve();
                var bbox = c.GetBoundingBox(true);
                var trans = Transform.Translation((Point3d.Origin - bbox.Center));
                c.Transform(trans);
                BaseCurve = c;
                Preview = GeneratePreview(BaseCurve);
            }
        }

        private static Image GeneratePreview(Curve c)
        {
            int w = 64; int h = 64;
            var bmp = new Bitmap(w, h, PixelFormat.Format32bppRgba);
            using (var g = new Graphics(bmp))
            {
                g.Clear(Colors.White);
                var bbox = c.GetBoundingBox(true);
                double width = bbox.Max.X - bbox.Min.X;
                double height = bbox.Max.Y - bbox.Min.Y;
                double maxDim = Math.Max(width, height);
                if (maxDim < 0.001) maxDim = 1.0;

                float scale = (float)((w - 10) / maxDim);
                float midX = w / 2f;
                float midY = h / 2f;

                var polyCurve = c.ToPolyline(0, 0, 0.1, 0, 0, 0, 0, 0, true);
                if (polyCurve != null && polyCurve.TryGetPolyline(out Rhino.Geometry.Polyline pl))
                {
                    var etoPts = new List<PointF>();
                    foreach (var p in pl)
                        etoPts.Add(new PointF(midX + (float)p.X * scale, midY - (float)p.Y * scale));
                    g.DrawPolygon(Colors.Black, etoPts.ToArray());
                }
            }
            return bmp;
        }
    }

    public static class ProfileLibrary
    {
        public static List<ProfileItem> Items { get; private set; } = new List<ProfileItem>();

        private static string _folderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NewRhinoGold");
        private static string _filePath => Path.Combine(_folderPath, "headcrv.xml");

        static ProfileLibrary()
        {
            LoadLibrary();
        }

        public static void AddCurve(string name, Curve c)
        {
            var newItem = new ProfileItem(name, c);
            Items.Add(newItem);
            SaveLibrary();
        }

        public static ProfileItem Get(Guid id)
        {
            return Items.FirstOrDefault(x => x.Id == id) ?? Items.FirstOrDefault();
        }

        private static void SaveLibrary()
        {
            try
            {
                if (!Directory.Exists(_folderPath)) Directory.CreateDirectory(_folderPath);

                var dataList = new List<SavedProfileData>();

                foreach (var item in Items)
                {
                    if (item.Name == "Round" || item.Name == "Square") continue;

                    string b64 = CurveToBase64(item.BaseCurve);
                    if (!string.IsNullOrEmpty(b64))
                    {
                        dataList.Add(new SavedProfileData { Name = item.Name, CurveBase64 = b64 });
                    }
                }

                XmlSerializer serializer = new XmlSerializer(typeof(List<SavedProfileData>));
                using (TextWriter writer = new StreamWriter(_filePath))
                {
                    serializer.Serialize(writer, dataList);
                }
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error saving library: {ex.Message}");
            }
        }

        private static void LoadLibrary()
        {
            Items.Clear();

            // Defaults
            Items.Add(new ProfileItem("Round", new Circle(Plane.WorldXY, 1.0).ToNurbsCurve()));
            Items.Add(new ProfileItem("Square", new Rectangle3d(Plane.WorldXY, new Interval(-1, 1), new Interval(-1, 1)).ToNurbsCurve()));

            if (File.Exists(_filePath))
            {
                try
                {
                    List<SavedProfileData> dataList = null;
                    XmlSerializer serializer = new XmlSerializer(typeof(List<SavedProfileData>));

                    using (FileStream fs = new FileStream(_filePath, FileMode.Open))
                    {
                        dataList = (List<SavedProfileData>)serializer.Deserialize(fs);
                    }

                    if (dataList != null)
                    {
                        foreach (var saved in dataList)
                        {
                            Curve geo = Base64ToCurve(saved.CurveBase64);
                            if (geo != null)
                            {
                                Items.Add(new ProfileItem(saved.Name, geo));
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    RhinoApp.WriteLine($"Error loading library: {ex.Message}");
                }
            }
        }

        private static string CurveToBase64(Curve c)
        {
            if (c == null) return null;
            string tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".3dm");

            try
            {
                var file = new Rhino.FileIO.File3dm();
                file.Objects.AddCurve(c);
                file.Write(tempFile, 7);

                byte[] bytes = File.ReadAllBytes(tempFile);
                return Convert.ToBase64String(bytes);
            }
            catch
            {
                return null;
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        private static Curve Base64ToCurve(string b64)
        {
            if (string.IsNullOrEmpty(b64)) return null;
            string tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".3dm");

            try
            {
                byte[] bytes = Convert.FromBase64String(b64);
                File.WriteAllBytes(tempFile, bytes);

                var file = Rhino.FileIO.File3dm.Read(tempFile);
                if (file != null && file.Objects.Count > 0)
                {
                    // KORREKTUR CS0021: Statt file.Objects[0] nutzen wir eine Schleife
                    foreach (var obj in file.Objects)
                    {
                        return obj.Geometry as Curve;
                    }
                }
            }
            catch
            {
                return null;
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
            return null;
        }
    }
}