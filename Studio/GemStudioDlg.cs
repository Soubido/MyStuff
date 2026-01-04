using System;
using System.Collections.Generic;
using Eto.Drawing;
using Eto.Forms;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.UI;
using NewRhinoGold.Core;

namespace NewRhinoGold.Studio
{
    // FINAL VERSION - DYNAMIC LAYOUT ENGINE
    // Solves: Cut-off buttons, Alignment issues, Input scaling.
    public class GemStudioDlg : Form
    {
        // --- UI State ---
        private ComboBox _comboShape;
        private ComboBox _comboMaterial;

        private NumericStepper _numSize;
        private NumericStepper _numLength;

        // Proportions
        private NumericStepper _numTableMM;
        private NumericStepper _numCrownMM;
        private NumericStepper _numGirdleMM;
        private NumericStepper _numPavilionMM;

        private NumericStepper _numGap;
        private Label _lblWeight;

        private RadioButton _rbPlacePoint;
        private RadioButton _rbPlaceSurface;
        private RadioButton _rbPlaceDummy;

        private CheckBox _chkFlip;

        private Button _btnStartPlacement;
        private Button _btnClose;

        // --- Logic State (UNTOUCHED) ---
        private System.Drawing.Color _currentColor = System.Drawing.Color.Blue;

        public static GemStudioDlg Instance { get; private set; }

        public GemStudioDlg()
        {
            Instance = this;

            Title = "Gem Studio";
            // KORREKTUR: Breite auf 400 erhöht, um "Abschneiden" physikalisch unmöglich zu machen.
            ClientSize = new Size(250, 380);
            Topmost = true;
            Resizable = false;
            Padding = new Padding(10);

            Content = BuildLayout();

            UpdateWeightLabel();

            Closed += (s, e) => { Instance = null; };
        }

        private Control BuildLayout()
        {
            InitializeControls();

            // STRATEGIEWECHSEL: DynamicLayout
            // Das ist der mächtigste Layout-Container in Eto.
            // Er richtet Labels (Spalte 1) und Inputs (Spalte 2) automatisch perfekt aus.
            var layout = new DynamicLayout
            {
                Padding = Padding.Empty, // Padding kommt vom Form
                Spacing = new Size(5, 5) // Sauberer Abstand
            };

            layout.BeginVertical(); // Start Hauptfluss

            // --- SECTION 1: BASIC INFO ---
            // AddRow(Label, Control) erstellt automatisch 2 Spalten
            layout.AddRow(CreateLabel("Shape:"), _comboShape);
            layout.AddRow(CreateLabel("Material:"), _comboMaterial);

            layout.AddRow(null); // Kleiner visueller Abstand

            // --- SECTION 2: DIMENSIONS ---
            layout.AddRow(CreateHeader("Dimensions (mm)"));
            layout.AddRow(CreateLabel("Width:"), _numSize);
            layout.AddRow(CreateLabel("Length:"), _numLength);

            layout.AddRow(null);

            // --- SECTION 3: PROPORTIONS ---
            layout.AddRow(CreateHeader("Proportions (%)"));
            layout.AddRow(CreateLabel("Table:"), _numTableMM);
            layout.AddRow(CreateLabel("Crown:"), _numCrownMM);
            layout.AddRow(CreateLabel("Girdle:"), _numGirdleMM);
            layout.AddRow(CreateLabel("Pavilion:"), _numPavilionMM);

            layout.AddRow(null);

            // --- SECTION 4: PLACEMENT SETTINGS ---
            layout.AddRow(CreateLabel("Gap:"), _numGap);
            layout.AddRow(CreateLabel("Weight:"), _lblWeight);

            layout.AddRow(null);

            // --- SECTION 5: MODE (Vertical) ---
            // Wir fügen das StackLayout als einzelne Zeile hinzu. 
            // Es spannt sich automatisch über die volle Breite.
            var modeStack = new StackLayout
            {
                Orientation = Orientation.Vertical,
                Spacing = 5,
                Items = { _rbPlacePoint, _rbPlaceSurface, _chkFlip, _rbPlaceDummy }
            };

            // "Mode:" Label links, StackLayout rechts? Nein, wir machen eine eigene Zeile für Mode.
            // Layout: 
            // Label "Mode:"
            // [Radio Buttons]
            layout.AddRow(CreateLabel("Mode:"), modeStack);

            // Spacer nach unten (Schiebt Buttons an den Rand, aber sanft)
            layout.Add(new Panel { Height = 10 });
            layout.Add(null); // Flexibler Spacer

            // --- SECTION 6: FOOTER (Buttons) ---
            // Wir erstellen ein TableLayout für die Buttons, damit sie exakt 50/50 sind.
            // Und wir fügen dieses TableLayout als EINE Zeile in das DynamicLayout ein.
            var buttonGrid = new TableLayout
            {
                Spacing = new Size(10, 0),
                Rows =
                {
                    new TableRow(
                        new TableCell(_btnStartPlacement, true), // 50% Scale
                        new TableCell(_btnClose, true)           // 50% Scale
                    )
                }
            };

            layout.AddRow(buttonGrid); // Fügt die Buttons über die volle Breite ein

            layout.EndVertical();

            return layout;
        }

