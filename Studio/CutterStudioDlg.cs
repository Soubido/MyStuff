using System;
using System.Collections.Generic;
using System.Linq;
using Eto.Drawing;
using Eto.Forms;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using NewRhinoGold.Core;
using NewRhinoGold.BezelStudio;
using NewRhinoGold.Helpers;

namespace NewRhinoGold.Studio
{
    public class CutterStudioDlg : Form
    {
        // --- STATUS ---
        private Guid _editingObjectId = Guid.Empty;
        private List<GemSmartData> _selectedGems = new List<GemSmartData>();
        private CutterPreviewConduit _previewConduit;
        private bool _suspendUpdates = false;

        // --- UI CONTROLS ---
        private Button _btnSelectGems, _btnBuild, _btnClose;

        // FIX: Hier hat die Variable gefehlt!
        private Button _btnFlipZ;

        // Parameter
        private NumericStepper _numScale, _numClearance;
        private NumericStepper _numTopHeight, _numTopDia;
        private NumericStepper _numSeatPos;
        private NumericStepper _numBotHeight, _numBotDia;
        private NumericStepper _numProfileRot;

        // Shape Mode
        private RadioButton _rbShapeRound, _rbShapeLibrary;
        private GridView _gridLibrary;

        private TabControl _tabControl;

        public CutterStudioDlg()
        {
            Title = "Cutter Studio";
            ClientSize = new Size(380, 480); // Etwas höher für den neuen Button
            Topmost = true;
            Resizable = false;

            _previewConduit = new CutterPreviewConduit();

            Content = BuildLayout();

            Shown += (s, e) => { _previewConduit.Enabled = true; UpdatePreview(); };
            Closed += (s, e) => { _previewConduit.Enabled = false; RhinoDoc.ActiveDoc?.Views.Redraw(); };
        }

        private Control BuildLayout()
        {
            _btnSelectGems = new Button { Text = "Select Gems", Height = 28 };
            _btnSelectGems.Click += OnSelectGems;

            var topLayout = new TableLayout { Padding = new Padding(10, 10, 10, 5), Spacing = new Size(5, 5) };
            topLayout.Rows.Add(new TableRow(_btnSelectGems));

            _tabControl = new TabControl();
            _tabControl.Pages.Add(new TabPage { Text = "Parameters", Content = BuildParamsTab() });
            _tabControl.Pages.Add(new TabPage { Text = "Bottom Shape", Content = BuildShapeTab() });

            _btnBuild = new Button { Text = "Build Cutters", Height = 28, Font = Eto.Drawing.Fonts.Sans(9, FontStyle.Bold) };
            _btnBuild.Click += OnBuild;

            _btnClose = new Button { Text = "Close", Height = 28 };
            _btnClose.Click += (s, e) => Close();

            var actionsLayout = new TableLayout { Spacing = new Size(5, 0) };
            actionsLayout.Rows.Add(new TableRow(null, _btnClose, _btnBuild));

            var mainLayout = new TableLayout { Padding = 0, Spacing = new Size(0, 0) };
            mainLayout.Rows.Add(topLayout);
            mainLayout.Rows.Add(new TableRow(_tabControl) { ScaleHeight = true });
            mainLayout.Rows.Add(new TableRow(new Panel { BackgroundColor = Colors.LightGrey, Height = 1 }));
            mainLayout.Rows.Add(new TableRow(new Panel { Content = actionsLayout, Padding = 10 }));

            return mainLayout;
        }

