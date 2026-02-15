using System;
using Rhino;
using Rhino.Commands;
using Rhino.UI;
using Eto.Forms;
using NewRhinoGold.Dialog;

namespace NewRhinoGold.Commands
{
    // Diese Klasse öffnet den Dialog.
    public class GemCreatorCommand : Rhino.Commands.Command
    {
        public override Guid Id => new Guid("A13B6391-5C65-41D5-9134-76D2CAB30369");
        public override string EnglishName => "GemCreator";

        private static GemCreatorDlg _dialog;

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            if (_dialog != null && _dialog.Visible)
            {
                _dialog.BringToFront();
                return Result.Success;
            }

            _dialog = new GemCreatorDlg();

            var rhinoMain = RhinoEtoApp.MainWindow;
            if (rhinoMain != null)
                _dialog.Owner = rhinoMain;

            _dialog.Closed += (s, e) =>
            {
                _dialog.Dispose();
                _dialog = null;
            };

            _dialog.Show();

            return Result.Success;
        }
    }
}