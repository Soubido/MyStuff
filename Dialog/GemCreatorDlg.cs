using Eto.Drawing;
using Eto.Forms;
using Rhino;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;
using Rhino.Commands;
using System;
using System.Collections.Generic;
using Rhino.UI;
using NewRhinoGold.Core;

namespace NewRhinoGold.Dialog
{
    public class GemCreatorDlg : Form
    {
        private readonly GemDisplayCond _displayCond;

        private bool _canUpdate = true;
        private readonly List<Brep> _previewBreps = new List<Brep>();
        private readonly List<Curve> _previewCurves = new List<Curve>();
        private readonly List<Guid> _selectedCurveIds = new List<Guid>();

        private readonly Dictionary<int, Plane> _previewPlanes = new Dictionary<int, Plane>();
        private readonly Dictionary<int, Curve> _previewBaseCurves = new Dictionary<int, Curve>();

        private Plane _workPlane = Plane.WorldXY;

        private System.Drawing.Color _gemColor = System.Drawing.Color.Blue;
        private double _calculatedCarat = 0.0;

        // --- Styles ---
        private readonly Eto.Drawing.Font _fontStandard = Eto.Drawing.Fonts.Sans(10);
        private readonly Eto.Drawing.Font _fontInput = Eto.Drawing.Fonts.Sans(11);
        private readonly Eto.Drawing.Font _fontHeader = Eto.Drawing.Fonts.Sans(12, FontStyle.Bold);

        // UI Controls
        private CheckBox _checkFlip;
        private Button _btnSelect;
        private Button _btnOk;
        private Button _btnCancel;

        private NumericStepper _numH1; // Crown
        private NumericStepper _numH2; // Girdle
        private NumericStepper _numH3; // Pavilion
        private NumericStepper _numDInt; // Table

        private TextBox _txtGemSize;
        private double _currentGemSizeVal = 0.0;

        private RadioButton _rbPercent;
        private RadioButton _rbMm;

        private ComboBox _comboCompound;
        private TextBox _txtWeight;
        private Button _btnColor;

        public GemCreatorDlg()
        {
            Title = "Gem Creator";
            ClientSize = new Size(260, 450);
            MinimumSize = new Size(260, 400);
            Resizable = false;
            ShowInTaskbar = false;
            Topmost = true;

            _displayCond = new GemDisplayCond();

            Content = BuildLayout();

            Shown += OnShown;
            Closed += OnClosed;
        }

        private Control BuildLayout()
        {
            InitializeControls();

            var layout = new DynamicLayout
            {
                Padding = new Padding(10),
                Spacing = new Size(5, 8)
            };

            layout.BeginVertical();

            // --- SECTION 1: SELECTION ---
            layout.AddRow(CreateHeader("Selection"));
            layout.AddRow(_btnSelect);
            layout.AddRow(_checkFlip);
            layout.AddRow(null);

            // --- SECTION 2: PARAMETERS ---
            layout.AddRow(CreateHeader("Parameters"));

            var paramTable = new TableLayout
            {
                Spacing = new Size(5, 5),
                Rows =
                {
                    CreateRow("Table:", _numDInt),
                    CreateRow("Crown:", _numH1),
                    CreateRow("Girdle:", _numH2),
                    CreateRow("Pavilion:", _numH3)
                }
            };
            layout.AddRow(paramTable);

            // Unit Mode
            var unitRow = new StackLayout
            {
                Orientation = Orientation.Horizontal,
                VerticalContentAlignment = VerticalAlignment.Center,
                Spacing = 10,
                Items =
                {
                    new Label { Text = "Unit Mode:", Font = _fontStandard, VerticalAlignment = VerticalAlignment.Center },
                    _rbPercent,
                    _rbMm
                }
            };
            layout.AddRow(unitRow);
            layout.AddRow(null);

            // --- SECTION 3: PROPERTIES ---
            layout.AddRow(CreateHeader("Properties"));

            var propTable = new TableLayout
            {
                Spacing = new Size(5, 5),
                Rows =
                {
                    CreateRow("Size:", _txtGemSize),
                    CreateRow("Material:", _comboCompound),
                    CreateRow("Weight:", _txtWeight),
                    CreateRow("Color:", _btnColor)
                }
            };
            layout.AddRow(propTable);
            layout.Add(null);

            // --- SECTION 4: FOOTER ---
            var buttonGrid = new TableLayout
            {
                Spacing = new Size(10, 0),
                Rows =
                {
                    new TableRow(
                        new TableCell(_btnOk, true),
                        new TableCell(_btnCancel, true)
                    )
                }
            };
            layout.AddRow(buttonGrid);
            layout.EndVertical();

            return layout;
        }

