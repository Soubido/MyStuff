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

namespace NewRhinoGold.BezelStudio
{
    public class BezelStudioDlg : Form
    {
        private GemDisplayCond _previewConduit;

        // Input Data
        private Guid _selectedGemId = Guid.Empty;
        private Curve _gemCurve;
        private Plane _gemPlane;
        private double _gemSize;

        // UI Controls
        private Button _btnSelectGem;

        // Geometrie Parameter
        private NumericStepper _numHeight;
        private NumericStepper _numThickTop;
        private NumericStepper _numThickBottom;
        private NumericStepper _numOffset;
        private NumericStepper _numGemGap;
        private NumericStepper _numZOffset;

        // Seat Parameter
        private NumericStepper _numSeatDepth;
        private NumericStepper _numSeatLedge;

        // Features
        private CheckBox _chkCreateCutter;
        private NumericStepper _numChamfer;
        private NumericStepper _numBombing;

        // Material & Farbe
        private ComboBox _comboMaterial;
        private Button _btnColor;
        private System.Drawing.Color _bezelColor = System.Drawing.Color.Gold; // Default Gold

        private Button _btnOk;
        private Button _btnCancel;

        // Temp Logic Storage
        private Brep _tempBezel = null;

        public BezelStudioDlg()
        {
            Title = "Bezel Studio";
            // Etwas breiter und höher für die Sektionen
            ClientSize = new Size(250, 400);
            Topmost = true;
            Resizable = false;
            Padding = new Padding(10);

            _previewConduit = new GemDisplayCond();

            Content = BuildLayout();

            // Material Init
            LoadMaterials();

            Shown += (s, e) => _previewConduit.Enable();
            Closed += (s, e) => { _previewConduit.Disable(); RhinoDoc.ActiveDoc?.Views.Redraw(); };
        }

        private Control BuildLayout()
        {
            InitializeControls();

            var layout = new DynamicLayout
            {
                Padding = Padding.Empty,
                Spacing = new Size(5, 5)
            };

            layout.BeginVertical();

            // --- SECTION 1: SELECTION ---
            // Select Button über volle Breite
            layout.AddRow(_btnSelectGem);

            layout.AddRow(null); // Spacer

            // --- SECTION 2: DIMENSIONS ---
            layout.AddRow(CreateHeader("Dimensions"));
            layout.AddRow(CreateLabel("Total Height:"), _numHeight);
            layout.AddRow(CreateLabel("Thickness Top:"), _numThickTop);
            layout.AddRow(CreateLabel("Thickness Bottom:"), _numThickBottom);

            layout.AddRow(null);

            // --- SECTION 3: OFFSETS ---
            layout.AddRow(CreateHeader("Offsets"));
            layout.AddRow(CreateLabel("Gem Gap:"), _numOffset); // UI Text vs Variable name angepasst an Ihre Vorlage
            layout.AddRow(CreateLabel("Cutter Gap:"), _numGemGap);
            layout.AddRow(CreateLabel("Z-Offset:"), _numZOffset);

            layout.AddRow(null);

            // --- SECTION 4: SEAT SETTINGS ---
            layout.AddRow(CreateHeader("Seat Settings"));
            layout.AddRow(CreateLabel("Seat Depth:"), _numSeatDepth);
            layout.AddRow(CreateLabel("Seat Ledge:"), _numSeatLedge);

            layout.AddRow(null);

            // --- SECTION 5: FEATURES ---
            layout.AddRow(CreateHeader("Features"));
            layout.AddRow(CreateLabel("Tapering (Chamfer):"), _numChamfer);
            layout.AddRow(CreateLabel("Curvature (Bombing):"), _numBombing);
            // Checkbox in eigener Zeile
            layout.AddRow(null, _chkCreateCutter); // Rechtsbündig unter den Steppern? Oder links?
            // Besser: Volle Breite oder explizit gelabelt. 
            // layout.AddRow(_chkCreateCutter); -> Nimmt erste Spalte.
            // Wir machen es so:
            layout.AddRow(new TableLayout { Rows = { new TableRow(null, _chkCreateCutter) } }); // Checkbox rechtsbündig unter Controls

            layout.AddRow(null);

            // --- SECTION 6: MATERIAL ---
            layout.AddRow(CreateHeader("Material"));
            layout.AddRow(CreateLabel("Alloy:"), _comboMaterial);
            layout.AddRow(CreateLabel("Color:"), _btnColor);

            // --- SPACER ---
            layout.Add(null);

            // --- SECTION 7: FOOTER ---
            var buttonGrid = new TableLayout
            {
                Spacing = new Size(10, 0),
                Rows =
                {
                    new TableRow(
                        new TableCell(_btnOk, true),     // 50%
                        new TableCell(_btnCancel, true)  // 50%
                    )
                }
            };
            layout.AddRow(buttonGrid);

            layout.EndVertical();

            return layout;
        }

