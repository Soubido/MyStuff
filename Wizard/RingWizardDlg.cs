using Eto.Drawing;
using Eto.Forms;
using NewRhinoGold.Core;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.UI;
using Rhino.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using NewRhinoGold.Helpers;

namespace NewRhinoGold.Wizard
{
    public class ProfileListItem
    {
        public string Text { get; set; }
        public Image Image { get; set; }
        public string Key => Text;
    }

    public class RingWizardDlg : Form
    {
        private readonly RingDesignManager _manager;
        private readonly RingPreviewConduit _conduit;
        private Guid _editingObjectId = Guid.Empty;

        // UI Controls
        private ListBox _listSizes;
        private NumericStepper _numDia, _numCirc;
        private DropDown _ddMaterials;
        private DropDown _ddProfile;
        private Button _btnPickCurve;

        private Button _btnTop, _btnBottom, _btnLeft, _btnRight;
        private Button _btnTopLeft, _btnTopRight, _btnBottomLeft, _btnBottomRight;

        private NumericStepper _numWidth, _numHeight;
        private NumericStepper _numRot, _numOffsetY;
        private CheckBox _chkMirror;
        private CheckBox _chkActive;
        private CheckBox _chkFlip;
        private Label _lblWeight;
        private Button _btnBake;

        private RingPosition _currentPos;
        private bool _isUpdatingUI = false;

        public RingWizardDlg()
        {
            Title = "Ring Wizard";
            ClientSize = new Size(300, 600);
            Resizable = false;
            Topmost = true;
            Owner = RhinoEtoApp.MainWindow;

            _manager = new RingDesignManager();
            _conduit = new RingPreviewConduit();
            _conduit.Enabled = true;

            Content = BuildMainLayout();

            _isUpdatingUI = true;
            FillSizeList();
            FillMaterialList();
            FillProfileList();
            LoadPositionValues(RingPosition.Bottom);
            _isUpdatingUI = false;

            Closed += (s, e) => {
                _conduit.Enabled = false;
                RhinoDoc.ActiveDoc.Views.Redraw();
            };

            UpdateGeometry();
            UpdateButtonColors();
        }