        private void InitializeControls()
        {
            _btnSelect = new Button { Text = "Select Closed Curve", Font = _fontStandard, Height = 28 };
            _btnSelect.Click += OnSelectCurves;

            _checkFlip = new CheckBox { Text = "Flip Direction", Font = _fontStandard };
            _checkFlip.CheckedChanged += (s, e) => Preview();

            _numH1 = CreateNumeric(14.0);
            _numH2 = CreateNumeric(3.0);
            _numH3 = CreateNumeric(43.0);
            _numDInt = CreateNumeric(56.0);

            _numH1.ValueChanged += (s, e) => Preview();
            _numH2.ValueChanged += (s, e) => Preview();
            _numH3.ValueChanged += (s, e) => Preview();
            _numDInt.ValueChanged += (s, e) => Preview();

            var groupRadio = new RadioButton();
            _rbPercent = new RadioButton(groupRadio) { Text = "%", Checked = true, Font = _fontStandard };
            _rbMm = new RadioButton(groupRadio) { Text = "mm", Font = _fontStandard };

            _rbPercent.CheckedChanged += (s, e) => { if (_rbPercent.Checked == true) ConvertMmToPercent(); };
            _rbMm.CheckedChanged += (s, e) => { if (_rbMm.Checked == true) ConvertPercentToMm(); };

            _txtGemSize = new TextBox { ReadOnly = true, Text = "0.0 mm", Font = _fontStandard, Height = 26 };

            _comboCompound = new ComboBox { Font = _fontStandard, Height = 26 };
            _comboCompound.SelectedIndexChanged += OnMaterialChanged;

            _txtWeight = new TextBox { ReadOnly = true, Font = _fontStandard, Height = 26 };

            _btnColor = new Button { Text = "Color...", Font = _fontStandard, Height = 26 };
            _btnColor.Click += OnPickColor;

            _btnOk = new Button { Text = "OK", Font = _fontStandard, Height = 30 };
            _btnOk.Click += OnOk;

            _btnCancel = new Button { Text = "Cancel", Font = _fontStandard, Height = 30 };
            _btnCancel.Click += (s, e) => Close();
        }

        private Label CreateHeader(string text) => new Label { Text = text, Font = _fontHeader, TextColor = Colors.Gray };
        private Label CreateLabel(string text) => new Label { Text = text, VerticalAlignment = VerticalAlignment.Center, Font = _fontStandard };
        private TableRow CreateRow(string labelText, Control control) => new TableRow(new TableCell(CreateLabel(labelText), false), new TableCell(control, true));
        private NumericStepper CreateNumeric(double val) => new NumericStepper { DecimalPlaces = 1, MinValue = 0, MaxValue = 999, Value = val, Font = _fontInput, Height = 26 };

        private void OnShown(object sender, EventArgs e) { LoadCompounds(); _displayCond.Enable(); Preview(); }
        private void OnClosed(object sender, EventArgs e) { _displayCond.Disable(); RhinoDoc.ActiveDoc?.Views.Redraw(); }

        private void LoadCompounds()
        {
            _comboCompound.Items.Clear();
            foreach (var gem in Densities.Gems) _comboCompound.Items.Add(gem.Name);
            if (_comboCompound.Items.Count > 0) _comboCompound.SelectedIndex = 0;
        }