        private Control BuildParamsTab()
        {
            _numScale = CreateStepper(100, 0);
            _numClearance = CreateStepper(0.10, 2);
            _numTopHeight = CreateStepper(100, 0);
            _numTopDia = CreateStepper(100, 0);
            _numSeatPos = CreateStepper(30, 0);
            _numBotHeight = CreateStepper(150, 0);
            _numBotDia = CreateStepper(70, 0);

            Control CreatePair(string l1, Control c1, string l2, Control c2)
            {
                var t = new TableLayout { Spacing = new Size(5, 2) };
                t.Rows.Add(new TableRow(
                    new Label { Text = l1, VerticalAlignment = VerticalAlignment.Center, Font = Eto.Drawing.Fonts.Sans(8) },
                    c1,
                    new Label { Text = l2, VerticalAlignment = VerticalAlignment.Center, Font = Eto.Drawing.Fonts.Sans(8) },
                    c2
                ));
                return t;
            }

            Control CreateSingle(string l1, Control c1)
            {
                var t = new TableLayout { Spacing = new Size(5, 2) };
                t.Rows.Add(new TableRow(
                    new Label { Text = l1, VerticalAlignment = VerticalAlignment.Center, Font = Eto.Drawing.Fonts.Sans(8) },
                    c1,
                    null
                ));
                return t;
            }

            var grpGlobal = new GroupBox { Text = "Global Settings", Padding = 5, Font = Eto.Drawing.Fonts.Sans(8, FontStyle.Bold) };
            var layGlobal = new TableLayout { Spacing = new Size(5, 5) };
            layGlobal.Rows.Add(CreatePair("Scale %:", _numScale, "Gap:", _numClearance));
            grpGlobal.Content = layGlobal;

            var grpTop = new GroupBox { Text = "Top Shaft (%)", Padding = 5, Font = Eto.Drawing.Fonts.Sans(8, FontStyle.Bold) };
            var layTop = new TableLayout { Spacing = new Size(5, 5) };
            layTop.Rows.Add(CreatePair("Height:", _numTopHeight, "Scale:", _numTopDia));
            grpTop.Content = layTop;

            var grpSeat = new GroupBox { Text = "Seat / Girdle", Padding = 5, Font = Eto.Drawing.Fonts.Sans(8, FontStyle.Bold) };
            var laySeat = new TableLayout { Spacing = new Size(5, 5) };
            laySeat.Rows.Add(CreateSingle("Seat Level %:", _numSeatPos));
            grpSeat.Content = laySeat;

            var grpBot = new GroupBox { Text = "Bottom Shaft (%)", Padding = 5, Font = Eto.Drawing.Fonts.Sans(8, FontStyle.Bold) };
            var layBot = new TableLayout { Spacing = new Size(5, 5) };
            layBot.Rows.Add(CreatePair("Height:", _numBotHeight, "Scale:", _numBotDia));
            grpBot.Content = layBot;

            var finalLayout = new StackLayout
            {
                Padding = 10,
                Spacing = 5,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Items = { grpGlobal, grpTop, grpSeat, grpBot, new StackLayoutItem(null, true) }
            };

            return new Scrollable { Content = finalLayout, Border = BorderType.None };
        }