        private Control BuildMainLayout()
        {
            // 1. SETTINGS
            var grpSettings = new GroupBox { Text = "General Settings", Padding = 5, Font = Eto.Drawing.Fonts.Sans(8, Eto.Drawing.FontStyle.Bold) };

            _ddMaterials = new DropDown { Height = 24 };
            _ddMaterials.SelectedValueChanged += (s, e) => UpdateGeometry();

            _listSizes = new ListBox { Height = 50 };
            _listSizes.SelectedIndexChanged += (s, e) => UpdateGeometry();

            _numDia = new NumericStepper { DecimalPlaces = 2, ReadOnly = true, Width = 60, Font = Eto.Drawing.Fonts.Sans(8) };
            _numCirc = new NumericStepper { DecimalPlaces = 2, ReadOnly = true, Width = 60, Font = Eto.Drawing.Fonts.Sans(8) };

            _lblWeight = new Label { Text = "Weight: ", TextColor = Colors.Blue, Font = Eto.Drawing.Fonts.Sans(9, Eto.Drawing.FontStyle.Bold), VerticalAlignment = VerticalAlignment.Center };

            var layoutSettings = new TableLayout { Spacing = new Size(5, 5) };
            layoutSettings.Rows.Add(new TableRow(new Label { Text = "Material:", VerticalAlignment = VerticalAlignment.Center }, _ddMaterials));
            layoutSettings.Rows.Add(new TableRow(new Label { Text = "Ring Size:", VerticalAlignment = VerticalAlignment.Center }, null));
            layoutSettings.Rows.Add(new TableRow(_listSizes) { ScaleHeight = false });

            var statsLayout = new TableLayout { Spacing = new Size(10, 0) };
            statsLayout.Rows.Add(new TableRow(
                new Label { Text = "Ø:", Font = Eto.Drawing.Fonts.Sans(8), VerticalAlignment = VerticalAlignment.Center }, _numDia,
                new Label { Text = "Cir:", Font = Eto.Drawing.Fonts.Sans(8), VerticalAlignment = VerticalAlignment.Center }, _numCirc
            ));
            layoutSettings.Rows.Add(new TableRow(statsLayout));
            layoutSettings.Rows.Add(new TableRow(_lblWeight));
            grpSettings.Content = layoutSettings;

            // 2. NAVIGATOR
            var grpMap = new GroupBox { Text = "Profile Navigator", Padding = 5, Font = Eto.Drawing.Fonts.Sans(8, Eto.Drawing.FontStyle.Bold) };
            var pixelLayout = new PixelLayout { Size = new Size(340, 160) };
            int cx = 170; int cy = 80; int radius = 60;

            _ddProfile = new DropDown { Width = 110, Font = Eto.Drawing.Fonts.Sans(8) };
            _ddProfile.ItemTextBinding = new PropertyBinding<string>(nameof(ProfileListItem.Text));
            _ddProfile.ItemKeyBinding = new PropertyBinding<string>(nameof(ProfileListItem.Key));
            _ddProfile.ItemImageBinding = new PropertyBinding<Image>(nameof(ProfileListItem.Image));
            _ddProfile.SelectedValueChanged += (s, e) => SaveCurrentValues();

            _btnPickCurve = new Button { Text = "Pick Curve", Width = 70, Height = 24, Font = Eto.Drawing.Fonts.Sans(8) };
            _btnPickCurve.Click += OnPickCurveClicked;

            pixelLayout.Add(_ddProfile, cx - 55, cy - 28);
            pixelLayout.Add(_btnPickCurve, cx - 35, cy + 8);

            _btnTop = AddCircularBtn(pixelLayout, "T", RingPosition.Top, cx, cy, radius, -90);
            _btnTopRight = AddCircularBtn(pixelLayout, "TR", RingPosition.TopRight, cx, cy, radius, -45);
            _btnRight = AddCircularBtn(pixelLayout, "R", RingPosition.Right, cx, cy, radius, 0);
            _btnBottomRight = AddCircularBtn(pixelLayout, "BR", RingPosition.BottomRight, cx, cy, radius, 45);
            _btnBottom = AddCircularBtn(pixelLayout, "B", RingPosition.Bottom, cx, cy, radius, 90);
            _btnBottomLeft = AddCircularBtn(pixelLayout, "BL", RingPosition.BottomLeft, cx, cy, radius, 135);
            _btnLeft = AddCircularBtn(pixelLayout, "L", RingPosition.Left, cx, cy, radius, 180);
            _btnTopLeft = AddCircularBtn(pixelLayout, "TL", RingPosition.TopLeft, cx, cy, radius, 225);
            grpMap.Content = pixelLayout;

            // 3. EDIT
            var grpEdit = new GroupBox { Text = "Section Parameters", Padding = 5, Font = Eto.Drawing.Fonts.Sans(8, Eto.Drawing.FontStyle.Bold) };
            _numWidth = new NumericStepper { DecimalPlaces = 2, MinValue = 0.5, MaxValue = 30, Width = 65, Increment = 0.1, Font = Eto.Drawing.Fonts.Sans(8) };
            _numHeight = new NumericStepper { DecimalPlaces = 2, MinValue = 0.5, MaxValue = 30, Width = 65, Increment = 0.1, Font = Eto.Drawing.Fonts.Sans(8) };
            _numRot = new NumericStepper { DecimalPlaces = 1, MinValue = -180, MaxValue = 180, Width = 65, Increment = 1.0, Font = Eto.Drawing.Fonts.Sans(8) };
            _numOffsetY = new NumericStepper { DecimalPlaces = 2, MinValue = -10, MaxValue = 10, Width = 65, Increment = 0.1, Font = Eto.Drawing.Fonts.Sans(8) };

            _chkMirror = new CheckBox { Text = "Mirror X", Checked = true, Font = Eto.Drawing.Fonts.Sans(8) };
            _chkActive = new CheckBox { Text = "Active", Checked = true, Font = Eto.Drawing.Fonts.Sans(8) };
            _chkFlip = new CheckBox { Text = "Flip", Checked = false, Font = Eto.Drawing.Fonts.Sans(8) };

            _numWidth.ValueChanged += (s, e) => SaveCurrentValues();
            _numHeight.ValueChanged += (s, e) => SaveCurrentValues();
            _numRot.ValueChanged += (s, e) => SaveCurrentValues();
            _numOffsetY.ValueChanged += (s, e) => SaveCurrentValues();
            _chkMirror.CheckedChanged += (s, e) => { _manager.MirrorX = _chkMirror.Checked == true; SaveCurrentValues(); };
            _chkActive.CheckedChanged += (s, e) => { if (!_isUpdatingUI) { _manager.ToggleActive(_currentPos); UpdateButtonColors(); UpdateGeometry(); } };
            _chkFlip.CheckedChanged += (s, e) => { if (!_isUpdatingUI) { _manager.ToggleFlipProfile(_currentPos); UpdateGeometry(); } };

            var layoutEdit = new TableLayout { Spacing = new Size(5, 5) };
            var checkLayout = new StackLayout { Orientation = Orientation.Horizontal, Spacing = 10, Items = { _chkActive, _chkMirror, _chkFlip } };
            layoutEdit.Rows.Add(new TableRow(checkLayout));
            var gridValues = new TableLayout { Spacing = new Size(5, 5) };
            gridValues.Rows.Add(new TableRow(
                new Label { Text = "Width:", VerticalAlignment = VerticalAlignment.Center, Font = Eto.Drawing.Fonts.Sans(8) }, _numWidth,
                new Label { Text = "Height:", VerticalAlignment = VerticalAlignment.Center, Font = Eto.Drawing.Fonts.Sans(8) }, _numHeight
            ));
            gridValues.Rows.Add(new TableRow(
                new Label { Text = "Rot (°):", VerticalAlignment = VerticalAlignment.Center, Font = Eto.Drawing.Fonts.Sans(8) }, _numRot,
                new Label { Text = "Off Y:", VerticalAlignment = VerticalAlignment.Center, Font = Eto.Drawing.Fonts.Sans(8) }, _numOffsetY
            ));
            layoutEdit.Rows.Add(new TableRow(gridValues));
            grpEdit.Content = layoutEdit;

            // 4. FOOTER
            _btnBake = new Button { Text = "Create Ring", Height = 26 };
            _btnBake.Click += OnBakeClicked;
            var btnCancel = new Button { Text = "Cancel", Height = 26 };
            btnCancel.Click += (s, e) => Close();
            var footerLayout = new TableLayout { Spacing = new Size(5, 0) };
            footerLayout.Rows.Add(new TableRow(null, btnCancel, _btnBake));

            var rootLayout = new TableLayout { Padding = 10, Spacing = new Size(0, 10) };
            rootLayout.Rows.Add(new TableRow(grpSettings));
            rootLayout.Rows.Add(new TableRow(grpMap));
            rootLayout.Rows.Add(new TableRow(grpEdit));
            rootLayout.Rows.Add(new TableRow { ScaleHeight = true });
            rootLayout.Rows.Add(new TableRow(new Panel { BackgroundColor = Colors.LightGrey, Height = 1 }));
            rootLayout.Rows.Add(new TableRow(new Panel { Padding = new Padding(0, 10, 0, 0), Content = footerLayout }));

            return rootLayout;
        }

