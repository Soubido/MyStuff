using System;
using Eto.Drawing;
using Eto.Forms;
using Rhino;
using Rhino.UI;

namespace NewRhinoGold.Dialog
{
    // WICHTIG: Das ist die Zeile, die Rhino zwingend braucht!
    [System.Runtime.InteropServices.Guid("D93808C6-2313-4315-B667-89735E263888")]
    public class MainToolbarDlg : Panel
    {
        // Für den Command holen wir die ID sicherheitshalber direkt von der Klasse
        public static Guid PanelId => typeof(MainToolbarDlg).GUID;

        // Fonts explizit über Eto.Drawing.Fonts adressiert, wie gewünscht
        private readonly Eto.Drawing.Font _fontHeader = Eto.Drawing.Fonts.Sans(9, FontStyle.Bold);
        private readonly Eto.Drawing.Font _fontBtn = Eto.Drawing.Fonts.Sans(9);

        private readonly Size _btnSize = new Size(-1, 28);
        private readonly Padding _padding = new Padding(5);

        public MainToolbarDlg()
        {
            // Konstruktor MUSS parameterlos sein für Rhino Panels
            Content = BuildLayout();
        }

        private Control BuildLayout()
        {
            var layout = new DynamicLayout { Padding = _padding, Spacing = new Size(0, 5) };

            layout.BeginVertical();

            // --- SELECTION ---
            layout.Add(CreateHeader("Selection"));
            layout.Add(CreateCmdBtn("Select Gems", "_SelGems"));
            layout.Add(CreateCmdBtn("Select Heads", "_SelHeads"));
            layout.Add(CreateCmdBtn("Select Bezels", "_SelBezels"));
            // NEU: Select Cutters hinzugefügt
            layout.Add(CreateCmdBtn("Select Cutters", "_SelCutters"));
            layout.Add(null); // Kleiner Abstand

            // --- CREATION ---
            layout.Add(CreateHeader("Creation"));
            // NEU: Ring Wizard (ganz oben, da Haupttool)
            layout.Add(CreateCmdBtn("Ring Wizard", "_RingWizard"));
            layout.Add(CreateCmdBtn("Gem Studio", "_GemStudio"));
            layout.Add(CreateCmdBtn("Gem Creator", "_GemCreator"));
            layout.Add(CreateCmdBtn("Cutter Studio", "_CutterStudio"));
            layout.Add(CreateCmdBtn("Bezel Studio", "_BezelStudio"));
            layout.Add(CreateCmdBtn("Head Studio", "_HeadStudio"));
            layout.Add(CreateCmdBtn("Pave Studio", "_PaveStudio"));
            layout.Add(null);

            // --- EDIT ---
            layout.Add(CreateHeader("Edit"));
            layout.Add(CreateCmdBtn("Edit Smart Object", "_EditSmartObject")); // Neuer Befehl
            layout.Add(null);

            // --- TOOLS ---
            layout.Add(CreateHeader("Tools"));
            layout.Add(CreateCmdBtn("Extract Curve", "_ExtractGemCurve"));
            layout.Add(CreateCmdBtn("Move on Srf", "_MoveOnSurface"));
            layout.Add(null);

            // --- ANALYSIS ---
            layout.Add(CreateHeader("Analysis"));
            layout.Add(CreateCmdBtn("Gold Weight", "_GoldWeight"));
            layout.Add(CreateCmdBtn("Gem Report", "_GemReport"));

            // Platzhalter am Ende, damit nicht alles gequetscht wirkt
            layout.Add(new Eto.Forms.Panel { Height = 10 });

            layout.EndVertical();

            return new Scrollable { Content = layout, Border = BorderType.None };
        }

        private Control CreateHeader(string text)
        {
            return new Label
            {
                Text = text,
                Font = _fontHeader,
                TextColor = Colors.Gray,
                TextAlignment = TextAlignment.Center
            };
        }

        private Button CreateCmdBtn(string label, string command)
        {
            var btn = new Button { Text = label, Font = _fontBtn, Size = _btnSize };
            // Führt den Rhino Befehl aus (z.B. "_SelCutters")
            btn.Click += (s, e) => RhinoApp.RunScript(command, false);
            return btn;
        }
    }
}