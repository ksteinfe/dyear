using System;
using System.Collections.Generic;
using System.Text;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System.Drawing;
using System.Linq;
using DYear.Statistics;

namespace DYear {


    public class Dhr_GradColorComponent : GH_Component {
        public Dhr_GradColorComponent()
            //Call the base constructor
            : base("Gradient Colorization", "GradColor", "Assigns a color value for each hour given, based on a given key and doman, using a single-interpolation gradient between two given colors.", "DYear", "Decorate") { }
        public override Grasshopper.Kernel.GH_Exposure Exposure { get { return GH_Exposure.primary; } }
        public override Guid ComponentGuid { get { return new Guid("{8DB38FB8-5B50-4DBC-B0B0-230757565D4E}"); } }
        protected override Bitmap Icon { get { return DYear.Properties.Resources.Olgay; } }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager) {
            pManager.RegisterParam(new GHParam_DHr(), "DHours", "Dhrs", "The Dhours to which to apply color values", GH_ParamAccess.list);
            pManager.Register_StringParam("Key", "Key", "The key on which to base colorization", GH_ParamAccess.item);
            pManager.Register_IntervalParam("Domain", "Rng", "The domain that will be used to map values unto colors.  Defaults to the range of given values.\nThe high end of the domain will correspond to the given high color, and the low end will correspond to the given low color.\nValues that fall outside of the given range will raise a warning.", GH_ParamAccess.item);
            pManager.Register_ColourParam("High Color", "Hi", "The color to assign to hours with high values.  Defaults to white.", Color.White, GH_ParamAccess.item);
            pManager.Register_ColourParam("Mid Color", "Mid", "The color to assign to hours with middle values.  Optional.", GH_ParamAccess.item);
            pManager.Register_ColourParam("High Color", "Lo", "The color to assign to hours with low values.  Defaults to black.", Color.Black, GH_ParamAccess.item);


            this.Params.Input[2].Optional = true;
            this.Params.Input[4].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager) {
            pManager.RegisterParam(new GHParam_DHr(), "DHours", "Dhrs", "The colorized Dhours", GH_ParamAccess.list);
            pManager.Register_ColourParam("Colors", "color", "The assigned colors", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA) {
            List<DHr> hours = new List<DHr>();
            string key = "";
            Color c0 = new Color();
            Color c1 = new Color();
            Color cm = new Color();
            bool do_mid = false;
            Interval domain = new Interval();
            if ((DA.GetDataList(0, hours)) && (DA.GetData(1, ref key)) && (DA.GetData(3, ref c1)) && (DA.GetData(5, ref c0))) {
                float[] vals = new float[0];
                if (!(DA.GetData(2, ref domain))) DHr.get_domain(key, hours.ToArray(), ref vals, ref domain);
                else {
                    vals = new float[hours.Count];
                    for (int h = 0; h < hours.Count; h++) vals[h] = hours[h].val(key);
                }

                if (DA.GetData(4, ref cm)) do_mid = true;

                List<Color> colors = new List<Color>();
                for (int h = 0; h < hours.Count; h++) {
                    double t = domain.NormalizedParameterAt(vals[h]);
                    if (t < 0) { this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Value falls below minimum of specified domain at index" + h); t = 0; }
                    if (t > 1) { this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Value falls above maximum of specified domain at index" + h); t = 1; }

                    Color c;
                    if (!do_mid) c = Util.InterpolateColor(c0, c1, t);
                    else {
                        if (t == 0.5) c = cm;
                        else if (t>0.5) c = Util.InterpolateColor(cm, c1, (t-0.5)*2);
                        else c = Util.InterpolateColor(c0, cm, t * 2);
                    }

                    colors.Add(c);
                    hours[h].color = c;
                }

                DA.SetDataList(0, hours);
                DA.SetDataList(1, colors);
            }
        }
    }

    public class Dhr_GradColor2Component : GH_Component {
        public Dhr_GradColor2Component()
            //Call the base constructor
            : base("Double Gradient Colorization", "GradColor2", "Assigns a color value for each hour given, based on a given key and doman, using a double-interpolation gradient between four given colors.", "DYear", "Decorate") { }
        public override Grasshopper.Kernel.GH_Exposure Exposure { get { return GH_Exposure.primary; } }
        public override Guid ComponentGuid { get { return new Guid("{ABFB05A1-E552-47E6-8F48-DAE90FD16825}"); } }
        protected override Bitmap Icon { get { return DYear.Properties.Resources.Olgay; } }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager) {
            pManager.RegisterParam(new GHParam_DHr(), "DHours", "Dhrs", "The Dhours to which to apply color values", GH_ParamAccess.list);
            pManager.Register_StringParam("Key A", "Key A", "The primary key on which to base colorization", GH_ParamAccess.item);
            pManager.Register_IntervalParam("Domain A", "Rng A", "The domain that will be used to map values unto colors for Key A.  Defaults to the range of given values for Key A.\nValues that fall outside of the given range will raise a warning.", GH_ParamAccess.item);
            pManager.Register_StringParam("Key B", "Key B", "The secondary key on which to base colorization", GH_ParamAccess.item);
            pManager.Register_IntervalParam("Domain B", "Rng B", "The domain that will be used to map values unto colors for Key B.  Defaults to the range of given values for Key B.\nValues that fall outside of the given range will raise a warning.", GH_ParamAccess.item);
            pManager.Register_ColourParam("A High, B High", "Hi-Hi", "The color to assign when A is high and B is high.  Defaults to red.", Color.Red, GH_ParamAccess.item);
            pManager.Register_ColourParam("A High, B Low", "Hi-Lo", "The color to assign when A is high and B is low.  Defaults to yellow.", Color.Yellow, GH_ParamAccess.item);
            pManager.Register_ColourParam("A Low, B High", "Lo-Hi", "The color to assign when A is high and B is high.  Defaults to blue.", Color.Blue, GH_ParamAccess.item);
            pManager.Register_ColourParam("A Low, B Low", "Lo-Lo", "The color to assign when A is high and B is low.  Defaults to white.", Color.White, GH_ParamAccess.item);

            this.Params.Input[2].Optional = true;
            this.Params.Input[4].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager) {
            pManager.RegisterParam(new GHParam_DHr(), "DHours", "Dhrs", "The colorized Dhours", GH_ParamAccess.list);
            pManager.Register_ColourParam("Colors", "color", "The assigned colors", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA) {
            List<DHr> hours = new List<DHr>();
            string key_a = "";
            string key_b = "";
            Color c11 = new Color();
            Color c10 = new Color();
            Color c01 = new Color();
            Color c00 = new Color();
            Interval domain_a = new Interval();
            Interval domain_b = new Interval();
            if ((DA.GetDataList(0, hours)) && (DA.GetData(1, ref key_a)) && (DA.GetData(3, ref key_b)) && (DA.GetData(5, ref c11)) && (DA.GetData(6, ref c10)) && (DA.GetData(7, ref c01)) && (DA.GetData(8, ref c00))   ) {
                float[] vals_a = new float[0];
                if (!(DA.GetData(2, ref domain_a))) DHr.get_domain(key_a, hours.ToArray(), ref vals_a, ref domain_a);
                else {
                    vals_a = new float[hours.Count];
                    for (int h = 0; h < hours.Count; h++) vals_a[h] = hours[h].val(key_a);
                }
                float[] vals_b = new float[0];
                if (!(DA.GetData(4, ref domain_b))) DHr.get_domain(key_b, hours.ToArray(), ref vals_b, ref domain_b);
                else {
                    vals_b = new float[hours.Count];
                    for (int h = 0; h < hours.Count; h++) vals_b[h] = hours[h].val(key_b);
                }

                List<Color> colors = new List<Color>();
                for (int h = 0; h < hours.Count; h++) {
                    double ta = domain_a.NormalizedParameterAt(vals_a[h]);
                    if (ta < 0) { this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Value for key A falls below minimum of specified domain at index" + h); continue; }
                    if (ta > 1) { this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Value for key A falls above maximum of specified domain at index" + h); continue; }
                   
                    double tb = domain_b.NormalizedParameterAt(vals_b[h]);
                    if (tb < 0) { this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Value for key B falls below minimum of specified domain at index" + h); continue; }
                    if (tb > 1) { this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Value for key B falls above maximum of specified domain at index" + h); continue; }

                    Color c0 = Util.InterpolateColor(c00, c10, ta);
                    Color c1 = Util.InterpolateColor(c01, c11, ta);
                    Color c = Util.InterpolateColor(c0, c1, tb);
                    colors.Add(c);
                    hours[h].color = c;
                }

                DA.SetDataList(0, hours);
                DA.SetDataList(1, colors);
            }
        }
    }



}
