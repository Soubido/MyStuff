using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Eto.Drawing;
using Eto.Forms;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using NewRhinoGold.Core; // Für GemSmartData, Densities, GemShapes

namespace NewRhinoGold.Reporting
{
    // Die Datenstruktur für eine Zeile im Report
    public class ReportRow
    {
        public int Count { get; set; }
        public string Shape { get; set; }
        public string Material { get; set; }
        public string SizeInfo { get; set; } // z.B. "1.30 mm" oder "5.0 x 3.0 mm"
        public double TotalWeight { get; set; }
        
        // Für interne Gruppierung
        public double RawSizeX { get; set; }
        public double RawSizeY { get; set; }
    }

    public class GemReportDlg : Form
    {
        private GridView _grid;
        private List<ReportRow> _rows;
        private Label _lblTotalCount;
        private Label _lblTotalWeight;

        public GemReportDlg()
        {
            Title = "Gem Report";
            ClientSize = new Size(600, 400);
            MinimumSize = new Size(400, 300);
            
            Content = BuildLayout();
            
            // Daten laden
            RefreshData();
        }

        private Control BuildLayout()
        {
            // Grid Setup
            _grid = new GridView { GridLines = GridLines.Both };
            _grid.Columns.Add(new GridColumn { HeaderText = "Count", DataCell = new TextBoxCell { Binding = Binding.Property<ReportRow, string>(r => r.Count.ToString()) }, Width = 60 });
            _grid.Columns.Add(new GridColumn { HeaderText = "Shape", DataCell = new TextBoxCell { Binding = Binding.Property<ReportRow, string>(r => r.Shape) }, Width = 100 });
            _grid.Columns.Add(new GridColumn { HeaderText = "Material", DataCell = new TextBoxCell { Binding = Binding.Property<ReportRow, string>(r => r.Material) }, Width = 100 });
            _grid.Columns.Add(new GridColumn { HeaderText = "Size", DataCell = new TextBoxCell { Binding = Binding.Property<ReportRow, string>(r => r.SizeInfo) }, Width = 120 });
            _grid.Columns.Add(new GridColumn { HeaderText = "Total Weight (ct)", DataCell = new TextBoxCell { Binding = Binding.Property<ReportRow, string>(r => r.TotalWeight.ToString("F3")) }, Width = 120 });

            // Summary
            _lblTotalCount = new Label { Text = "Total Gems: 0", Font = Fonts.Sans(10, FontStyle.Bold) };
            _lblTotalWeight = new Label { Text = "Total Weight: 0.00 ct", Font = Fonts.Sans(10, FontStyle.Bold) };

            // Buttons
            var btnRefresh = new Button { Text = "Refresh" };
            btnRefresh.Click += (s, e) => RefreshData();

            var btnPrint = new Button { Text = "Print (HTML)" };
            btnPrint.Click += OnPrint;

            var btnSave = new Button { Text = "Save CSV" };
            btnSave.Click += OnSaveCsv;

            var btnClose = new Button { Text = "Close" };
            btnClose.Click += (s, e) => Close();

            // Layout
            var layout = new DynamicLayout { Padding = 10, Spacing = new Size(5, 5) };
            layout.Add(_grid, yscale: true);
            layout.AddRow(null); // Spacer
            layout.AddRow(_lblTotalCount, _lblTotalWeight);
            layout.AddRow(null);
            
            var buttonRow = new StackLayout 
            { 
                Orientation = Orientation.Horizontal, 
                Spacing = 5, 
                HorizontalContentAlignment = HorizontalAlignment.Right,
                Items = { btnRefresh, btnPrint, btnSave, btnClose }
            };
            layout.AddRow(buttonRow);

            return layout;
        }

        private void RefreshData()
        {
            var doc = RhinoDoc.ActiveDoc;
            if (doc == null) return;

            var rawItems = new List<ReportRow>();

            // 1. Alle Objekte scannen
            var settings = new ObjectEnumeratorSettings { IncludeLights = false, IncludeGrips = false, NormalObjects = true, LockedObjects = true };
            foreach (var obj in doc.Objects.GetObjectList(settings))
            {
                // A) Check GemSmartData (GemStudio / GemCreator)
                // Wir suchen UserData vom Typ GemSmartData
                var smartData = obj.Geometry.UserData.Find(typeof(GemSmartData)) as GemSmartData;
                
                if (smartData != null)
                {
                    rawItems.Add(new ReportRow 
                    { 
                        Count = 1,
                        Shape = smartData.CutType,
                        Material = smartData.MaterialName,
                        RawSizeX = smartData.GemSize,
                        RawSizeY = smartData.GemSize, // Annahme: Wenn SmartData keine Y hat, ist es rund/quadratisch. (Erweiterbar)
                        SizeInfo = $"{smartData.GemSize:F2} mm",
                        TotalWeight = smartData.CaratWeight
                    });
                    continue;
                }

                // B) Check UserStrings (PaveStudio Legacy)
                // Format: Shape;Material;SizeX;SizeY
                string rgString = obj.Attributes.GetUserString("RG GEM");
                if (!string.IsNullOrEmpty(rgString))
                {
                    var parts = rgString.Split(';');
                    if (parts.Length >= 3)
                    {
                        string shape = parts[0];
                        string mat = parts[1];
                        double sx = double.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture);
                        double sy = (parts.Length > 3) ? double.Parse(parts[3], System.Globalization.CultureInfo.InvariantCulture) : sx;

                        // Gewicht berechnen (da PaveStudio es nur im String speichert, wenn überhaupt)
                        // Falls Pave es nicht speichert, rechnen wir es hier:
                        double weight = CalculateWeight(shape, mat, sx, sy);

                        rawItems.Add(new ReportRow
                        {
                            Count = 1,
                            Shape = shape,
                            Material = mat,
                            RawSizeX = sx,
                            RawSizeY = sy,
                            SizeInfo = (Math.Abs(sx - sy) < 0.01) ? $"{sx:F2} mm" : $"{sx:F2} x {sy:F2} mm",
                            TotalWeight = weight
                        });
                    }
                }
            }

