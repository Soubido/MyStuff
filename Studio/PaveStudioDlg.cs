using System;
using System.Collections.Generic;
using Eto.Drawing;
using Eto.Forms;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input.Custom;
using Rhino.UI;
using NewRhinoGold.Core;
using NewRhinoGold.Helpers;
using NewRhinoGold.BezelStudio;

namespace NewRhinoGold.Studio
{
    // Datenklasse für gesetzte Steine
    public class PaveItem
    {
        public Point3d Position { get; set; }
        public Vector3d Normal { get; set; }
        public double SizeX { get; set; }
        public double SizeY { get; set; }
        public string Shape { get; set; }
        public Brep Geometry { get; set; }
    }

    public class PaveStudioDlg : Form
    {
        private readonly GemDisplayCond _previewConduit;

        // Die isolierte Arbeitsfläche
        private Brep _workingSurfaceBrep;

        // Liste der gesetzten Steine
        private List<PaveItem> _placedGems = new List<PaveItem>();

        // --- Styles (Centralized) ---
        private readonly Eto.Drawing.Font _fontStandard = Eto.Drawing.Fonts.Sans(10);
        private readonly Eto.Drawing.Font _fontInput = Eto.Drawing.Fonts.Sans(11);
        private readonly Eto.Drawing.Font _fontHeader = Eto.Drawing.Fonts.Sans(12, FontStyle.Bold);

        // UI Controls
        private Button _btnSelectSurf;
        private DropDown _drpShape;
        private NumericStepper _numSizeX;
        private NumericStepper _numSizeY;
        private NumericStepper _numGap;
        private DropDown _drpMaterial;
        private Label _lblWeight;

        private CheckBox _chkFlip;
        private CheckBox _chkAllowCollision;
        private CheckBox _chkSymX;
        private CheckBox _chkSymY;

        private Button _btnInsert;
        private Button _btnUndo;
        private Button _btnOk;
        private Button _btnCancel;

        // Aktuelle Farbe (aus Densities)
        private System.Drawing.Color _currentGemColor = System.Drawing.Color.White;

        public PaveStudioDlg()
        {
            Title = "Pave Studio";
            // Fenstergröße angepasst für bessere Lesbarkeit
            ClientSize = new Eto.Drawing.Size(300, 450);
            Topmost = true;
            Resizable = false;

            _previewConduit = new GemDisplayCond();

            // 1. Layout bauen und ZUWEISEN
            Content = BuildLayout();

            // Events
            Shown += (s, e) => _previewConduit.Enable();
            Closed += (s, e) => {
                _previewConduit.Disable();
                _workingSurfaceBrep = null; // Speicher freigeben
                RhinoDoc.ActiveDoc?.Views.Redraw();
            };

            // Initiale Berechnung
            UpdateUIState();
        }

        private Control BuildLayout()
        {
            InitializeControls();

            var layout = new DynamicLayout
            {
                Padding = new Padding(10),
                Spacing = new Eto.Drawing.Size(5, 5)
            };

            layout.BeginVertical();

            // --- SECTION 1: SELECTION ---
            layout.AddRow(CreateHeader("Reference Surface"));
            layout.AddRow(_btnSelectSurf);

            layout.AddRow(null);

            // --- SECTION 2: PARAMETERS ---
            layout.AddRow(CreateHeader("Pave Settings"));

            var paramGrid = new TableLayout { Spacing = new Eto.Drawing.Size(5, 5) };
            paramGrid.Rows.Add(CreateRow("Shape:", _drpShape));
            paramGrid.Rows.Add(CreateRow("Size X:", _numSizeX));
            paramGrid.Rows.Add(CreateRow("Size Y:", _numSizeY));
            paramGrid.Rows.Add(CreateRow("Gap:", _numGap));
            paramGrid.Rows.Add(CreateRow("Material:", _drpMaterial));
            paramGrid.Rows.Add(CreateRow("Weight:", _lblWeight));

            layout.AddRow(paramGrid);

            layout.AddRow(null);

            // --- SECTION 3: OPTIONS ---
            layout.AddRow(CreateHeader("Options"));
            layout.AddRow(_chkFlip);
            layout.AddRow(_chkAllowCollision);
            layout.AddRow(_chkSymX);
            layout.AddRow(_chkSymY);

            layout.AddRow(null);

            // --- SECTION 4: ACTIONS (Tools) ---
            layout.AddRow(CreateHeader("Actions"));

            // Buttons nebeneinander für besseren Workflow
            var actionGrid = new TableLayout
            {
                Spacing = new Eto.Drawing.Size(5, 0),
                Rows =
                {
                    new TableRow(
                        new TableCell(_btnInsert, true),
                        new TableCell(_btnUndo, true)
                    )
                }
            };
            layout.AddRow(actionGrid);

            // Spacer (Spring)
            layout.Add(null);

            // --- SECTION 5: FOOTER ---
            var footer = new TableLayout
            {
                Spacing = new Eto.Drawing.Size(10, 0),
                Rows =
                {
                    new TableRow(
                        new TableCell(_btnOk, true),
                        new TableCell(_btnCancel, true)
                    )
                }
            };
            layout.AddRow(footer);

            layout.EndVertical();

            return layout;
        }

