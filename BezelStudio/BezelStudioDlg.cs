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

        public BezelStudioDlg()
        {
            Title = "Bezel Studio";
            ClientSize = new Size(250, 380);
            Topmost = true;
            Resizable = false;
            Padding = new Padding(10);

            _previewConduit = new GemDisplayCond();

            Content = BuildLayout();
            LoadMaterials();

            Shown += (s, e) => _previewConduit.Enable();
            Closed += (s, e) => { _previewConduit.Disable(); RhinoDoc.ActiveDoc?.Views.Redraw(); };
        }

        private Control BuildLayout()
        {
            InitializeControls();

            var layout = new DynamicLayout { Padding = Padding.Empty, Spacing = new Size(5, 5) };

            layout.BeginVertical();
            layout.AddRow(_btnSelectGem);
            layout.AddRow(null);

            layout.AddRow(CreateHeader("Dimensions"));
            // Label geändert: Height ist wieder editierbar
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

            // CHANGE: Height is editable again
            _numHeight = CreateStepper(3.0);
            _numHeight.Enabled = true; // User can edit
            // _numHeight.BackgroundColor entfernt

            _numThickTop = CreateStepper(0.76);
            _numThickBottom = CreateStepper(0.76);
            _numOffset = CreateStepper(0.10);
            _numZOffset = CreateStepper(0.00); _numZOffset.MinValue = -100.0;
            _numSeatDepth = CreateStepper(0.66);
            _numSeatLedge = CreateStepper(0.74);
            _numChamfer = CreateStepper(0.0);
            _numBombing = CreateStepper(0.0);

            _comboMaterial = new ComboBox();
            _comboMaterial.SelectedIndexChanged += OnMaterialChanged;

            _btnOk = new Button { Text = "Build" };
            _btnOk.Click += OnBuild;

            _btnCancel = new Button { Text = "Close" };
            _btnCancel.Click += (s, e) => Close();
        }

        private Label CreateLabel(string text) => new Label { Text = text, VerticalAlignment = VerticalAlignment.Center };
        private Label CreateHeader(string text) => new Label { Text = text, Font = Eto.Drawing.Fonts.Sans(8, FontStyle.Bold), TextColor = Colors.Gray };

        private NumericStepper CreateStepper(double val)
        {
            var s = new NumericStepper { Value = val, DecimalPlaces = 2, Increment = 0.1 };
            s.ValueChanged += (sender, e) => UpdatePreview();
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
            UpdatePreview();
        }

        private void OnSelectGem(object sender, EventArgs e)
        {
            var go = new GetObject();
            go.SetCommandPrompt("Select Gem");
            go.GeometryFilter = ObjectType.Brep | ObjectType.Mesh | ObjectType.InstanceReference; // InstanceReference für Blocks
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

                    // Auto-Calculate Height suggestion (but editable)
                    var bbox = obj.Geometry.GetBoundingBox(p);
                    if (bbox.IsValid)
                        _numHeight.Value = (bbox.Max.Z - bbox.Min.Z) + 0.3;
                    else
                        _numHeight.Value = 3.5;

                    UpdatePreview();
                }
                else
                {
                    MessageBox.Show("Could not extract gem data. If this is a block, ensure it contains valid geometry.", "Error", MessageBoxButtons.OK, MessageBoxType.Error);
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

            _tempBezel = BezelBuilder.CreateBezel(_gemCurve, _gemPlane, param);

            if (_tempBezel != null)
            {
                _previewConduit.setbreps(new[] { _tempBezel });
                _previewConduit.SetColor(System.Drawing.Color.FromArgb(130, _bezelColor.R, _bezelColor.G, _bezelColor.B));
                RhinoDoc.ActiveDoc.Views.Redraw();
            }
        }

        private void OnBuild(object sender, EventArgs e)
        {
            if (_tempBezel == null) return;
            var doc = RhinoDoc.ActiveDoc;
            string undoName = _editingObjectId != Guid.Empty ? "Update Bezel" : "Create Bezel";
            uint undoSn = doc.BeginUndoRecord(undoName);
            try
            {
                var attr = new ObjectAttributes();
                attr.Name = "SmartBezel";
                attr.SetUserString("RG BEZEL", "1");
                string matName = _comboMaterial.SelectedValue?.ToString() ?? "Metal";
                attr.SetUserString("RG MATERIAL ID", matName);
                attr.ObjectColor = _bezelColor;
                attr.ColorSource = ObjectColorSource.ColorFromObject;

                var smartData = new BezelSmartData(
                    _numHeight.Value, _numThickTop.Value, _numThickBottom.Value,
                    _numOffset.Value, _numZOffset.Value, _numSeatDepth.Value,
                    _numSeatLedge.Value, _numChamfer.Value, _numBombing.Value,
                    _selectedGemId, _gemPlane
                );

                _tempBezel.UserData.Add(smartData);

                if (_editingObjectId != Guid.Empty)
                {
                    doc.Objects.Replace(_editingObjectId, _tempBezel);
                    var existingObj = doc.Objects.FindId(_editingObjectId);
                    if (existingObj != null)
                    {
                        existingObj.Attributes.ObjectColor = _bezelColor;
                        existingObj.Attributes.SetUserString("RG MATERIAL ID", matName);
                        existingObj.CommitChanges();
                    }
                }
                else
                {
                    doc.Objects.AddBrep(_tempBezel, attr);
                }
            }
            finally
            {
                doc.EndUndoRecord(undoSn);
            }
            Close();
        }

        public void LoadSmartData(NewRhinoGold.Core.BezelSmartData data, Guid objectId)
        {
            if (data == null) return;
            _editingObjectId = objectId;
            _btnOk.Text = "Update";

            _numHeight.Value = data.Height;
            _numThickTop.Value = data.ThicknessTop;
            _numThickBottom.Value = data.ThicknessBottom;
            _numOffset.Value = data.Offset;
            _numZOffset.Value = data.ZOffset;
            _numSeatDepth.Value = data.SeatDepth;
            _numSeatLedge.Value = data.SeatLedge;
            _numChamfer.Value = data.Chamfer;
            _numBombing.Value = data.Bombing;

            _selectedGemId = data.GemId;
            _gemPlane = data.GemPlane;

            var doc = RhinoDoc.ActiveDoc;
            if (doc != null)
            {
                var gemObj = doc.Objects.FindId(data.GemId);
                if (gemObj != null && NewRhinoGold.Helpers.RhinoGoldHelper.TryGetGemData(gemObj, out Rhino.Geometry.Curve c, out Rhino.Geometry.Plane p, out double s))
                {
                    _gemCurve = c;
                }
            }
            UpdatePreview();
        }
    }
}