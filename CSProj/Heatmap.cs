using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System.Drawing;
using System.Drawing.Imaging;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel.Attributes;


namespace DYear
{
    public class Dhr_HeatmapComponent : GH_Component
    {

        public Dictionary<int, float> plotvals;
        public Interval plotdomain;

        public Dhr_HeatmapComponent()
            //Call the base constructor
            : base("Quick Heatmap", "Heatmap", "Given a list of DHrs and a key, displays a greyscale heatmap of the values associated with that key.\nRed and blue indicates hours that fall outside the given domain.", "DYear", "Visualize")
        {
            clearLocals();
        }
        public override Guid ComponentGuid { get { return new Guid("{709C00D7-4E99-46AB-AD4A-A7235C822726}"); } }
        protected override Bitmap Icon { get { return DYear.Properties.Resources.Component; } }
        public override Grasshopper.Kernel.GH_Exposure Exposure { get { return GH_Exposure.primary; } }

        void clearLocals()
        {
            plotvals = new Dictionary<int, float>();
            plotdomain = new Interval();
        }

        public bool isPlotable
        {
            get
            {
                if (RuntimeMessageLevel != GH_RuntimeMessageLevel.Blank) return false;
                if ((plotvals.Count > 0) && (plotdomain.IsValid)) return true;
                return false;
            }
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.RegisterParam(new GHParam_DHr(), "DHours", "Dhrs", "The Dhours to plot", GH_ParamAccess.list);
            pManager.Register_StringParam("Value Key", "Key", "The name of the value to plot", GH_ParamAccess.item);
            pManager.Register_IntervalParam("Domain", "Rng", "The [optional] domain to plot, with black corresponding to the low value and white to the high value.  Defaults to the max and min of the given values", GH_ParamAccess.item);
            Params.Input[2].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.RegisterParam(new GHParam_DHr(), "DHours", "Dhrs", "The Dhours that were plotted", GH_ParamAccess.list);
            pManager.Register_DoubleParam("Values", "Vals", "The values displayed in the heatmap", GH_ParamAccess.list);
            pManager.Register_IntervalParam("Range", "Rng", "An interval that describes the range of values found in the given list of Dhours for this key", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            clearLocals();
            List<DHr> dhrs = new List<DHr>();
            string key = "";
            if (DA.GetDataList(0, dhrs) && DA.GetData(1, ref key)) //if it works...
            {
                double[] vals = new double[dhrs.Count];
                double max = dhrs[0].val(key);
                double min = dhrs[0].val(key);
                for (int h = 0; h < dhrs.Count; h++)
                {
                    float val = dhrs[h].val(key);
                    vals[h] = val;
                    if (val > max) max = val;
                    if (val < min) min = val;
                    plotvals.Add(dhrs[h].hr, val);
                }
                plotdomain = new Interval(min, max);
                Interval givendomain = new Interval();
                if (DA.GetData(2, ref givendomain)) plotdomain = givendomain;

                DA.SetDataList(0, dhrs);
                DA.SetDataList(1, vals);
                DA.SetData(2, new Interval(min, max));
            }
        }

        public override void CreateAttributes() { m_attributes = new Dhr_HeatmapComponent_Attributes(this); }
    }
    
    public class Dhr_HeatmapComponent_Attributes : GH_ComponentAttributes
    {
        public Dhr_HeatmapComponent_Attributes(Dhr_HeatmapComponent owner) : base(owner) { }

        SizeF img_size = new SizeF(300, 100);
        SizeF param_size = new SizeF(40, 30);
        int buffer = 2;

        protected override void Layout()
        {
            float width = img_size.Width + (param_size.Width * 2) + (buffer * 2);
            float height = img_size.Height;
            Bounds = new RectangleF(Pivot, new SizeF(width, height));
            PointF cpt = new PointF(Pivot.X, Pivot.Y + height / 2);

            Owner.Params.Input[0].Attributes.Bounds = new RectangleF(new PointF(cpt.X, cpt.Y - param_size.Height * 1.5f), param_size);
            Owner.Params.Input[1].Attributes.Bounds = new RectangleF(new PointF(cpt.X, cpt.Y - param_size.Height * 0.5f), param_size);
            Owner.Params.Input[2].Attributes.Bounds = new RectangleF(new PointF(cpt.X, cpt.Y + param_size.Height * 0.5f), param_size);
            cpt.X = cpt.X + width;
            Owner.Params.Output[0].Attributes.Bounds = new RectangleF(new PointF(cpt.X - param_size.Width, cpt.Y - param_size.Height * 1.5f), param_size);
            Owner.Params.Output[1].Attributes.Bounds = new RectangleF(new PointF(cpt.X - param_size.Width, cpt.Y - param_size.Height * 0.5f), param_size);
            Owner.Params.Output[2].Attributes.Bounds = new RectangleF(new PointF(cpt.X - param_size.Width, cpt.Y + param_size.Height * 0.5f), param_size);
        }

