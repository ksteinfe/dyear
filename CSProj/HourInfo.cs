using System;
using System.Collections.Generic;
using System.Text;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System.Drawing;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel.Attributes;

namespace DYear
{
    public class HourInfoComponent : GH_Component
    {
        public List<DHr> vhours;

        public HourInfoComponent()
            //Call the base constructor
            : base("Data Hour Info", "HourInfo", "Displays information from a list of DHrs", "DYear", "Manipulate")
        {
        vhours = new List<DHr>();
        }


        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            GHParam_DHr param = new GHParam_DHr();
            pManager.RegisterParam(param, "DHour", "Dhr", "The Dhour from which to extract a value", GH_ParamAccess.list);

        }
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager){}

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<DHr> dhrs= new List<DHr>();
            if (DA.GetDataList(0, dhrs)) //if it works...
            {
                this.vhours = new List<DHr>(dhrs);
            }
        }

        public override Guid ComponentGuid { get { return new Guid("{709C00D7-4E99-46AB-AD4A-A7235C822726}"); } }
        protected override Bitmap Icon { get { return DYear.Properties.Resources.Component; } }
        public override void CreateAttributes() { m_attributes = new HourInfoComponent_Attributes(this); }
    }


    public class HourInfoComponent_Attributes : GH_ComponentAttributes
    {
        public HourInfoComponent_Attributes(HourInfoComponent owner) : base(owner) { }

        protected override void Render(GH_Canvas canvas, Graphics graphics, GH_CanvasChannel channel)
        {
            // Render all the wires that connect the Owner to all its Sources.
            if (channel == GH_CanvasChannel.Wires)
            {
                base.Render(canvas, graphics, channel);
                return;
            }
            // Render the parameter capsule and any additional text on top of it.
            //base.Render(canvas, graphics, channel);
            //return;

            // Define the default palette.
            GH_Palette palette = GH_Palette.Normal;
            // Adjust palette based on the Owner's worst case messaging level.
            switch (Owner.RuntimeMessageLevel)
            {
                case GH_RuntimeMessageLevel.Warning:
                    palette = GH_Palette.Warning;
                    break;

                case GH_RuntimeMessageLevel.Error:
                    palette = GH_Palette.Error;
                    break;
            }

            // Create a new Capsule without text or icon.
            GH_Capsule capsule = GH_Capsule.CreateCapsule(Bounds, palette);

            capsule.AddInputGrip(this.InputGrip.Y);

            // Render the capsule using the current Selection, Locked and Hidden states.
            // DYr parameters are always hidden since they cannot be drawn in the rhino viewport.
            capsule.Render(graphics, Selected, Owner.Locked, true);
            capsule.Dispose(); // Always dispose of a GH_Capsule when you're done with it.
            capsule = null;

            // Now it's time to draw the text on top of the capsule.
            // First we'll draw the Owner NickName using a standard font and a black brush.
            // We'll also align the NickName in the center of the Bounds.
            StringFormat format = new StringFormat();
            format.Alignment = StringAlignment.Center;
            format.LineAlignment = StringAlignment.Center;
            format.Trimming = StringTrimming.EllipsisCharacter;
            RectangleF textRectangle = Bounds;
            textRectangle.Height = 15;
            graphics.DrawString(Owner.NickName, GH_FontServer.StandardBold, Brushes.Black, textRectangle, format);


            // ksteinfe start
            HourInfoComponent hiowner = (HourInfoComponent)(Owner);

            Rectangle rect = new Rectangle(0, 0, 100, 100);
            GH_Capsule cap = GH_Capsule.CreateTextCapsule(Bounds, Bounds, GH_Palette.Pink, "hello");
            cap.Render(graphics, Selected, Owner.Locked, true);
            cap.Dispose();





            // Always dispose of any GDI+ object that implement IDisposable.
            format.Dispose();
        }

    }

}


