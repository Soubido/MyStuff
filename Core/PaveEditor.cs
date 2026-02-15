using System.Collections.Generic;
using Rhino;
using Rhino.Input;
using Rhino.Input.Custom;
using Rhino.UI;
using Rhino.Geometry;
using Eto.Forms;

namespace NewRhinoGold.Core
{
    public class PaveEditor
    {
        public List<PaveInstance> CurrentItems { get; private set; }
        private Brep _surface;

        public PaveEditor(List<PaveInstance> items, Brep srf)
        {
            CurrentItems = new List<PaveInstance>(items);
            _surface = srf;
        }

        public void RunEditorLoop()
        {
            bool running = true;
            while (running)
            {
                RhinoDoc.ActiveDoc.Views.Redraw();

                var gp = new GetPoint();
                gp.SetCommandPrompt("Click on stone to Delete, or empty space to Add. [RightClick=Exit]");
                gp.AcceptNothing(true);
                gp.Get();

                if (gp.CommandResult() == Rhino.Commands.Result.Cancel ||
                    gp.CommandResult() == Rhino.Commands.Result.Nothing)
                {
                    running = false;
                    break;
                }

                if (gp.Result() == GetResult.Point)
                {
                    Point3d clickPt = gp.Point();

                    int hitIndex = -1;
                    for (int i = 0; i < CurrentItems.Count; i++)
                    {
                        if (CurrentItems[i].Position.DistanceTo(clickPt) < CurrentItems[i].Definition.Diameter / 2.0)
                        {
                            hitIndex = i;
                            break;
                        }
                    }

                    if (hitIndex != -1)
                    {
                        var res = MessageBox.Show("Delete this stone?", "Edit Pave", MessageBoxButtons.YesNo);
                        if (res == DialogResult.Yes) CurrentItems.RemoveAt(hitIndex);
                    }
                    else
                    {
                        // Add Logic
                        Point3d srfPt = _surface.ClosestPoint(clickPt);
                        _surface.ClosestPoint(srfPt, out Point3d _, out ComponentIndex ci, out double u, out double v, 0.1, out Vector3d n);

                        if (CurrentItems.Count > 0)
                        {
                            var def = CurrentItems[0].Definition;
                            CurrentItems.Add(new PaveInstance { Position = srfPt, Normal = n, Definition = def });
                        }
                    }
                }
            }
        }
    }
}