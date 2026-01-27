using System;
using Eto.Drawing;
using Eto.Forms;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input.Custom;
using Rhino.UI;
using NewRhinoGold.Core;

namespace NewRhinoGold.Dialog
{
    public class PickGemDlg : Form
    {
        // Speichert das Ziel dauerhaft, solange der Dialog offen ist
        private ObjRef _targetRef = null;
        
        // UI Elemente
        private Label _lblStatus;
        private Button _btnSelectTarget;
        private NumericStepper _numGap;
        private Button _btnPickGem;
        private Button _btnClose;

        public PickGemDlg()
        {
            Title = "Pick & Place Gem";
            ClientSize = new Size(240, 220);
            Topmost = true;       // Bleibt im Vordergrund
            Resizable = false;
            Padding = new Padding(10);
            Owner = RhinoEtoApp.MainWindow;

            Content = BuildLayout();
        }

        private Control BuildLayout()
        {
            var layout = new DynamicLayout { Padding = Padding.Empty, Spacing = new Size(5, 5) };

            _lblStatus = new Label 
            { 
                Text = "Step 1: Select a Target", 
                TextColor = Colors.DimGray, 
                Font = Eto.Drawing.Fonts.Sans(8, FontStyle.Italic) 
            };

            _btnSelectTarget = new Button { Text = "Set Target (Srf/Crv)" };
            _btnSelectTarget.Click += OnSetTarget;

            _numGap = new NumericStepper { Value = 0.2, Increment = 0.1, DecimalPlaces = 2, MinValue = 0 };
            
            _btnPickGem = new Button { Text = "Pick Gem", Height = 40, Font = Eto.Drawing.Fonts.Sans(10, FontStyle.Bold) };
            _btnPickGem.Click += OnPickGem;
            _btnPickGem.Enabled = false; // Erst aktiv, wenn Ziel da ist

            _btnClose = new Button { Text = "Close" };
            _btnClose.Click += (s, e) => Close();

            layout.BeginVertical();
            layout.AddRow(new Label { Text = "Target Surface/Curve:", Font = Eto.Drawing.Fonts.Sans(9, FontStyle.Bold) });
            layout.AddRow(_lblStatus);
            layout.AddRow(_btnSelectTarget);
            layout.AddRow(null); // Spacer
            
            layout.AddRow(new Label { Text = "Gap / Distance (mm):", Font = Eto.Drawing.Fonts.Sans(9, FontStyle.Bold) });
            layout.AddRow(_numGap);
            layout.AddRow(null); // Spacer

            layout.AddRow(_btnPickGem);
            layout.AddRow(null);
            layout.AddRow(_btnClose);
            layout.EndVertical();

            return layout;
        }

        private void OnSetTarget(object sender, EventArgs e)
        {
            Visible = false; // Dialog ausblenden

            var go = new GetObject();
            go.SetCommandPrompt("Select Target Surface or Curve");
            go.GeometryFilter = ObjectType.Surface | ObjectType.PolysrfFilter | ObjectType.Curve;
            go.EnablePreSelect(true, true);
            go.DeselectAllBeforePostSelect = false; // Erlaubt Wechsel der Selektion
            go.Get();

            Visible = true; // Dialog wieder zeigen

            if (go.CommandResult() == Rhino.Commands.Result.Success)
            {
                _targetRef = go.Object(0);
                
                string type = _targetRef.Geometry() is Curve ? "Curve" : "Surface";
                _lblStatus.Text = $"Target set: {type}";
                _lblStatus.TextColor = Colors.Green;
                
                _btnPickGem.Enabled = true; // Jetzt kann man Gems picken
            }
        }

        private void OnPickGem(object sender, EventArgs e)
        {
            if (_targetRef == null) return;

            Visible = false;

            try
            {
                // 1. Gem wählen
                var goGem = new GetObject();
                goGem.SetCommandPrompt("Pick a Gem to place");
                goGem.GeometryFilter = ObjectType.Brep | ObjectType.Mesh;
                goGem.EnablePreSelect(true, true);
                goGem.Get();

                if (goGem.CommandResult() == Rhino.Commands.Result.Success)
                {
                    var gemObj = goGem.Object(0);
                    
                    // SmartData lesen (für Orientierung)
                    var smartData = gemObj.Object().Attributes.UserData.Find(typeof(GemSmartData)) as GemSmartData;
                    Plane srcPlane = smartData != null ? smartData.GemPlane : Plane.WorldXY;
                    
                    // Radius für die Gap-Anzeige berechnen
                    var bbox = gemObj.Geometry().GetBoundingBox(srcPlane);
                    double radius = (bbox.Max.X - bbox.Min.X) / 2.0;

                    // 2. Placer starten (Hier wird die Klasse aus dem vorherigen Schritt genutzt)
                    // Stellen Sie sicher, dass ContinuousGemPlacer im Namespace verfügbar ist
                    var placer = new ContinuousGemPlacer(
                        gemObj.Geometry(), 
                        srcPlane, 
                        _targetRef, 
                        radius, 
                        _numGap.Value, 
                        smartData
                    );
                    
                    // Endlosschleife starten (bis Rechtsklick)
                    placer.RunLoop(RhinoDoc.ActiveDoc);
                }
            }
            finally
            {
                // Dialog kommt zurück, Target bleibt gespeichert
                Visible = true;
            }
        }
    }
}