        private void InitializeControls()
        {
            // --- Selection ---
            _btnSelectSurf = new Button { Text = "Select Surface (Face)", Font = _fontStandard, Height = 30 };
            _btnSelectSurf.Click += OnSelectSurface;

            // --- Parameters ---
            _drpShape = new DropDown { Font = _fontStandard, Height = 26 };
            foreach (string name in GemShapes.GetNames()) _drpShape.Items.Add(name);
            _drpShape.SelectedIndex = 0;
            _drpShape.SelectedIndexChanged += (s, e) => UpdateUIState();

            _numSizeX = CreateStepper(1.3, 2, 0.1);
            _numSizeX.MinValue = 0.5;
            _numSizeX.ValueChanged += (s, e) => UpdateUIState();

            _numSizeY = CreateStepper(1.3, 2, 0.1);
            _numSizeY.MinValue = 0.5;
            _numSizeY.Enabled = false;
            _numSizeY.ValueChanged += (s, e) => UpdateWeightInfo();

            _numGap = CreateStepper(0.2, 2, 0.05);
            _numGap.MinValue = 0.0;

            _drpMaterial = new DropDown { Font = _fontStandard, Height = 26 };
            _drpMaterial.Items.Add("Diamond");
            if (Densities.Gems != null) foreach (var d in Densities.Gems) _drpMaterial.Items.Add(d.Name);
            if (_drpMaterial.Items.Count > 0) _drpMaterial.SelectedIndex = 0;
            _drpMaterial.SelectedIndexChanged += (s, e) => UpdateWeightInfo();

            _lblWeight = new Label { Text = "-", Font = Eto.Drawing.Fonts.Sans(10, FontStyle.Bold), VerticalAlignment = VerticalAlignment.Center };

            // --- Options ---
            _chkFlip = new CheckBox { Text = "Flip Normal", Font = _fontStandard };
            _chkAllowCollision = new CheckBox { Text = "Allow Collision", Font = _fontStandard };
            _chkSymX = new CheckBox { Text = "Mirror X (World)", Font = _fontStandard };
            _chkSymY = new CheckBox { Text = "Mirror Y (World)", Font = _fontStandard };

            // --- Actions ---
            _btnInsert = new Button { Text = "Start Pave", Font = _fontStandard, Height = 30 };
            _btnInsert.Click += OnInsertLoop;

            _btnUndo = new Button { Text = "Undo Last", Font = _fontStandard, Height = 30 };
            _btnUndo.Click += OnUndo;

            // --- Footer ---
            _btnOk = new Button { Text = "OK", Font = _fontStandard, Height = 30 };
            _btnOk.Click += OnOk;

            _btnCancel = new Button { Text = "Cancel", Font = _fontStandard, Height = 30 };
            _btnCancel.Click += (s, e) => Close();
        }

        // --- Helpers ---
        private Label CreateHeader(string text)
        {
            return new Label { Text = text, Font = _fontHeader, TextColor = Colors.Gray };
        }