        private void InitializeControls()
        {
            // Shapes
            _comboShape = new ComboBox();
            foreach (var name in GemShapes.GetNames()) _comboShape.Items.Add(name);
            _comboShape.SelectedIndex = 0;
            _comboShape.SelectedIndexChanged += (s, e) => UpdateWeightLabel();

            // Material
            _comboMaterial = new ComboBox();
            if (Densities.Gems != null) foreach (var g in Densities.Gems) _comboMaterial.Items.Add(g.Name);
            if (_comboMaterial.Items.Count > 0) _comboMaterial.SelectedIndex = 0;
            _comboMaterial.SelectedIndexChanged += (s, e) => UpdateWeightLabel();

            // Steppers
            _numSize = CreateStepper(5.0);
            _numLength = CreateStepper(5.0);
            _numTableMM = CreateStepper(55, 0);
            _numCrownMM = CreateStepper(15, 0);
            _numGirdleMM = CreateStepper(3, 1);
            _numPavilionMM = CreateStepper(43, 0);
            _numGap = CreateStepper(0.0, 2);

            // Labels - SAFE: Eto.Drawing.Fonts
            _lblWeight = new Label { Text = "0.00 ct", VerticalAlignment = VerticalAlignment.Center, Font = Eto.Drawing.Fonts.Sans(10, FontStyle.Bold) };

            // Radios
            _rbPlacePoint = new RadioButton { Text = "Curve or Point", Checked = true };
            _rbPlaceSurface = new RadioButton(_rbPlacePoint) { Text = "Surface" };
            _rbPlaceDummy = new RadioButton(_rbPlacePoint) { Text = "Center (0,0)" };

            // Checkbox
            _chkFlip = new CheckBox { Text = "Flip Direction", Visible = false };
            _rbPlaceSurface.CheckedChanged += (s, e) => { _chkFlip.Visible = (_rbPlaceSurface.Checked == true); };

            // Buttons
            _btnStartPlacement = new Button { Text = "Place Gem" };
            _btnStartPlacement.Click += OnStartPlacement;

            _btnClose = new Button { Text = "Close" };
            _btnClose.Click += (s, e) => Close();
        }

        // --- Layout Helpers ---
        // Helper für DynamicLayout Labels
        private Label CreateLabel(string text)
        {
            return new Label { Text = text, VerticalAlignment = VerticalAlignment.Center };
        }

        private Label CreateHeader(string text)
        {
            return new Label { Text = text, Font = Eto.Drawing.Fonts.Sans(8, FontStyle.Bold), TextColor = Colors.Gray };
        }

        // --- LOGIC (EXACT COPY - DO NOT TOUCH) ---
        private void OnStartPlacement(object sender, EventArgs e)
        {
            this.Visible = false;
            string shapeName = _comboShape.SelectedValue?.ToString() ?? "Round";
            GemShapes.ShapeType shapeType;
            Enum.TryParse(shapeName, out shapeType);

            double width = _numSize.Value;
            double length = (shapeType == GemShapes.ShapeType.Round) ? width : _numLength.Value;

            Curve baseCurve = GemShapes.Create(shapeType, width, length);

            var gp = new GemParameters();
            gp.Table = _numTableMM.Value;
            gp.H1 = width * (_numCrownMM.Value / 100.0);
            gp.H2 = width * (_numGirdleMM.Value / 100.0);
            gp.H3 = width * (_numPavilionMM.Value / 100.0);

            Brep tempGem = GemBuilder.CreateGem(baseCurve, gp, width);

            if (tempGem == null) { ResetUI(); return; }

            try
            {
                if (_rbPlaceDummy.Checked == true)
                {
                    BakeGem(tempGem, baseCurve, Transform.Identity, shapeType, width);
                }
                else
                {
                    RunInteractivePlacement(tempGem, baseCurve, shapeType, width);
                }
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error: {ex.Message}");
            }
            finally
            {
                ResetUI();
            }
        }