        private void OnBakeClicked(object sender, EventArgs e)
        {
            // 1. Geometrie holen (Sicherheits-Check)
            var finalBreps = GetFinalRing();

            // Wenn Geometrie null ist, abbrechen (verhindert Crash beim Zugriff auf Index 0)
            if (finalBreps == null || finalBreps.Length == 0)
            {
                MessageBox.Show("Error: Could not calculate ring geometry.", MessageBoxType.Error);
                return;
            }

            Brep finalRing = finalBreps[0];
            var doc = RhinoDoc.ActiveDoc;

            // 2. Geometrie-Validierung (Wie besprochen)
            if (!finalRing.IsValid)
            {
                finalRing.Repair(doc.ModelAbsoluteTolerance);
                if (!finalRing.IsValid)
                {
                    MessageBox.Show("Geometry is invalid. Please check profiles.", "Error", MessageBoxButtons.OK, MessageBoxType.Error);
                    return;
                }
            }

            // Prüfung: Ist der Ring geschlossen?
            if (!finalRing.IsSolid)
            {
                // Versuch, die Löcher zu schließen
                // WICHTIG: Nicht direkt überschreiben, da Ergebnis null sein kann!
                var cappedRing = finalRing.CapPlanarHoles(doc.ModelAbsoluteTolerance);

                if (cappedRing != null)
                {
                    // Nur übernehmen, wenn es geklappt hat
                    finalRing = cappedRing;
                }

                // Jetzt prüfen wir erneut (finalRing ist hier garantiert nicht null, 
                // entweder ist es der neue geschlossene oder der alte offene Ring)
                if (!finalRing.IsSolid)
                {
                    var res = MessageBox.Show(
                        "The ring is NOT a closed solid (Naked Edges detected).\nCreate anyway?",
                        "Warning: Not Watertight",
                        MessageBoxButtons.YesNo,
                        MessageBoxType.Warning);

                    if (res == DialogResult.No) return;
                }
            }

            // 3. Daten schreiben (ABSTURZSICHER GEMACHT)
            string undoName = _editingObjectId != Guid.Empty ? "Update Ring" : "Create Ring";
            uint sn = doc.BeginUndoRecord(undoName);

            try
            {
                // Attribute erstellen
                var attr = doc.CreateDefaultAttributes();
                attr.Name = "SmartRing";
                attr.SetUserString("RG RING", "1");

                // SAFE ACCESS: Material ID (verhindert NullRef)
                string matId = _ddMaterials.SelectedKey ?? "metal.au750"; // Fallback, falls null
                attr.SetUserString("RG MATERIAL ID", matId);

                // SAFE ACCESS: Ring Size (verhindert NullRef)
                double currentSize = 54.0; // Standardwert
                if (_listSizes.SelectedValue != null)
                {
                    double.TryParse(_listSizes.SelectedValue.ToString(), out currentSize);
                }

                // SAFE ACCESS: Checkbox
                bool isMirrored = _chkMirror.Checked == true; // Behandelt null als false

                // SmartData zusammenbauen
                var sections = new List<RingSmartSection>();

                // Manager ist sicher, da im Konstruktor erstellt, aber wir prüfen trotzdem die Sektionen
                foreach (RingPosition pos in Enum.GetValues(typeof(RingPosition)))
                {
                    var sec = _manager.GetSection(pos);
                    if (sec != null) // Sicher ist sicher
                    {
                        sections.Add(new RingSmartSection
                        {
                            PositionIndex = (int)pos,
                            Width = sec.Width,
                            Height = sec.Height,
                            Rotation = sec.Rotation,
                            OffsetY = sec.OffsetY,
                            ProfileName = sec.ProfileName ?? "D-Shape", // Fallback
                            IsActive = sec.IsActive,
                            FlipX = sec.FlipX
                        });
                    }
                }

                var smartData = new RingSmartData(currentSize, matId, isMirrored, sections);

                // UserData anhängen
                finalRing.UserData.Add(smartData);

                // Ins Dokument einfügen
                if (_editingObjectId != Guid.Empty)
                {
                    doc.Objects.Replace(_editingObjectId, finalRing);
                    RhinoApp.WriteLine("Ring updated.");
                }
                else
                {
                    doc.Objects.AddBrep(finalRing, attr);
                    RhinoApp.WriteLine("Ring created.");
                }
            }
            catch (Exception ex)
            {
                // Fehler abfangen, damit Rhino nicht abstürzt, und Meldung zeigen
                MessageBox.Show($"Critical Error while baking: {ex.Message}", MessageBoxType.Error);
            }
            finally
            {
                doc.EndUndoRecord(sn);
            }

            doc.Views.Redraw();
            Close();
        }