        private TableRow CreateRow(string labelText, Control ctrl)
        {
            return new TableRow(
                new TableCell(new Label { Text = labelText, VerticalAlignment = VerticalAlignment.Center, Font = _fontStandard }, false),
                new TableCell(ctrl, true)
            );
        }

        private NumericStepper CreateStepper(double v, int d = 2, double inc = 0.1)
        {
            return new NumericStepper
            {
                Value = v,
                DecimalPlaces = d,
                Increment = inc,
                Font = _fontInput,
                Height = 26
            };
        }

        // --- LOGIC (EXACT COPY) ---

        private void UpdateUIState()
        {
            if (_drpShape.SelectedKey == null) return;
            string shape = _drpShape.SelectedKey;
            bool isSymmetric = (shape == "Round" || shape == "Square");

            if (isSymmetric)
            {
                _numSizeY.Enabled = false;
                _numSizeY.Value = _numSizeX.Value;
            }
            else
            {
                _numSizeY.Enabled = true;
            }
            UpdateWeightInfo();
        }

        private void UpdateWeightInfo()
        {
            if (_drpShape.SelectedKey == null || _drpMaterial.SelectedKey == null) return;

            string matName = _drpMaterial.SelectedKey;
            double density = Densities.GetDensity(matName);
            var info = Densities.Get(matName);

            // Farbe setzen für das Tool
            _currentGemColor = info != null ? info.DisplayColor : System.Drawing.Color.White;

            double w = _numSizeX.Value;
            double l = _numSizeY.Value;
            string shapeName = _drpShape.SelectedKey;

            GemShapes.ShapeType type = GemShapes.ShapeType.Round;
            Enum.TryParse(shapeName, out type);
            Curve baseCrv = GemShapes.Create(type, w, l);

            if (baseCrv != null)
            {
                var amp = AreaMassProperties.Compute(baseCrv);
                if (amp != null)
                {
                    double area = amp.Area;
                    double height = w * 0.6;
                    double volume = area * height * 0.55;
                    double carat = Math.Abs(volume) * (density / 1000.0) * 5.0;
                    _lblWeight.Text = $"{carat:F3} ct";
                    return;
                }
            }
            _lblWeight.Text = "-";
        }

        private void OnSelectSurface(object sender, EventArgs e)
        {
            var go = new GetObject();
            go.SetCommandPrompt("Select ONE Surface Face (SubObject)");
            go.GeometryFilter = ObjectType.Surface;
            go.EnablePreSelect(true, true);
            go.SubObjectSelect = true;
            go.DeselectAllBeforePostSelect = true;
            go.OneByOnePostSelect = true;

            // Dialog ausblenden während Auswahl
            Visible = false;
            go.Get();
            Visible = true;

            if (go.CommandResult() == Rhino.Commands.Result.Success)
            {
                var objRef = go.Object(0);
                var face = objRef.Face();

                if (face != null)
                {
                    // ISOLATION: Wir erstellen eine Kopie der Fläche im Speicher
                    _workingSurfaceBrep = face.DuplicateFace(false);

                    if (_workingSurfaceBrep != null)
                    {
                        _btnSelectSurf.Text = "Face Selected";
                        objRef.Object().Select(false);
                    }
                }
            }
        }

        private void OnInsertLoop(object sender, EventArgs e)
        {
            if (_workingSurfaceBrep == null)
            {
                MessageBox.Show("Please select a surface first.");
                return;
            }

            while (true)
            {
                // Liste für Kollisionsprüfung erstellen
                var existingData = new List<ExistingGemData>();
                foreach (var item in _placedGems)
                {
                    double r = (item.SizeX + item.SizeY) / 4.0;
                    existingData.Add(new ExistingGemData { Point = item.Position, Radius = r });
                }

                var tool = new PavePlacementTool(
                    _workingSurfaceBrep,
                    _numSizeX.Value,
                    _numSizeY.Value,
                    _numGap.Value,
                    _drpShape.SelectedKey,
                    _chkFlip.Checked ?? false,
                    _chkSymX.Checked ?? false,
                    _chkSymY.Checked ?? false,
                    _chkAllowCollision.Checked ?? false,
                    existingData,
                    _currentGemColor
                );

                tool.SetCommandPrompt("Click to place gem (Right-click to stop)");
                tool.AcceptNothing(true);

                this.Visible = false;
                var result = tool.Get();
                this.Visible = true;

                if (result == Rhino.Input.GetResult.Point)
                {
                    var newGems = tool.GetPlacedGems();

                    if (newGems != null && newGems.Count > 0)
                    {
                        foreach (var g in newGems)
                        {
                            AddGem(g.Position, g.Normal, g.Geometry);
                        }
                    }
                }
                else
                {
                    break;
                }
            }
        }

