using System;
using System.Collections.Generic;
using Eto.Drawing;
using Eto.Forms;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.UI;
using NewRhinoGold.Core;
using NewRhinoGold.BezelStudio;
using NewRhinoGold.Helpers;

namespace NewRhinoGold.Studio
{
	public class CutterStudioDlg : Dialog<bool>
	{
		// State
		private List<GemSmartData> _selectedGems = new List<GemSmartData>();
		private CutterPreviewConduit _previewConduit;
		private bool _suspendUpdates = false;

		// UI Controls
		private Button _btnSelectGems, _btnBuild, _btnClose;
		private NumericStepper _numScale, _numClearance;
		private NumericStepper _numTopHeight, _numTopDia;
		private NumericStepper _numSeatPos;
		private NumericStepper _numBotHeight, _numBotDia;
		private RadioButton _rbShapeRound, _rbShapeLibrary;
		private GridView _gridLibrary;
		private NumericStepper _numProfileRot;
		private TabControl _tabControl;

		public CutterStudioDlg()
		{
			Title = "Cutter Studio";
			ClientSize = new Size(360, 550);
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

			_tabControl = new TabControl();
			_tabControl.Pages.Add(new TabPage { Text = "Parameters", Content = BuildParamsTab() });
			_tabControl.Pages.Add(new TabPage { Text = "Bottom Shape", Content = BuildShapeTab() });

			_btnBuild = new Button { Text = "Build Cutters", Height = 28 };
			_btnBuild.Click += OnBuild;

			_btnClose = new Button { Text = "Close", Height = 28 };
			_btnClose.Click += (s, e) => Close(false);

			var layout = new TableLayout { Padding = 10, Spacing = new Size(2, 4) };
			layout.Rows.Add(_btnSelectGems);
			layout.Rows.Add(_tabControl);
			layout.Rows.Add(new Panel { Height = 4 });
			layout.Rows.Add(new TableRow(null, _btnClose, _btnBuild));

			return layout;
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

			var l = new TableLayout { Padding = 10, Spacing = new Size(2, 2) };
			l.Rows.Add(CreateHeader("Global"));
			l.Rows.Add(CreateRow("Scale % / Gap:", new StackLayout { Orientation = Orientation.Horizontal, Spacing = 5, Items = { _numScale, _numClearance } }));
			l.Rows.Add(new Panel { Height = 4 });
			l.Rows.Add(CreateHeader("Top Shaft (%)"));
			l.Rows.Add(CreateRow("Height / Scale:", new StackLayout { Orientation = Orientation.Horizontal, Spacing = 5, Items = { _numTopHeight, _numTopDia } }));
			l.Rows.Add(new Panel { Height = 4 });
			l.Rows.Add(CreateHeader("Seat / Girdle"));
			l.Rows.Add(CreateRow("Seat Level %:", _numSeatPos));
			l.Rows.Add(new Panel { Height = 4 });
			l.Rows.Add(CreateHeader("Bottom Shaft (%)"));
			l.Rows.Add(CreateRow("Height / Scale:", new StackLayout { Orientation = Orientation.Horizontal, Spacing = 5, Items = { _numBotHeight, _numBotDia } }));
			return new Scrollable { Content = l, Border = BorderType.None };
		}