        private Control BuildShapeTab()
        {
            _rbShapeRound = new RadioButton { Text = "Standard (Round)", Checked = true, Font = Eto.Drawing.Fonts.Sans(8) };
            _rbShapeLibrary = new RadioButton(_rbShapeRound) { Text = "Library Profile", Font = Eto.Drawing.Fonts.Sans(8) };

            _gridLibrary = new GridView { Height = 150, Enabled = false, Border = BorderType.Bezel };
            _gridLibrary.Columns.Add(new GridColumn
            {
                HeaderText = "Profile",
                Resizable = false,
                AutoSize = true,
                DataCell = new ImageTextCell
                {
                    ImageBinding = Binding.Property<HeadProfileItem, Image>(x => x.Preview),
                    TextBinding = Binding.Property<HeadProfileItem, string>(x => x.Name)
                }
            });

            _gridLibrary.DataStore = HeadProfileLibrary.GetProfileItems();
            _gridLibrary.SelectionChanged += (s, e) => UpdatePreview();

            var list = (List<HeadProfileItem>)_gridLibrary.DataStore;
            if (list != null && list.Count > 0) _gridLibrary.SelectRow(0);

            _numProfileRot = CreateStepper(0);

            // FIX: Button initialisieren
            _btnFlipZ = new Button { Text = "Flip Direction (Z)", Height = 24 };
            _btnFlipZ.Click += OnFlipZ;

            void UpdateEnabled()
            {
                _gridLibrary.Enabled = (_rbShapeLibrary.Checked == true);
                UpdatePreview();
            }
            _rbShapeRound.CheckedChanged += (s, e) => UpdateEnabled();
            _rbShapeLibrary.CheckedChanged += (s, e) => UpdateEnabled();

            var grpMode = new GroupBox { Text = "Shape Mode", Padding = 5, Font = Eto.Drawing.Fonts.Sans(8, FontStyle.Bold) };
            var modeLayout = new TableLayout { Spacing = new Size(5, 5) };
            modeLayout.Rows.Add(_rbShapeRound);
            modeLayout.Rows.Add(_rbShapeLibrary);
            grpMode.Content = modeLayout;

            var grpRot = new GroupBox { Text = "Adjustments", Padding = 5, Font = Eto.Drawing.Fonts.Sans(8, FontStyle.Bold) };
            var rotLayout = new TableLayout { Spacing = new Size(5, 5) };
            rotLayout.Rows.Add(new TableRow(new Label { Text = "Rotation:", VerticalAlignment = VerticalAlignment.Center, Font = Eto.Drawing.Fonts.Sans(8) }, _numProfileRot));

            // FIX: Button zum Layout hinzufügen
            rotLayout.Rows.Add(new TableRow(_btnFlipZ));

            grpRot.Content = rotLayout;

            var mainLayout = new TableLayout { Padding = 10, Spacing = new Size(5, 5) };
            mainLayout.Rows.Add(grpMode);
            mainLayout.Rows.Add(new Label { Text = "Library Selection (from 'Curves' folder):", Font = Eto.Drawing.Fonts.Sans(8) });
            mainLayout.Rows.Add(new TableRow(_gridLibrary) { ScaleHeight = true });
            mainLayout.Rows.Add(grpRot);

            return new Scrollable { Content = mainLayout, Border = BorderType.None };
        }

        private CutterParameters GetParameters()
        {
            var p = new CutterParameters();
            p.GlobalScale = _numScale.Value;
            p.Clearance = _numClearance.Value;
            p.TopHeight = _numTopHeight.Value;
            p.TopDiameterScale = _numTopDia.Value;
            p.SeatLevel = _numSeatPos.Value;
            p.BottomHeight = _numBotHeight.Value;
            p.BottomDiameterScale = _numBotDia.Value;

            p.UseCustomProfile = (_rbShapeLibrary.Checked == true);

            if (p.UseCustomProfile && _gridLibrary.SelectedItem is HeadProfileItem item)
                p.ProfileName = item.Name;

            p.ProfileRotation = _numProfileRot.Value;
            return p;
        }

        // --- EVENTS ---

        // NEU: Der Flip Event Handler
        private void OnFlipZ(object sender, EventArgs e)
        {
            if (_selectedGems.Count == 0) return;

            foreach (var gemData in _selectedGems)
            {
                var p = gemData.GemPlane;
                p.Flip();
                gemData.GemPlane = p;
            }
            UpdatePreview();
        }