        private void InitializeControls()
        {
            _btnSelectGem = new Button { Text = "Select Gem" };
            _btnSelectGem.Click += OnSelectGem;

            // Defaults
            _numHeight = CreateStepper(3.88);
            _numThickTop = CreateStepper(0.76);
            _numThickBottom = CreateStepper(0.76);

            _numOffset = CreateStepper(0.10);
            _numGemGap = CreateStepper(0.05);
            _numZOffset = CreateStepper(0.00);
            _numZOffset.MinValue = -100.0;

            _numSeatDepth = CreateStepper(0.66);
            _numSeatLedge = CreateStepper(0.74);

            _numChamfer = CreateStepper(0.0);
            _numBombing = CreateStepper(0.0);

            _chkCreateCutter = new CheckBox { Text = "Create Cutter", Checked = true };
            _chkCreateCutter.CheckedChanged += (s, e) => UpdatePreview();

            // Material Controls
            _comboMaterial = new ComboBox();
            _comboMaterial.SelectedIndexChanged += OnMaterialChanged;

            _btnColor = new Button { Text = "Color" };
            _btnColor.Click += OnPickColor;

            _btnOk = new Button { Text = "Build" };
            _btnOk.Click += OnBuild;

            _btnCancel = new Button { Text = "Close" };
            _btnCancel.Click += (s, e) => Close();
        }

        // --- Helpers ---

        private Label CreateLabel(string text)
        {
            return new Label { Text = text, VerticalAlignment = VerticalAlignment.Center };
        }

        private Label CreateHeader(string text)
        {
            // SAFE: Explicit Eto.Drawing Namespace
            return new Label { Text = text, Font = Eto.Drawing.Fonts.Sans(8, FontStyle.Bold), TextColor = Colors.Gray };
        }

        private NumericStepper CreateStepper(double val, int decimals = 2, double increment = 0.1)
        {
            var s = new NumericStepper
            {
                Value = val,
                DecimalPlaces = decimals,
                Increment = increment
                // Width entfernt -> Auto-Size durch DynamicLayout
            };
            s.ValueChanged += (sender, e) => UpdatePreview();
            return s;
        }

        // --- LOGIC (UNTOUCHED) ---

        private void LoadMaterials()
        {
            _comboMaterial.Items.Clear();

            // Nur Metalle laden
            if (Densities.Metals != null)
            {
                foreach (var metal in Densities.Metals)
                {
                    _comboMaterial.Items.Add(metal.Name);
                }
            }

            // Default setzen (Gelbgold 750, falls vorhanden, sonst erster)
            if (_comboMaterial.Items.Count > 0)
            {
                int idx = -1;
                for (int i = 0; i < _comboMaterial.Items.Count; i++)
                {
                    if (_comboMaterial.Items[i].Text.Contains("Gelbgold")) idx = i;
                }
                _comboMaterial.SelectedIndex = idx >= 0 ? idx : 0;
            }

            // Initial Update triggern
            OnMaterialChanged(null, null);
        }