        private void OnPickCurveClicked(object sender, EventArgs e)
        {
            this.Visible = false;
            var go = new Rhino.Input.Custom.GetObject();
            go.SetCommandPrompt("Select Open Profile Curve");
            go.GeometryFilter = Rhino.DocObjects.ObjectType.Curve;
            go.Get();
            this.Visible = true;

            if (go.CommandResult() == Rhino.Commands.Result.Success)
            {
                var crv = go.Object(0).Curve();
                if (crv != null)
                {
                    // Vorschau
                    _manager.UpdateSection(_currentPos, _numWidth.Value, _numHeight.Value, _numRot.Value, _numOffsetY.Value, crv);
                    bool old = _isUpdatingUI; _isUpdatingUI = true;
                    _ddProfile.SelectedKey = null;
                    _isUpdatingUI = old;
                    UpdateGeometry();

                    // Speichern Abfrage
                    var result = MessageBox.Show("Save to Ring Profiles?", "Save Profile", MessageBoxButtons.YesNo, MessageBoxType.Question);
                    if (result == DialogResult.Yes)
                    {
                        var inputDlg = new NewRhinoGold.Helpers.TextInputDialog("Save Profile", "RingProfile01");
                        string name = inputDlg.ShowModal(this);

                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            // WICHTIG: Hier sagen wir "Profiles"
                            bool saved = NewRhinoGold.Helpers.ProfileLoader.SaveProfile(name, crv, "Profiles");

                            if (saved)
                            {
                                ReloadProfiles(name);
                                _manager.UpdateSection(_currentPos, _numWidth.Value, _numHeight.Value, _numRot.Value, _numOffsetY.Value, name);
                                RhinoApp.WriteLine($"Saved to Profiles/{name}.3dm");
                            }
                            else
                            {
                                MessageBox.Show("Could not save. Check 'Profiles' folder.", MessageBoxType.Error);
                            }
                        }
                    }
                }
            }
        }
        private void ReloadProfiles(string selectName = null)
        {
            // Diese Methode macht im Prinzip das Gleiche wie FillProfileList, 
            // aber versucht, das neu erstellte Item direkt auszuwählen.

            var items = new List<ProfileListItem>();

            // Namen neu abrufen (jetzt ist der neue dabei)
            var names = RingProfileLibrary.GetProfileNames();
            if (names == null || names.Count == 0) names = new List<string> { "D-Shape" };

            foreach (var name in names)
            {
                var crv = RingProfileLibrary.GetOpenProfile(name);
                items.Add(new ProfileListItem { Text = name, Image = GenerateProfileIcon(crv) });
            }

            _ddProfile.DataStore = items;

            // Versuchen, den neuen Namen zu selektieren
            if (selectName != null)
            {
                // Wir suchen das Item, dessen Key oder Text passt
                var found = items.FirstOrDefault(i => i.Key == selectName || i.Text == selectName);
                if (found != null)
                {
                    _ddProfile.SelectedValue = found;
                }
                else if (items.Count > 0)
                {
                    _ddProfile.SelectedIndex = 0;
                }
            }
            else if (items.Count > 0)
            {
                _ddProfile.SelectedIndex = 0;
            }
        }