        private void AddGem(Point3d pt, Vector3d normal, Brep gemGeo)
        {
            var item = new PaveItem
            {
                Position = pt,
                Normal = normal,
                SizeX = _numSizeX.Value,
                SizeY = _numSizeY.Value,
                Shape = _drpShape.SelectedKey,
                Geometry = gemGeo
            };
            _placedGems.Add(item);
            UpdateConduit();
        }

        private void OnUndo(object sender, EventArgs e)
        {
            if (_placedGems.Count > 0)
            {
                _placedGems.RemoveAt(_placedGems.Count - 1);
                UpdateConduit();
            }
        }

        private void UpdateConduit()
        {
            var breps = new List<Brep>();
            foreach (var item in _placedGems) if (item.Geometry != null) breps.Add(item.Geometry);
            _previewConduit.setbreps(breps.ToArray());
            RhinoDoc.ActiveDoc.Views.Redraw();
        }

        private void OnOk(object sender, EventArgs e)
        {
            if (_placedGems.Count > 0)
            {
                var doc = RhinoDoc.ActiveDoc;
                var sn = doc.BeginUndoRecord("Pave Insert");

                try
                {
                    string matName = _drpMaterial.SelectedKey;

                    // Einmalig Dichte holen
                    double density = Densities.GetDensity(matName);

                    foreach (var item in _placedGems)
                    {
                        var attr = doc.CreateDefaultAttributes();
                        attr.Name = "PaveGem";

                        var info = Densities.Get(matName);
                        if (info != null)
                        {
                            attr.ObjectColor = info.DisplayColor;
                            attr.ColorSource = ObjectColorSource.ColorFromObject;
                        }

                        // Legacy String (zur Sicherheit behalten)
                        attr.SetUserString("RG GEM", $"{item.Shape};{matName};{item.SizeX};{item.SizeY}");

                        // --- SMART DATA ERSTELLEN ---

                        // 1. Kurve rekonstruieren
                        GemShapes.ShapeType type = GemShapes.ShapeType.Round;
                        Enum.TryParse(item.Shape, out type);
                        Curve baseCrv = GemShapes.Create(type, item.SizeX, item.SizeY);

                        // Kurve an die Position des Steins transformieren
                        Plane gemPlane = new Plane(item.Position, item.Normal);
                        // Shapes werden auf WorldXY erstellt, wir müssen sie auf die gemPlane hieven
                        baseCrv.Transform(Transform.PlaneToPlane(Plane.WorldXY, gemPlane));

                        // 2. Gewicht schätzen
                        double weight = 0.0;
                        if (item.Geometry != null)
                        {
                            var mp = VolumeMassProperties.Compute(item.Geometry);
                            if (mp != null)
                                weight = Math.Abs(mp.Volume) * (density / 1000.0) * 5.0;
                        }

                        // 3. SmartData Objekt bauen
                        var smartData = new GemSmartData(
                            baseCrv,
                            gemPlane,
                            item.Shape,
                            item.SizeX,
                            matName,
                            weight
                        );

                        // 4. An Geometry kleben
                        item.Geometry.UserData.Add(smartData);

                        doc.Objects.AddBrep(item.Geometry, attr);
                    }
                }
                finally
                {
                    doc.EndUndoRecord(sn);
                }

                doc.Views.Redraw();
            }
            Close();
        }
    }
}