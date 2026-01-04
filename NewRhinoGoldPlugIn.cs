using Rhino;
using Rhino.PlugIns;
using Rhino.UI;
using NewRhinoGold.Dialog; // Namespace für MainToolbarDlg

namespace NewRhinoGold
{
    public class NewRhinoGoldPlugIn : PlugIn
    {
        public NewRhinoGoldPlugIn()
        {
            Instance = this;
        }

        public static NewRhinoGoldPlugIn Instance { get; private set; }

        public override PlugInLoadTime LoadTime => PlugInLoadTime.AtStartup;

        protected override LoadReturnCode OnLoad(ref string errorMessage)
        {
            // 1. Das Panel beim Rhino-System anmelden
            // Argumente: Plugin, Typ des Panels, Titel im Reiter, Icon (hier null)
            Panels.RegisterPanel(this, typeof(MainToolbarDlg), "BJewel Tools", null);

            RhinoApp.Idle += OnIdle;
            return LoadReturnCode.Success;
        }

        private void OnIdle(object sender, System.EventArgs e)
        {
            RhinoApp.Idle -= OnIdle;
            // Wir führen BJewel aus, was jetzt das Panel öffnet
            RhinoApp.RunScript("_BJewel", false);
        }
    }
}