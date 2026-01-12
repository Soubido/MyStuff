using System;
using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Input.Custom;
using NewRhinoGold.Core;
using NewRhinoGold.Studio;
using NewRhinoGold.BezelStudio;

namespace NewRhinoGold.Commands
{
    public class EditSmartObjectCommand : Command
    {
        public EditSmartObjectCommand() { Instance = this; }
        public static EditSmartObjectCommand Instance { get; private set; }
        public override string EnglishName => "EditSmartObject";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var go = new GetObject();
            go.SetCommandPrompt("Select Smart Object to Edit");
            go.GeometryFilter = ObjectType.Brep | ObjectType.Mesh | ObjectType.Curve;
            go.SubObjectSelect = false;
            go.Get();
            if (go.CommandResult() != Result.Success) return go.CommandResult();

            var obj = go.Object(0).Object();
            if (obj == null) return Result.Failure;

            // 1. GEM
            if (obj.Geometry.UserData.Find(typeof(GemSmartData)) is GemSmartData gemData)
            {
                var dlg = GemStudioDlg.Instance ?? new GemStudioDlg();
                dlg.Show();
                dlg.LoadSmartData(gemData, obj.Id); // ID übergeben
                return Result.Success;
            }

            // 2. BEZEL
            if (obj.Geometry.UserData.Find(typeof(NewRhinoGold.Core.BezelSmartData)) is NewRhinoGold.Core.BezelSmartData bezelData)
            {
                var dlg = new BezelStudioDlg();
                dlg.Show();
                dlg.LoadSmartData(bezelData, obj.Id); // ID übergeben
                return Result.Success;
            }

            // 3. HEAD
            if (obj.Geometry.UserData.Find(typeof(HeadSmartData)) is HeadSmartData headData)
            {
                var dlg = new HeadStudioDlg();
                dlg.Show();
                dlg.LoadSmartData(headData, obj.Id); // ID übergeben
                return Result.Success;
            }

            // 4. CUTTER
            if (obj.Geometry.UserData.Find(typeof(CutterSmartData)) is CutterSmartData cutterData)
            {
                var dlg = new CutterStudioDlg();
                dlg.Show();
                dlg.LoadSmartData(cutterData, obj.Id); // ID übergeben
                return Result.Success;
            }
            // 5. RING
            if (obj.Geometry.UserData.Find(typeof(RingSmartData)) is RingSmartData ringData)
            {
                // Falls RingWizardDlg ein Singleton ist, nutzen Sie Instance, sonst new
                // Hier Annahme: new RingWizardDlg()
                var dlg = new NewRhinoGold.Wizard.RingWizardDlg();

                // Owner setzen für Modeless
                dlg.Owner = Rhino.UI.RhinoEtoApp.MainWindow;
                dlg.Show();

                dlg.LoadSmartData(ringData, obj.Id);
                return Result.Success;
            }

            RhinoApp.WriteLine("This object contains no editable SmartData.");
            return Result.Failure;
        }
    }
}