            // 2. Gruppieren
            // Wir gruppieren nach Form, Material und Größe (mit kleiner Toleranz)
            var grouped = rawItems
                .GroupBy(x => new { x.Shape, x.Material, Size = Math.Round(x.RawSizeX, 2) }) // Gruppierung vereinfacht auf X-Size
                .Select(g => new ReportRow
                {
                    Count = g.Count(),
                    Shape = g.Key.Shape,
                    Material = g.Key.Material,
                    RawSizeX = g.First().RawSizeX,
                    SizeInfo = g.First().SizeInfo,
                    TotalWeight = g.Sum(x => x.TotalWeight)
                })
                .OrderBy(r => r.Shape).ThenByDescending(r => r.RawSizeX)
                .ToList();

            _rows = grouped;
            _grid.DataStore = _rows;

            // Summary update
            int totalCount = _rows.Sum(r => r.Count);
            double totalWeight = _rows.Sum(r => r.TotalWeight);
            _lblTotalCount.Text = $"Total Gems: {totalCount}";
            _lblTotalWeight.Text = $"Total Weight: {totalWeight:F2} ct";
        }

        // Helper zur Gewichtsberechnung für Pave-Steine (falls nötig)
        private double CalculateWeight(string shapeName, string matName, double w, double l)
        {
            double density = Densities.GetDensity(matName);
            if (density == 0) density = 3.52; // Fallback Diamond

            GemShapes.ShapeType type = GemShapes.ShapeType.Round;
            Enum.TryParse(shapeName, out type);
            
            // Geometrie simulieren für Volumen
            Curve c = GemShapes.Create(type, w, l);
            if (c != null)
            {
                var amp = AreaMassProperties.Compute(c);
                if (amp != null)
                {
                    double height = w * 0.6;
                    double vol = amp.Area * height * 0.55; // Faktor für Steinform
                    return Math.Abs(vol) * (density / 1000.0) * 5.0;
                }
            }
            return 0.0;
        }

        private void OnSaveCsv(object sender, EventArgs e)
        {
            var dlg = new SaveFileDialog();
            dlg.Filters.Add(new FileFilter("CSV Files (*.csv)", ".csv"));
            dlg.FileName = "GemReport.csv";

            if (dlg.ShowDialog(this) == DialogResult.Ok)
            {
                try
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("Count;Shape;Material;Size;Total Weight (ct)");
                    foreach(var row in _rows)
                    {
                        sb.AppendLine($"{row.Count};{row.Shape};{row.Material};{row.SizeInfo};{row.TotalWeight:F3}");
                    }
                    
                    // Summary
                    sb.AppendLine($";;;Total Gems:;{_rows.Sum(r => r.Count)}");
                    sb.AppendLine($";;;Total Weight:;{_rows.Sum(r => r.TotalWeight):F3}");

                    File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
                    MessageBox.Show("Report saved successfully.");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error saving file: {ex.Message}");
                }
            }
        }

        private void OnPrint(object sender, EventArgs e)
        {
            // Erzeugt eine temporäre HTML Datei und öffnet sie
            string tempFile = Path.Combine(Path.GetTempPath(), "GemReport.html");
            
            var sb = new StringBuilder();
            sb.AppendLine("<html><head><style>");
            sb.AppendLine("body { font-family: sans-serif; padding: 20px; }");
            sb.AppendLine("table { border-collapse: collapse; width: 100%; }");
            sb.AppendLine("th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }");
            sb.AppendLine("th { background-color: #f2f2f2; }");
            sb.AppendLine("</style></head><body>");
            sb.AppendLine("<h1>Gem Report</h1>");
            sb.AppendLine($"<p>Date: {DateTime.Now}</p>");
            sb.AppendLine("<table>");
            sb.AppendLine("<tr><th>Count</th><th>Shape</th><th>Material</th><th>Size</th><th>Total Weight (ct)</th></tr>");

            foreach (var row in _rows)
            {
                sb.AppendLine($"<tr><td>{row.Count}</td><td>{row.Shape}</td><td>{row.Material}</td><td>{row.SizeInfo}</td><td>{row.TotalWeight:F3}</td></tr>");
            }
            
            // Footer
            sb.AppendLine($"<tr style='font-weight:bold; background-color:#eee'><td>{_rows.Sum(r => r.Count)}</td><td></td><td></td><td>Total:</td><td>{_rows.Sum(r => r.TotalWeight):F3} ct</td></tr>");
            
            sb.AppendLine("</table></body></html>");

            try
            {
                File.WriteAllText(tempFile, sb.ToString());
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = tempFile, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open print view: {ex.Message}");
            }
        }
    }
}