        private void OnMaterialChanged(object sender, EventArgs e)
        {
            string matName = GetSelectedMaterialName();
            if (!string.IsNullOrEmpty(matName)) { var info = Densities.Get(matName); if (info != null) _gemColor = info.DisplayColor; }
            Preview();
        }

        private string GetSelectedMaterialName() => _comboCompound.SelectedIndex < 0 ? null : _comboCompound.Items[_comboCompound.SelectedIndex].Text;

        private void OnPickColor(object sender, EventArgs e)
        {
            var dlg = new ColorDialog(); dlg.Color = Eto.Drawing.Color.FromArgb(_gemColor.R, _gemColor.G, _gemColor.B, _gemColor.A);
            if (dlg.ShowDialog(this) == DialogResult.Ok) { var c = dlg.Color; _gemColor = System.Drawing.Color.FromArgb((int)(c.A * 255), (int)(c.R * 255), (int)(c.G * 255), (int)(c.B * 255)); Preview(); }
        }

        private void ConvertPercentToMm()
        {
            double size = _currentGemSizeVal; if (size < 0.001) return;
            _canUpdate = false;
            try { _numDInt.Value = _numDInt.Value * size / 100.0; _numH1.Value = _numH1.Value * size / 100.0; _numH2.Value = _numH2.Value * size / 100.0; _numH3.Value = _numH3.Value * size / 100.0; }
            finally { _canUpdate = true; }
            Preview();
        }

        private void ConvertMmToPercent()
        {
            double size = _currentGemSizeVal; if (size < 0.001) return;
            _canUpdate = false;
            try { _numDInt.Value = (_numDInt.Value / size) * 100.0; _numH1.Value = (_numH1.Value / size) * 100.0; _numH2.Value = (_numH2.Value / size) * 100.0; _numH3.Value = (_numH3.Value / size) * 100.0; }
            finally { _canUpdate = true; }
            Preview();
        }

        private void OnSelectCurves(object sender, EventArgs e)
        {
            var doc = RhinoDoc.ActiveDoc; var go = new GetObject(); go.SetCommandPrompt("Select closed curves"); go.GeometryFilter = ObjectType.Curve; go.EnablePreSelect(true, true);
            Visible = false; go.GetMultiple(1, 0); Visible = true;
            if (go.CommandResult() == Result.Success)
            {
                _selectedCurveIds.Clear();
                for (int i = 0; i < go.ObjectCount; i++) _selectedCurveIds.Add(go.Object(i).ObjectId);
                if (doc.Views.ActiveView != null) _workPlane = doc.Views.ActiveView.ActiveViewport.ConstructionPlane();
                Preview();
            }
        }