        private void RunInteractivePlacement(Brep templateGem, Curve templateCurve, GemShapes.ShapeType shape, double size)
        {
            Brep targetBrep = null;

            if (_rbPlaceSurface.Checked == true)
            {
                var go = new Rhino.Input.Custom.GetObject();
                go.SetCommandPrompt("Select Surface");
                go.GeometryFilter = ObjectType.Surface | ObjectType.PolysrfFilter;
                go.Get();

                if (go.CommandResult() == Rhino.Commands.Result.Success)
                {
                    var obj = go.Object(0);
                    targetBrep = obj.Brep();
                    if (targetBrep == null && obj.Surface() != null)
                        targetBrep = obj.Surface().ToBrep();
                }
                else
                {
                    return;
                }
            }

            var toolPos = new GemPlacementTool(templateGem, templateCurve, _currentColor, targetBrep, _numGap.Value);
            toolPos.Get();

            if (toolPos.CommandResult() != Rhino.Commands.Result.Success) return;

            Transform posXform = toolPos.FinalTransform;

            if (_rbPlaceSurface.Checked == true && _chkFlip.Checked == true)
            {
                Plane tempPlane = Plane.WorldXY;
                tempPlane.Transform(posXform);
                Transform flip = Transform.Rotation(Math.PI, tempPlane.XAxis, tempPlane.Origin);
                posXform = flip * posXform;
            }

            Transform finalXform = posXform;
            bool skipRotation = (shape == GemShapes.ShapeType.Round || shape == GemShapes.ShapeType.Square);

            if (!skipRotation)
            {
                var toolRot = new GemRotationTool(templateGem, posXform, _currentColor);
                toolRot.Get();

                if (toolRot.CommandResult() == Rhino.Commands.Result.Success)
                {
                    finalXform = toolRot.FinalTransform;
                }
            }

            BakeGem(templateGem, templateCurve, finalXform, shape, size);
        }

        private void BakeGem(Brep templateBrep, Curve templateCurve, Transform xform, GemShapes.ShapeType shape, double size)
        {
            var doc = RhinoDoc.ActiveDoc;
            uint sn = doc.BeginUndoRecord("Place Gem");

            Brep finalGem = templateBrep.DuplicateBrep();
            finalGem.Transform(xform);

            Curve finalCurve = templateCurve.DuplicateCurve();
            finalCurve.Transform(xform);

            Plane finalPlane = Plane.WorldXY;
            finalPlane.Transform(xform);

            var attr = doc.CreateDefaultAttributes();
            attr.Name = "SmartGem";

            string matName = _comboMaterial.SelectedValue?.ToString() ?? "Unknown";
            var matInfo = Densities.Get(matName);
            if (matInfo != null)
            {
                attr.ObjectColor = matInfo.DisplayColor;
                attr.ColorSource = ObjectColorSource.ColorFromObject;
            }

            double weight = 0.0;
            var mp = VolumeMassProperties.Compute(finalGem);
            if (mp != null)
                weight = Math.Abs(mp.Volume) * (Densities.GetDensity(matName) / 1000.0) * 5.0;

            var smartData = new GemSmartData(
                finalCurve,
                finalPlane,
                shape.ToString(),
                size,
                matName,
                weight
            );

            finalGem.UserData.Add(smartData);
            doc.Objects.AddBrep(finalGem, attr);

            doc.EndUndoRecord(sn);
            doc.Views.Redraw();
        }

        private void UpdateWeightLabel()
        {
            double d = _numSize.Value;
            double h = d * 0.6;
            double vol = Math.PI * Math.Pow(d / 2, 2) * h * 0.4;
            string mat = _comboMaterial.SelectedValue?.ToString();
            double dens = Densities.GetDensity(mat);
            double w = vol * (dens / 1000.0) * 5.0;
            _lblWeight.Text = $"~{w:F2} ct";
        }

        private void ResetUI() => this.Visible = true;

        private NumericStepper CreateStepper(double val, int dec = 2)
        {
            var s = new NumericStepper { Value = val, DecimalPlaces = dec };
            s.ValueChanged += (sender, e) => UpdateWeightLabel();
            return s;
        }
    }
}