        private void SaveCurrentValues()
        {
            if (_isUpdatingUI) return;
            string selectedName = null;
            if (_ddProfile.SelectedValue is ProfileListItem pli) selectedName = pli.Text;
            else if (_ddProfile.SelectedKey != null) selectedName = _ddProfile.SelectedKey;

            if (!string.IsNullOrEmpty(selectedName))
                _manager.UpdateSection(_currentPos, _numWidth.Value, _numHeight.Value, _numRot.Value, _numOffsetY.Value, selectedName);
            else
            {
                var sec = _manager.GetSection(_currentPos);
                if (sec.CustomProfileCurve != null)
                    _manager.UpdateSection(_currentPos, _numWidth.Value, _numHeight.Value, _numRot.Value, _numOffsetY.Value, sec.CustomProfileCurve);
                else
                    _manager.UpdateSection(_currentPos, _numWidth.Value, _numHeight.Value, _numRot.Value, _numOffsetY.Value, "D-Shape");
            }
            UpdateGeometry();
        }

        private void LoadPositionValues(RingPosition pos)
        {
            bool wasUpd = _isUpdatingUI;
            _isUpdatingUI = true;
            _currentPos = pos;
            var sec = _manager.GetSection(pos);

            _numWidth.Value = sec.Width;
            _numHeight.Value = sec.Height;
            _numRot.Value = sec.Rotation;
            _numOffsetY.Value = sec.OffsetY;
            _chkActive.Checked = sec.IsActive;
            _chkFlip.Checked = sec.FlipX;

            bool found = false;
            foreach (var item in _ddProfile.DataStore)
            {
                if (item is ProfileListItem pli && pli.Text == sec.ProfileName) { _ddProfile.SelectedKey = pli.Key; found = true; break; }
            }
            if (!found) _ddProfile.SelectedKey = null;
            UpdateButtonColors();
            _isUpdatingUI = wasUpd;
        }