        private bool Preview()
        {
            if (!_canUpdate) return false;
            var doc = RhinoDoc.ActiveDoc;
            if (doc == null || _selectedCurveIds.Count == 0) return false;

            _previewBreps.Clear(); _previewPlanes.Clear(); _previewBaseCurves.Clear(); _previewCurves.Clear();

            double h1 = _numH1.Value; double h2 = _numH2.Value; double h3 = _numH3.Value; double dInt = _numDInt.Value;
            bool inPercent = _rbPercent.Checked == true;
            int index = 0; double totalVolume = 0;

            foreach (var id in _selectedCurveIds)
            {
                var obj = doc.Objects.FindId(id) as CurveObject; if (obj == null) continue;
                Curve baseCrv = obj.CurveGeometry ?? obj.Geometry as Curve; if (baseCrv == null) continue;

                var c2 = baseCrv.DuplicateCurve();
                Plane pl; if (!c2.TryGetPlane(out pl)) pl = _workPlane;
                if (_checkFlip.Checked == true) pl.Flip();

                // 1. Ausrichtung zu WorldXY für BoundingBox
                var toWorld = Transform.PlaneToPlane(pl, Plane.WorldXY);
                c2.Transform(toWorld);

                var bb = c2.GetBoundingBox(true);
                double width = bb.Max.X - bb.Min.X; if (width < 0.001) continue;
                if (index == 0) { _currentGemSizeVal = width; _txtGemSize.Text = $"{width:F2} mm"; }

                // 2. Plane zentrieren
                Point3d centerInWorld = bb.Center;
                Point3d correctedOrigin = pl.PointAt(centerInWorld.X, centerInWorld.Y);
                Plane centeredPlane = new Plane(pl); centeredPlane.Origin = correctedOrigin;
                _previewPlanes[index] = centeredPlane;

                // --- FIX: SEAM ALIGNMENT (Verdrehte Cutter beheben) ---
                // Wir suchen den Punkt auf der Kurve (im WorldSpace), der am weitesten rechts (+X) liegt.
                // Dadurch startet die Kurve immer bei "3 Uhr" relativ zur Plane.
                // Das verhindert Twists beim Loften mit Kreisen.
                Point3d targetSeamPt = bb.Center + Vector3d.XAxis * (width * 0.5);
                if (c2.ClosestPoint(targetSeamPt, out double tSeam))
                {
                    c2.ChangeClosedCurveSeam(tSeam);
                }

                // WICHTIG: Die gespeicherte Basiskurve muss auch diesen neuen Seam haben!
                // Da c2 transformiert wurde, ist der Parameter tSeam auch für die Originalkurve gültig (bei rigider Transformation).
                Curve correctedBaseCrv = baseCrv.DuplicateCurve();
                correctedBaseCrv.ChangeClosedCurveSeam(tSeam); // Seam korrigieren
                _previewBaseCurves[index] = correctedBaseCrv; // Diese Kurve wird gespeichert
                _previewCurves.Add(correctedBaseCrv);

                double H1 = inPercent ? h1 * width / 100.0 : h1;
                double H2 = inPercent ? h2 * width / 100.0 : h2;
                double H3 = inPercent ? h3 * width / 100.0 : h3;
                double D = inPercent ? dInt * width / 100.0 : dInt;

                // Geometrie erstellen
                Curve girdleTop = c2.DuplicateCurve();
                Curve girdleBottom = c2.DuplicateCurve(); girdleBottom.Translate(0, 0, -H2);

                var crownCurve = c2.DuplicateCurve();
                crownCurve.Transform(Transform.Scale(bb.Center, D / width));
                crownCurve.Translate(0, 0, H1);
                Plane tablePlane = new Plane(new Point3d(0, 0, H1), Vector3d.ZAxis);
                if (!crownCurve.IsPlanar()) crownCurve = Curve.ProjectToPlane(crownCurve, tablePlane);

                Point3d apex = bb.Center; apex.Z = -H2 - H3;

                var breps = new List<Brep>();
                var crownLoft = Brep.CreateFromLoft(new[] { crownCurve, girdleTop }, Point3d.Unset, Point3d.Unset, LoftType.Straight, false);
                if (crownLoft != null) breps.AddRange(crownLoft);

                if (H2 > 0.001)
                {
                    var girdleLoft = Brep.CreateFromLoft(new[] { girdleTop, girdleBottom }, Point3d.Unset, Point3d.Unset, LoftType.Straight, false);
                    if (girdleLoft != null) breps.AddRange(girdleLoft);
                }

                var pavLoft = Brep.CreateFromLoft(new[] { girdleBottom, Point3dToCurve(apex) }, Point3d.Unset, Point3d.Unset, LoftType.Straight, false);
                if (pavLoft != null) breps.AddRange(pavLoft);

                var finalBrep = Brep.JoinBreps(breps, doc.ModelAbsoluteTolerance);
                if (finalBrep != null && finalBrep.Length > 0)
                {
                    var b = finalBrep[0];
                    b = b.CapPlanarHoles(doc.ModelAbsoluteTolerance);
                    var mp = VolumeMassProperties.Compute(b); if (mp != null) totalVolume += Math.Abs(mp.Volume);

                    // Zurück transformieren zum Objekt-Ort
                    var toInstance = Transform.PlaneToPlane(Plane.WorldXY, pl);
                    b.Transform(toInstance);
                    _previewBreps.Add(b);
                }
                index++;
            }

            CalculateWeight(totalVolume);
            _displayCond.SetColor(_gemColor);
            _displayCond.setbreps(_previewBreps.ToArray());
            _displayCond.setcurves(_previewCurves.ToArray());
            doc.Views.Redraw();
            return true;
        }