		private Control BuildShapeTab()
		{
			_rbShapeRound = new RadioButton { Text = "Standard (Round)", Checked = true, Font = Eto.Drawing.Fonts.Sans(8) };
			_rbShapeLibrary = new RadioButton(_rbShapeRound) { Text = "Library Profile", Font = Eto.Drawing.Fonts.Sans(8) };

			_gridLibrary = new GridView { Height = 200, Enabled = false };
			_gridLibrary.Columns.Add(new GridColumn { HeaderText = "Profile", DataCell = new ImageTextCell { ImageBinding = Binding.Property<ProfileItem, Image>(x => x.Preview), TextBinding = Binding.Property<ProfileItem, string>(x => x.Name) } });
			_gridLibrary.DataStore = ProfileLibrary.Items;
			_gridLibrary.SelectionChanged += (s, e) => UpdatePreview();

			_numProfileRot = CreateStepper(0);

			void UpdateEnabled()
			{
				_gridLibrary.Enabled = (_rbShapeLibrary.Checked == true);
				UpdatePreview();
			}
			_rbShapeRound.CheckedChanged += (s, e) => UpdateEnabled();
			_rbShapeLibrary.CheckedChanged += (s, e) => UpdateEnabled();

			var l = new TableLayout { Padding = 10, Spacing = new Size(2, 4) };
			l.Rows.Add(_rbShapeRound);
			l.Rows.Add(_rbShapeLibrary);
			l.Rows.Add(_gridLibrary);
			l.Rows.Add(new Panel { Height = 8 });
			l.Rows.Add(CreateRow("Rotation:", _numProfileRot));
			return new Scrollable { Content = l, Border = BorderType.None };
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
			if (p.UseCustomProfile && _gridLibrary.SelectedItem is ProfileItem item)
				p.ProfileId = item.Id;
			p.ProfileRotation = _numProfileRot.Value;
			return p;
		}

		private void OnSelectGems(object sender, EventArgs e)
		{
			// ÄNDERUNG: Dialog ausblenden
			this.Visible = false;

			try
			{
				var go = new Rhino.Input.Custom.GetObject();
				// Minimaler Prompt, notwendig für Interaktion
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

						var data = rhinoObj.Geometry.UserData.Find(typeof(GemSmartData)) as GemSmartData;
						if (data != null)
						{
							_selectedGems.Add(data);
						}
						else
						{
							if (SelectionHelpers.IsGem(rhinoObj))
							{
								if (RhinoGoldHelper.TryGetGemData(rhinoObj, out Curve c, out Plane p, out double s))
								{
									_selectedGems.Add(new GemSmartData(c, p, "Unknown", s, "Default", 0));
								}
							}
						}
					}
					UpdatePreview();
				}
			}
			finally
			{
				// ÄNDERUNG: Dialog immer wieder einblenden, auch bei Abbruch
				this.Visible = true;
			}
		}

		private void OnBuild(object sender, EventArgs e)
		{
			var p = GetParameters();
			var doc = RhinoDoc.ActiveDoc;

			uint sn = doc.BeginUndoRecord("Create Cutters");

			foreach (var gem in _selectedGems)
			{
				var parts = CutterBuilder.CreateCutter(gem, p);
				foreach (var brep in parts)
				{
					var attr = doc.CreateDefaultAttributes();
					attr.Name = "RG Cutter";
					attr.ObjectColor = System.Drawing.Color.OrangeRed;
					attr.ColorSource = ObjectColorSource.ColorFromObject;
					doc.Objects.AddBrep(brep, attr);
				}
			}

			doc.EndUndoRecord(sn);
			doc.Views.Redraw();

			// ÄNDERUNG: Dialog schließen, nachdem gebaut wurde
			Close(true);
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
				previewBreps.AddRange(parts);
			}

			_previewConduit.SetBreps(previewBreps);
			RhinoDoc.ActiveDoc.Views.Redraw();
		}

		// Helpers
		private NumericStepper CreateStepper(double v, int d = 0)
		{
			var s = new NumericStepper { Value = v, DecimalPlaces = d, Width = 60, Font = Eto.Drawing.Fonts.Sans(8) };
			s.ValueChanged += (o, e) => UpdatePreview();
			return s;
		}
		private TableRow CreateRow(string t, Control c) => new TableRow(new Label { Text = t, VerticalAlignment = VerticalAlignment.Center, Font = Eto.Drawing.Fonts.Sans(8) }, c);
		private Label CreateHeader(string t) => new Label { Text = t, Font = Eto.Drawing.Fonts.Sans(8, Eto.Drawing.FontStyle.Bold) };
	}

	// Vorschau-Conduit
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