        private void UpdateButtonColors()
        {
            void SetBtnColor(Button b, RingPosition p)
            {
                var sec = _manager.GetSection(p);
                if (_currentPos == p) { b.BackgroundColor = Colors.DodgerBlue; b.TextColor = Colors.White; }
                else if (sec.IsActive) { b.BackgroundColor = Colors.Orange; b.TextColor = Colors.White; }
                else { b.BackgroundColor = Colors.LightGrey; b.TextColor = Colors.DarkGray; }
            }
            SetBtnColor(_btnTop, RingPosition.Top);
            SetBtnColor(_btnTopLeft, RingPosition.TopLeft);
            SetBtnColor(_btnTopRight, RingPosition.TopRight);
            SetBtnColor(_btnLeft, RingPosition.Left);
            SetBtnColor(_btnRight, RingPosition.Right);
            SetBtnColor(_btnBottom, RingPosition.Bottom);
            SetBtnColor(_btnBottomLeft, RingPosition.BottomLeft);
            SetBtnColor(_btnBottomRight, RingPosition.BottomRight);
        }

        private void UpdateGeometry()
        {
            if (_isUpdatingUI || _listSizes.SelectedValue == null) return;
            double size = double.Parse(_listSizes.SelectedValue.ToString());
            double radius = (size / Math.PI) / 2.0;
            _numCirc.Value = size; _numDia.Value = size / Math.PI;

            var plane = new Plane(Point3d.Origin, Vector3d.XAxis, Vector3d.ZAxis);
            var circle = new Circle(plane, radius);
            circle.Rotate(-Math.PI / 2.0, plane.Normal, plane.Origin);
            Curve rail = circle.ToNurbsCurve();

            System.Drawing.Color displayColor = System.Drawing.Color.Gold;
            double density = 15.0;

            string matId = _ddMaterials.SelectedKey;
            if (!string.IsNullOrEmpty(matId))
            {
                var m = Densities.Get(matId);
                if (m != null)
                {
                    displayColor = m.DisplayColor;
                    density = m.Density;
                }
            }

            var buildData = _manager.GetBuildData();
            var ringBreps = RingBuilder.BuildRing(radius, buildData.Slots, buildData.IsClosedLoop, true);

            if (ringBreps != null)
            {
                double totalVol = 0;
                foreach (var b in ringBreps) totalVol += b.GetVolume();
                double grams = (totalVol / 1000.0) * density;
                _lblWeight.Text = $"Weight: {grams:F2} g";
            }
            else
            {
                _lblWeight.Text = "Weight: n/a";
            }

            _conduit.SetScene(rail, ringBreps, displayColor);
            RhinoDoc.ActiveDoc.Views.Redraw();
        }

        public Brep[] GetFinalRing()
        {
            if (_listSizes.SelectedValue == null) return null;
            double size = double.Parse(_listSizes.SelectedValue.ToString());
            double radius = (size / Math.PI) / 2.0;
            var buildData = _manager.GetBuildData();
            return RingBuilder.BuildRing(radius, buildData.Slots, buildData.IsClosedLoop, true);
        }