        private void OnSelectGems(object sender, EventArgs e)
        {
            this.Visible = false;
            try
            {
                var go = new Rhino.Input.Custom.GetObject();
                go.SetCommandPrompt("Select Gems");
                go.GeometryFilter = ObjectType.Brep | ObjectType.Mesh;
                go.GetMultiple(1, 0);

                if (go.CommandResult() == Rhino.Commands.Result.Success)
                {
                    _selectedGems.Clear();
                    foreach (var objRef in go.Objects())
                    {
                        var rhinoObj = objRef.Object();
                        if (rhinoObj == null) continue;

                        GemSmartData finalData = null;

                        // 1. Suche in Attributen
                        finalData = rhinoObj.Attributes.UserData.Find(typeof(GemSmartData)) as GemSmartData;

                        // Fallback: Geometrie
                        if (finalData == null)
                        {
                            finalData = rhinoObj.Geometry.UserData.Find(typeof(GemSmartData)) as GemSmartData;
                        }

                        // 2. Fallback: Namens-Check
                        if (finalData == null)
                        {
                            string objName = rhinoObj.Attributes.Name ?? "";
                            if (NewRhinoGold.Helpers.SelectionHelpers.IsGem(rhinoObj) ||
                                objName.IndexOf("gem", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                if (RhinoGoldHelper.TryGetGemData(rhinoObj, out Curve c, out Plane p, out double s))
                                {
                                    finalData = new GemSmartData(c, p, "Reconstructed", s, "Unknown", 0);
                                }
                            }
                        }

                        // 3. Daten reparieren (0-Werte)
                        if (finalData != null)
                        {
                            if (finalData.CrownHeightPercent < 0.001 && finalData.PavilionHeightPercent < 0.001)
                            {
                                finalData.TablePercent = 55.0;
                                finalData.CrownHeightPercent = 15.0;
                                finalData.GirdleThicknessPercent = 3.0;
                                finalData.PavilionHeightPercent = 43.0;
                            }
                            _selectedGems.Add(finalData);
                        }
                    }
                    UpdatePreview();
                }
            }
            finally
            {
                this.Visible = true;
            }
        }

        // --- HELPERS ---

        private Brep EnsureQualityBrep(Brep rawBrep)
        {
            if (rawBrep == null) return null;

            // FIX: .Faces.SplitKinkyFaces statt .SplitKinkyFaces
            rawBrep.Faces.SplitKinkyFaces(RhinoMath.DefaultAngleTolerance, true);

            // Versuchen zu schließen
            if (!rawBrep.IsSolid)
            {
                var capped = rawBrep.CapPlanarHoles(RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);
                if (capped != null) rawBrep = capped;
            }

            // Normals checken
            if (rawBrep.IsSolid && rawBrep.SolidOrientation == BrepSolidOrientation.Inward)
            {
                rawBrep.Flip();
            }

            // Aufräumen
            rawBrep.MergeCoplanarFaces(RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);

            return rawBrep;
        }

        private void OnBuild(object sender, EventArgs e)
        {
            var p = GetParameters();
            var doc = RhinoDoc.ActiveDoc;
            uint sn = doc.BeginUndoRecord("Create/Update Cutter");

            foreach (var gem in _selectedGems)
            {
                var parts = CutterBuilder.CreateCutter(gem, p);
                if (parts == null) continue;

                // Bereinigen für Booleans
                for (int i = 0; i < parts.Count; i++)
                {
                    parts[i] = EnsureQualityBrep(parts[i]);
                }
                parts.RemoveAll(x => x == null);

                if (parts.Count == 0) continue;

                var cutterData = new CutterSmartData(p, Guid.Empty);

                if (_editingObjectId != Guid.Empty)
                {
                    // Update Mode
                    var mainPart = parts[0];
                    mainPart.UserData.Add(cutterData);
                    doc.Objects.Replace(_editingObjectId, mainPart);

                    for (int i = 1; i < parts.Count; i++)
                    {
                        var attr = doc.CreateDefaultAttributes();
                        attr.Name = "RG Cutter";
                        attr.ObjectColor = System.Drawing.Color.OrangeRed;
                        attr.ColorSource = ObjectColorSource.ColorFromObject;
                        parts[i].UserData.Add(cutterData);
                        doc.Objects.AddBrep(parts[i], attr);
                    }
                }
                else
                {
                    // New Mode
                    foreach (var brep in parts)
                    {
                        var attr = doc.CreateDefaultAttributes();
                        attr.Name = "RG Cutter";
                        attr.ObjectColor = System.Drawing.Color.OrangeRed;
                        attr.ColorSource = ObjectColorSource.ColorFromObject;
                        brep.UserData.Add(cutterData);
                        doc.Objects.AddBrep(brep, attr);
                    }
                }
            }
            doc.EndUndoRecord(sn);
            doc.Views.Redraw();
            Close();
        }

        private void UpdatePreview()
        {
            if (_suspendUpdates) return;
            if (_selectedGems.Count == 0)
            {
                _previewConduit.SetBreps(null);
                RhinoDoc.ActiveDoc.Views.Redraw();
                return;
            }

            var p = GetParameters();
            var previewBreps = new List<Brep>();

            foreach (var gem in _selectedGems)
            {
                var parts = CutterBuilder.CreateCutter(gem, p);
                if (parts != null)
                {
                    foreach (var part in parts)
                    {
                        var clean = EnsureQualityBrep(part);
                        if (clean != null) previewBreps.Add(clean);
                    }
                }
            }

            _previewConduit.SetBreps(previewBreps);
            RhinoDoc.ActiveDoc.Views.Redraw();
        }

        private NumericStepper CreateStepper(double v, int d = 0)
        {
            var s = new NumericStepper { Value = v, DecimalPlaces = d, Width = 60, Font = Eto.Drawing.Fonts.Sans(8) };
            s.ValueChanged += (o, e) => UpdatePreview();
            return s;
        }

        public void LoadSmartData(CutterSmartData data, Guid objectId)
        {
            if (data == null) return;
            _editingObjectId = objectId;
            _btnBuild.Text = "Update";

            _suspendUpdates = true;
            _numScale.Value = data.GlobalScale;
            _numClearance.Value = data.Clearance;
            _numTopHeight.Value = data.TopHeight;
            _numTopDia.Value = data.TopDiameterScale;
            _numSeatPos.Value = data.SeatLevel;
            _numBotHeight.Value = data.BottomHeight;
            _numBotDia.Value = data.BottomDiameterScale;

            _numProfileRot.Value = data.ProfileRotation;
            _rbShapeLibrary.Checked = data.UseCustomProfile;
            _rbShapeRound.Checked = !data.UseCustomProfile;

            if (data.UseCustomProfile && !string.IsNullOrEmpty(data.ProfileName))
            {
                var list = (List<HeadProfileItem>)_gridLibrary.DataStore;
                if (list != null)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (list[i].Name == data.ProfileName) { _gridLibrary.SelectRow(i); break; }
                    }
                }
            }

