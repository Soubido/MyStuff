using System;
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

        // Data
        private Guid _editingObjectId = Guid.Empty;
        private Guid _selectedGemId = Guid.Empty;
        private Curve _gemCurve;
        private Plane _gemPlane;

        // UI
        private Button _btnSelectGem;

        // Params
        private NumericStepper _numHeight;
        private NumericStepper _numThickTop;
        private NumericStepper _numThickBottom;
        private NumericStepper _numOffset;
        private NumericStepper _numZOffset;
        private NumericStepper _numSeatDepth;
        private NumericStepper _numSeatLedge;
        private NumericStepper _numChamfer;
        private NumericStepper _numBombing;

        private ComboBox _comboMaterial;
        private System.Drawing.Color _bezelColor = System.Drawing.Color.Gold;

        private Button _btnOk;
        private Button _btnCancel;

        private Brep _tempBezel = null;

        // WICHTIG: Flag verhindert unnötige Neuberechnung beim Laden von Daten
        private bool _isUpdatingUI = false;

        public BezelStudioDlg()
        {
            Title = "Bezel Studio";
            ClientSize = new Size(250, 420); // Etwas höher für bessere Lesbarkeit
            Topmost = true;
            Resizable = false;
            Padding = new Padding(10);

            _previewConduit = new GemDisplayCond();

            Content = BuildLayout();
            LoadMaterials();

            Shown += (s, e) => _previewConduit.Enable();
            Closed += (s, e) => {
                _previewConduit.Disable();
                RhinoDoc.ActiveDoc?.Views.Redraw();
            };
        }

        private Control BuildLayout()
        {
            InitializeControls();

            var layout = new DynamicLayout { Padding = Padding.Empty, Spacing = new Size(5, 5) };

            layout.BeginVertical();
            layout.AddRow(_btnSelectGem);
            layout.AddRow(null);

            layout.AddRow(CreateHeader("Dimensions"));
            layout.AddRow(CreateLabel("Total Height:"), _numHeight);
            layout.AddRow(CreateLabel("Thickness Top:"), _numThickTop);
            layout.AddRow(CreateLabel("Thickness Bottom:"), _numThickBottom);
            layout.AddRow(null);

            layout.AddRow(CreateHeader("Offsets"));
            layout.AddRow(CreateLabel("Gem Gap:"), _numOffset);
            layout.AddRow(CreateLabel("Z-Offset:"), _numZOffset);
            layout.AddRow(null);

            layout.AddRow(CreateHeader("Seat Settings"));
            layout.AddRow(CreateLabel("Seat Depth:"), _numSeatDepth);
            layout.AddRow(CreateLabel("Seat Ledge:"), _numSeatLedge);
            layout.AddRow(null);

            layout.AddRow(CreateHeader("Features"));
            layout.AddRow(CreateLabel("Tapering (Chamfer):"), _numChamfer);
            layout.AddRow(CreateLabel("Curvature (Bombing):"), _numBombing);
            layout.AddRow(null);

            layout.AddRow(CreateHeader("Material"));
            layout.AddRow(CreateLabel("Alloy:"), _comboMaterial);

            layout.Add(null);

            var buttonGrid = new TableLayout
            {
                Spacing = new Size(10, 0),
                Rows = { new TableRow(new TableCell(_btnOk, true), new TableCell(_btnCancel, true)) }
            };
            layout.AddRow(buttonGrid);
            layout.EndVertical();
            return layout;
        }

        private void InitializeControls()
        {
            _btnSelectGem = new Button { Text = "Select Gem" };
            _btnSelectGem.Click += OnSelectGem;

            _numHeight = CreateStepper(3.5);
            _numThickTop = CreateStepper(0.8);
            _numThickBottom = CreateStepper(0.8);
            _numOffset = CreateStepper(0.10);
            _numZOffset = CreateStepper(0.00); _numZOffset.MinValue = -100.0;
            _numSeatDepth = CreateStepper(0.66);
            _numSeatLedge = CreateStepper(0.70); // Typischer Standardwert
            _numChamfer = CreateStepper(0.0);
            _numBombing = CreateStepper(0.0);

            _comboMaterial = new ComboBox();
            _comboMaterial.SelectedIndexChanged += OnMaterialChanged;

            _btnOk = new Button { Text = "Build" };
            _btnOk.Click += OnBuild;

            _btnCancel = new Button { Text = "Close" };
            _btnCancel.Click += (s, e) => Close();
        }

        private Label CreateLabel(string text) => new Label { Text = text, VerticalAlignment = VerticalAlignment.Center, Font = Eto.Drawing.Fonts.Sans(8) };
        private Label CreateHeader(string text) => new Label { Text = text, Font = Eto.Drawing.Fonts.Sans(8, FontStyle.Bold), TextColor = Colors.DimGray };

        private NumericStepper CreateStepper(double val)
        {
            var s = new NumericStepper { Value = val, DecimalPlaces = 2, Increment = 0.1, Width = 70 };
            // WICHTIG: Nur updaten, wenn wir nicht gerade Werte laden
            s.ValueChanged += (sender, e) => { if (!_isUpdatingUI) UpdatePreview(); };
            return s;
        }

        private void LoadMaterials()
        {
            _comboMaterial.Items.Clear();
            if (Densities.Metals != null)
                foreach (var metal in Densities.Metals) _comboMaterial.Items.Add(metal.Name);

            if (_comboMaterial.Items.Count > 0) _comboMaterial.SelectedIndex = 0;
            OnMaterialChanged(null, null);
        }

        private void OnMaterialChanged(object sender, EventArgs e)
        {
            string matName = _comboMaterial.SelectedValue?.ToString();
            if (!string.IsNullOrEmpty(matName))
            {
                var info = Densities.Get(matName);
                if (info != null) _bezelColor = info.DisplayColor;
            }
            if (!_isUpdatingUI) UpdatePreview();
        }

        private void OnSelectGem(object sender, EventArgs e)
        {
            var go = new GetObject();
            go.SetCommandPrompt("Select Gem");
            go.GeometryFilter = ObjectType.Brep | ObjectType.Mesh | ObjectType.InstanceReference;
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

                    // Auto-Höhe vorschlagen, wenn wir noch nicht manuell editieren
                    var bbox = obj.Geometry.GetBoundingBox(p);
                    if (bbox.IsValid)
                    {
                        bool wasUpdating = _isUpdatingUI;
                        _isUpdatingUI = true;
                        _numHeight.Value = (bbox.Max.Z - bbox.Min.Z) + 0.5; // +0.5mm Sicherheit
                        _isUpdatingUI = wasUpdating;
                    }
                    UpdatePreview();
                }
                else
                {
                    MessageBox.Show("Invalid Gem Geometry. Please select a valid Gem.", "Error", MessageBoxButtons.OK, MessageBoxType.Error);
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
                ZOffset = _numZOffset.Value,
                SeatDepth = _numSeatDepth.Value,
                SeatLedge = _numSeatLedge.Value,
                Chamfer = _numChamfer.Value,
                Bombing = _numBombing.Value
            };

            // Hier wird der Builder aufgerufen (Stellen Sie sicher, dass BezelBuilder.CreateBezel existiert)
            _tempBezel = BezelBuilder.CreateBezel(_gemCurve, _gemPlane, param);

            if (_tempBezel != null)
            {
                _previewConduit.setbreps(new[] { _tempBezel });
                _previewConduit.SetColor(System.Drawing.Color.FromArgb(150, _bezelColor.R, _bezelColor.G, _bezelColor.B));
                RhinoDoc.ActiveDoc.Views.Redraw();
            }
        }

        private void OnBuild(object sender, EventArgs e)
        {
            if (_tempBezel == null) return;
            var doc = RhinoDoc.ActiveDoc;

            // 1. SmartData erstellen
            var smartData = new BezelSmartData(
                _numHeight.Value, _numThickTop.Value, _numThickBottom.Value,
                _numOffset.Value, _numZOffset.Value, _numSeatDepth.Value,
                _numSeatLedge.Value, _numChamfer.Value, _numBombing.Value,
                _selectedGemId, _gemPlane
            );

            // 2. Alte UserData entfernen (falls vorhanden) und neue hinzufügen
            // Das ist wichtig, um saubere Daten zu haben
            var existingData = _tempBezel.UserData.Find(typeof(BezelSmartData));
            if (existingData != null) _tempBezel.UserData.Remove(existingData);

            _tempBezel.UserData.Add(smartData);

            string undoName = _editingObjectId != Guid.Empty ? "Update Bezel" : "Create Bezel";
            uint undoSn = doc.BeginUndoRecord(undoName);
            try
            {
                // Attribute vorbereiten
                var attr = new ObjectAttributes();
                attr.Name = "SmartBezel";
                attr.SetUserString("RG BEZEL", "1");
                string matName = _comboMaterial.SelectedValue?.ToString() ?? "Metal";
                attr.SetUserString("RG MATERIAL ID", matName);
                attr.ObjectColor = _bezelColor;
                attr.ColorSource = ObjectColorSource.ColorFromObject;

                if (_editingObjectId != Guid.Empty)
                {
                    // UPDATE LOGIK
                    // Replace tauscht Geometrie + UserData aus, behält aber Layer/Farbe des alten Objekts.
                    if (doc.Objects.Replace(_editingObjectId, _tempBezel))
                    {
                        // Wir müssen die Attribute des 'alten' (jetzt ersetzten) Objekts aktualisieren,
                        // falls der User das Material geändert hat.
                        var obj = doc.Objects.FindId(_editingObjectId);
                        if (obj != null)
                        {
                            obj.Attributes.ObjectColor = _bezelColor;
                            obj.Attributes.SetUserString("RG MATERIAL ID", matName);
                            obj.CommitChanges(); // WICHTIG: Änderungen anwenden
                        }
                    }
                    else
                    {
                        // Fallback, falls Objekt gelöscht wurde
                        doc.Objects.AddBrep(_tempBezel, attr);
                    }
                }
                else
                {
                    // NEU ERSTELLEN
                    doc.Objects.AddBrep(_tempBezel, attr);
                }
            }
            finally
            {
                doc.EndUndoRecord(undoSn);
            }
            Close();
        }

        public void LoadSmartData(BezelSmartData data, Guid objectId)
        {
            if (data == null) return;
            _editingObjectId = objectId;
            _btnOk.Text = "Update";
            Title = "Edit Bezel";

            // WICHTIG: UI Updates blockieren, damit nicht 10x gerechnet wird
            _isUpdatingUI = true;

            try
            {
                _numHeight.Value = data.Height;
                _numThickTop.Value = data.ThicknessTop;

                // Fallback für alte Daten (sicherheitshalber)
                _numThickBottom.Value = data.ThicknessBottom > 0 ? data.ThicknessBottom : data.ThicknessTop;

                _numOffset.Value = data.Offset;
                _numZOffset.Value = data.ZOffset;
                _numSeatDepth.Value = data.SeatDepth;
                _numSeatLedge.Value = data.SeatLedge;
                _numChamfer.Value = data.Chamfer;
                _numBombing.Value = data.Bombing;

                _selectedGemId = data.GemId;
                _gemPlane = data.GemPlane;

                // Versuch, die Kurve wiederherzustellen
                var doc = RhinoDoc.ActiveDoc;
                if (doc != null)
                {
                    var gemObj = doc.Objects.FindId(data.GemId);
                    if (gemObj != null)
                    {
                        if (RhinoGoldHelper.TryGetGemData(gemObj, out Curve c, out Plane p, out double s))
                        {
                            _gemCurve = c;
                            // Optional: Plane aktualisieren, falls der Stein bewegt wurde
                            // _gemPlane = p; 
                        }
                    }
                }

                // Material auslesen (Attribut UserString)
                if (doc != null)
                {
                    var obj = doc.Objects.FindId(objectId);
                    if (obj != null)
                    {
                        string matId = obj.Attributes.GetUserString("RG MATERIAL ID");
                        if (!string.IsNullOrEmpty(matId))
                        {
                            _comboMaterial.SelectedValue = matId; // Setzt Combo Box
                        }
                    }
                }
            }
            finally
            {
                _isUpdatingUI = false;
            }

            // Einmalig updaten am Ende
            UpdatePreview();
        }
    }
}