        private Image GenerateProfileIcon(Curve crv)
        {
            if (crv == null) return null;
            try
            {
                int w = 32; int h = 32; var bmp = new Bitmap(w, h, PixelFormat.Format32bppRgba); using (var g = new Graphics(bmp))
                {
                    var poly = crv.ToPolyline(0, 0, 0.1, 0, 0, 0, 0, 0, true); if (poly != null && poly.TryGetPolyline(out Rhino.Geometry.Polyline pl))
                    {
                        var bbox = pl.BoundingBox; double max = Math.Max(bbox.Max.X - bbox.Min.X, bbox.Max.Y - bbox.Min.Y); if (max < 0.001) max = 1;
                        float sc = (float)(22.0 / max); var pts = new List<PointF>();
                        foreach (var pt in pl) pts.Add(new PointF(w / 2.0f + (float)(pt.X * sc), (h / 2.0f) + 5 - (float)(pt.Y * sc)));
                        g.DrawLines(Colors.Black, pts.ToArray());
                    }
                }
                return bmp;
            }
            catch { return null; }
        }

        private Button AddCircularBtn(PixelLayout layout, string text, RingPosition pos, int cx, int cy, int r, double angleDeg)
        {
            var btn = new Button { Text = text, Size = new Size(28, 24), Font = Eto.Drawing.Fonts.Sans(7) };
            btn.Click += (s, e) => LoadPositionValues(pos);
            btn.Tag = pos;
            double rad = angleDeg * (Math.PI / 180.0);
            int x = (int)(cx + r * Math.Cos(rad)) - 14;
            int y = (int)(cy + r * Math.Sin(rad)) - 12;
            layout.Add(btn, x, y);
            return btn;
        }

        private void FillSizeList() { _listSizes.Items.Clear(); for (int i = 48; i <= 72; i++) _listSizes.Items.Add(new ListItem { Text = i.ToString(), Key = i.ToString() }); _listSizes.SelectedIndex = 6; }
        private void FillMaterialList() { _ddMaterials.Items.Clear(); foreach (var mat in Densities.Metals) _ddMaterials.Items.Add(new ListItem { Text = mat.Name, Key = mat.Id }); string def = "metal.au750"; if (_ddMaterials.Items.Any(i => i.Key == def)) _ddMaterials.SelectedKey = def; else if (_ddMaterials.Items.Count > 0) _ddMaterials.SelectedIndex = 0; }
        private void FillProfileList()
        {
            var items = new List<ProfileListItem>();
            var names = RingProfileLibrary.GetProfileNames(); if (names == null || names.Count == 0) names = new List<string> { "D-Shape" };
            foreach (var name in names) { var crv = RingProfileLibrary.GetOpenProfile(name); items.Add(new ProfileListItem { Text = name, Image = GenerateProfileIcon(crv) }); }
            _ddProfile.DataStore = items; if (items.Count > 0) _ddProfile.SelectedIndex = 0;
        }

        public void LoadSmartData(RingSmartData data, Guid objectId)
        {
            if (data == null) return;
            _editingObjectId = objectId;
            Title = "Edit Ring";
            _btnBake.Text = "Update Ring";

            bool old = _isUpdatingUI;
            _isUpdatingUI = true;

            foreach (var item in _listSizes.Items)
            {
                if (item.Text == data.RingSize.ToString() || item.Key == data.RingSize.ToString()) { _listSizes.SelectedKey = item.Key; break; }
            }

            foreach (var item in _ddMaterials.Items)
            {
                if (item.Key == data.MaterialId) { _ddMaterials.SelectedKey = item.Key; break; }
            }

            _chkMirror.Checked = data.MirrorX;
            _manager.MirrorX = data.MirrorX;

            foreach (var s in data.Sections)
            {
                RingPosition pos = (RingPosition)s.PositionIndex;
                _manager.UpdateSection(pos, s.Width, s.Height, s.Rotation, s.OffsetY, s.ProfileName);
                var check = _manager.GetSection(pos);
                if (check.IsActive != s.IsActive) _manager.ToggleActive(pos);
                if (check.FlipX != s.FlipX) _manager.ToggleFlipProfile(pos);
            }

            _isUpdatingUI = old;
            LoadPositionValues(RingPosition.Bottom);
            UpdateGeometry();
        }
    }
}