        private void CalculateWeight(double volumeMm3)
        {
            string matName = GetSelectedMaterialName();
            if (string.IsNullOrEmpty(matName)) { _txtWeight.Text = "0.00 ct"; _calculatedCarat = 0.0; return; }
            double density = Densities.GetDensity(matName);
            double massGrams = volumeMm3 * (density / 1000.0);
            _calculatedCarat = massGrams * 5.0;
            _txtWeight.Text = $"{_calculatedCarat:F2} ct";
        }

        private static Curve Point3dToCurve(Point3d pt) => new Circle(new Plane(pt, Vector3d.ZAxis), 0.001).ToNurbsCurve();

        private void OnOk(object sender, EventArgs e)
        {
            var doc = RhinoDoc.ActiveDoc;
            if (doc == null || _previewBreps.Count == 0)
            {
                Close();
                return;
            }

            uint undo = doc.BeginUndoRecord("Create Smart Gem");
            try
            {
                for (int i = 0; i < _previewBreps.Count; i++)
                {
                    var brep = _previewBreps[i];
                    if (brep == null) continue;

                    // 1. Attribute erstellen
                    var attr = new ObjectAttributes();
                    attr.Name = "SmartGem";
                    string matName = GetSelectedMaterialName() ?? "Gem";
                    attr.SetUserString("RG MATERIAL ID", matName);
                    attr.ObjectColor = _gemColor;
                    attr.ColorSource = ObjectColorSource.ColorFromObject;

                    // 2. Originaldaten holen
                    Curve originalCurve = null;
                    Plane originalPlane = Plane.WorldXY;

                    if (_previewBaseCurves.TryGetValue(i, out Curve cVal)) originalCurve = cVal;
                    if (_previewPlanes.TryGetValue(i, out Plane pVal)) originalPlane = pVal;

                    // 3. SmartData Instanz erstellen
                    // WICHTIG: "CutType" sollte idealerweise beschreibend sein, z.B. "CustomProfile"
                    var smartData = new GemSmartData(originalCurve, originalPlane, "CustomProfile", _currentGemSizeVal, matName, _calculatedCarat);

                    // 4. FEHLENDE PARAMETER HINZUFÜGEN (WICHTIG!)
                    // Wir müssen die Werte zurück in Prozent rechnen, falls sie in mm sind,
                    // damit die parametrische Logik konsistent bleibt.
                    bool inPercent = _rbPercent.Checked == true;
                    double size = _currentGemSizeVal > 0.001 ? _currentGemSizeVal : 1.0;

                    if (inPercent)
                    {
                        smartData.TablePercent = _numDInt.Value;
                        smartData.CrownHeightPercent = _numH1.Value;
                        smartData.GirdleThicknessPercent = _numH2.Value;
                        smartData.PavilionHeightPercent = _numH3.Value;
                    }
                    else
                    {
                        // Umrechnung mm -> % für die Speicherung
                        smartData.TablePercent = (_numDInt.Value / size) * 100.0;
                        smartData.CrownHeightPercent = (_numH1.Value / size) * 100.0;
                        smartData.GirdleThicknessPercent = (_numH2.Value / size) * 100.0;
                        smartData.PavilionHeightPercent = (_numH3.Value / size) * 100.0;
                    }

                    // 5. KORREKTUR: Daten an Attribute hängen, nicht an Brep-Geometrie
                    attr.UserData.Add(smartData);

                    // 6. Objekt ins Dokument einfügen
                    doc.Objects.AddBrep(brep, attr);
                }
            }
            finally
            {
                doc.EndUndoRecord(undo);
            }
            Close();
        }
    }
}