        private void OnMaterialChanged(object sender, EventArgs e)
        {
            string matName = _comboMaterial.SelectedValue?.ToString();
            if (!string.IsNullOrEmpty(matName))
            {
                var info = Densities.Get(matName);
                if (info != null)
                {
                    _bezelColor = info.DisplayColor;
                }
            }
            UpdatePreview();
        }

        private void OnPickColor(object sender, EventArgs e)
        {
            var dlg = new ColorDialog();
            dlg.Color = Eto.Drawing.Color.FromArgb(_bezelColor.R, _bezelColor.G, _bezelColor.B, _bezelColor.A);

            if (dlg.ShowDialog(this) == DialogResult.Ok)
            {
                var c = dlg.Color;
                _bezelColor = System.Drawing.Color.FromArgb((int)(c.A * 255), (int)(c.R * 255), (int)(c.G * 255), (int)(c.B * 255));
                UpdatePreview();
            }
        }

        private void OnSelectGem(object sender, EventArgs e)
        {
            var go = new GetObject();
            go.SetCommandPrompt("Select Gem to add Bezel");
            go.GeometryFilter = ObjectType.Brep | ObjectType.Mesh;
            go.EnablePreSelect(true, true);

            Visible = false;
            go.Get();
            Visible = true;

            if (go.CommandResult() == Rhino.Commands.Result.Success)
            {
                var obj = go.Object(0).Object();
                if (obj == null) return;

                if (RhinoGoldHelper.TryGetGemData(obj, out Curve c, out Plane p, out double size))
                {
                    _selectedGemId = obj.Id;
                    _gemCurve = c;
                    _gemPlane = p;
                    _gemSize = size;
                    UpdatePreview();
                }
                else
                {
                    MessageBox.Show("Selected object is not a recognized Gem.", "Error", MessageBoxButtons.OK, MessageBoxType.Error);
                }
            }
        }

        private void UpdatePreview()
        {
            if (_gemCurve == null) return;

            var param = new BezelParameters
            {
                Height = _numHeight.Value,
                ThicknessTop = _numThickTop.Value,
                ThicknessBottom = _numThickBottom.Value,
                Offset = _numOffset.Value,
                GemGap = _numGemGap.Value,
                ZOffset = _numZOffset.Value,
                SeatDepth = _numSeatDepth.Value,
                SeatLedge = _numSeatLedge.Value,
                Chamfer = _numChamfer.Value,
                Bombing = _numBombing.Value,
                CreateCutter = _chkCreateCutter.Checked == true
            };

            _tempBezel = BezelBuilder.CreateBezel(_gemCurve, _gemPlane, param);

            if (_tempBezel != null)
            {
                _previewConduit.setbreps(new[] { _tempBezel });
                // Farbe setzen
                _previewConduit.SetColor(_bezelColor);
                RhinoDoc.ActiveDoc.Views.Redraw();
            }
        }

        private void OnBuild(object sender, EventArgs e)
        {
            if (_tempBezel == null) return;
            var doc = RhinoDoc.ActiveDoc;

            uint undoSn = doc.BeginUndoRecord("Create Bezel");
            try
            {
                var attr = new ObjectAttributes();
                attr.Name = "SmartBezel";
                attr.SetUserString("RG BEZEL", "1");

                // Material & Farbe setzen
                string matName = _comboMaterial.SelectedValue?.ToString() ?? "Metal";
                attr.SetUserString("RG MATERIAL ID", matName);
                attr.ObjectColor = _bezelColor;
                attr.ColorSource = ObjectColorSource.ColorFromObject;

                var smartData = new BezelSmartData(
                    _numHeight.Value,
                    _numThickTop.Value,
                    _numOffset.Value,
                    _selectedGemId,
                    _gemPlane
                );

                _tempBezel.UserData.Add(smartData);
                doc.Objects.AddBrep(_tempBezel, attr);
            }
            finally
            {
                doc.EndUndoRecord(undoSn);
            }

            Close();
        }
    }
}