            var doc = RhinoDoc.ActiveDoc;
            if (data.GemId != Guid.Empty)
            {
                var gemObj = doc.Objects.FindId(data.GemId);
                if (gemObj != null)
                {
                    if (RhinoGoldHelper.TryGetGemData(gemObj, out Curve c, out Plane pl, out double s))
                    {
                        _selectedGems.Clear();
                        _selectedGems.Add(new GemSmartData(c, pl, "Unknown", s, "Def", 0));
                    }
                }
            }

            _suspendUpdates = false;
            UpdatePreview();
        }
    }

    public class CutterPreviewConduit : Rhino.Display.DisplayConduit
    {
        private List<Brep> _breps;
        private System.Drawing.Color _color = System.Drawing.Color.OrangeRed;

        public void SetBreps(List<Brep> breps)
        {
            _breps = breps;
        }

        protected override void CalculateBoundingBox(Rhino.Display.CalculateBoundingBoxEventArgs e)
        {
            base.CalculateBoundingBox(e);
            if (_breps != null)
            {
                foreach (var b in _breps)
                    e.IncludeBoundingBox(b.GetBoundingBox(false));
            }
        }

        protected override void PostDrawObjects(Rhino.Display.DrawEventArgs e)
        {
            base.PostDrawObjects(e);
            if (_breps != null)
            {
                var mat = new Rhino.Display.DisplayMaterial(_color, 0.6);
                foreach (var b in _breps)
                    e.Display.DrawBrepShaded(b, mat);
            }
        }
    }
}