        protected override void Render(GH_Canvas canvas, Graphics graphics, GH_CanvasChannel channel)
        {
            // Render all the wires that connect the Owner to all its Sources.
            if (channel == GH_CanvasChannel.Wires) base.Render(canvas, graphics, channel);

            // Render the parameter capsule and any additional text on top of it.
            if (channel == GH_CanvasChannel.Objects)
            {
                Dhr_HeatmapComponent realOwner = (Dhr_HeatmapComponent)Owner;

                // Define the default palette.
                GH_Palette palette = GH_Palette.Normal;
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
                GH_Capsule capsulein = GH_Capsule.CreateCapsule(new RectangleF(Bounds.X, Bounds.Y, param_size.Width, Bounds.Height), palette, new int[] { 6, 2, 2, 6 }, 5);
                GH_Capsule capsuleout = GH_Capsule.CreateCapsule(new RectangleF(Bounds.X + Bounds.Width - param_size.Width, Bounds.Y, param_size.Width, Bounds.Height), palette, new int[] { 2, 6, 6, 2 }, 5);

                foreach (IGH_Param p in Owner.Params.Input) capsulein.AddInputGrip(p.Attributes.InputGrip.Y);
                foreach (IGH_Param p in Owner.Params.Output) capsuleout.AddOutputGrip(p.Attributes.OutputGrip.Y);

                capsulein.Render(graphics, Selected, Owner.Locked, true);
                capsuleout.Render(graphics, Selected, Owner.Locked, true);
                capsulein.Dispose();
                capsuleout.Dispose();
                capsulein = null;
                capsuleout = null;

                // draw text labels for inputs and outputs
                StringFormat format = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center,
                    Trimming = StringTrimming.EllipsisCharacter
                };
                foreach (IGH_Param p in Owner.Params.Input) graphics.DrawString(p.NickName, GH_FontServer.Standard, Brushes.Black, p.Attributes.Bounds, format);
                foreach (IGH_Param p in Owner.Params.Output) graphics.DrawString(p.NickName, GH_FontServer.Standard, Brushes.Black, p.Attributes.Bounds, format);



                RectangleF imgBounds = new RectangleF(new PointF(Bounds.X + param_size.Width + buffer, Bounds.Y), img_size);


                //Bitmap bmp = Properties.Resources.Component;
                //if (realOwner.isPlotable) bmp = makeHeatmapImage(realOwner);
                //graphics.DrawImage(bmp, imgBounds);
                if (realOwner.isPlotable) drawHeatmapImage(realOwner, graphics, imgBounds);
                else graphics.FillRectangle(new SolidBrush(Color.FromArgb(64, 255, 255, 255)), imgBounds);

                // draw lines
                //graphics.DrawLine(new Pen(Color.White), new PointF(imgBounds.Left, imgBounds.Top), new PointF(imgBounds.Right, imgBounds.Top));
                //graphics.DrawLine(new Pen(Color.White), new PointF(imgBounds.Left, imgBounds.Bottom), new PointF(imgBounds.Right, imgBounds.Bottom));

            }
        }

        public void drawHeatmapImage(Dhr_HeatmapComponent Owner, Graphics graphics, RectangleF imgBounds)
        {
            SizeF pixelSize = new SizeF(imgBounds.Width / 365.0f, imgBounds.Height / 24.0f);
            int i = 0;
            for (int d = 0; d < 365; d++) for (int h = 0; h < 24; h++)
                {
                    Color c = Color.Transparent;
                    if (Owner.plotvals.ContainsKey(i))
                    {
                        double t = Owner.plotdomain.NormalizedParameterAt(Owner.plotvals[i]);
                        if (t < 0) c = Color.Blue;
                        else if (t > 1) c = Color.Red;
                        // else c = Color.FromArgb(255, (int)(255 * (1 - t)), (int)(255 * (1 - t)), (int)(255 * (1 - t))); // what? is black high or low?
                        else c = Color.FromArgb(255, (int)(255 * (t)), (int)(255 * (t)), (int)(255 * (t)));
                    }
                    SolidBrush brush = new SolidBrush(c);
                    System.Drawing.PointF pt = new System.Drawing.PointF(imgBounds.Location.X + pixelSize.Width * d, imgBounds.Location.Y + pixelSize.Height * h);
                    graphics.FillRectangle(brush, new RectangleF(pt, pixelSize));
                    i++;
                }


        }


        private Bitmap makeHeatmapImage_SUPERSEDED(Dhr_HeatmapComponent Owner)
        {
            int pixelWidth = 3;
            int pixelHeight = 10;
            Bitmap bmp = new Bitmap(365 * pixelWidth, 24 * pixelHeight);
            using (Graphics gfx = Graphics.FromImage(bmp))
            {
                int i = 0;
                for (int d = 0; d < 365; d++) for (int h = 0; h < 24; h++)
                    {
                        Color c = Color.Transparent;
                        if (Owner.plotvals.ContainsKey(i))
                        {
                            double t = Owner.plotdomain.NormalizedParameterAt(Owner.plotvals[i]);
                            if (t < 0) c = Color.Blue;
                            else if (t > 1) c = Color.Red;
                            else c = Color.FromArgb(255, (int)(255 * (1 - t)), (int)(255 * (1 - t)), (int)(255 * (1 - t)));
                        }
                        SolidBrush brush = new SolidBrush(c);
                        gfx.FillRectangle(brush, pixelWidth * d, pixelHeight * h, pixelWidth, pixelHeight);
                        i++;
                    }

            }
            return bmp;
        }

    }
}

