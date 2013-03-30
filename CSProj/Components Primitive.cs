using System;
using System.Collections.Generic;
using System.Text;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using System.Drawing;
using System.Linq;
using DYear.Statistics;

namespace DYear {


    public class Dhr_MakeHourComponent : GH_Component
    {
        public Dhr_MakeHourComponent()
            //Call the base constructor
            : base("Construct Hour", "Dhour", "Constructs a Dhour out of its constituent parts", "DYear", "Primitive") { }

        public override Guid ComponentGuid { get { return new Guid("{39DD748B-79FE-4B9E-9108-32ACF9751688}"); } }
        protected override Bitmap Icon { get { return DYear.Properties.Resources.Olgay; } }
        public override Grasshopper.Kernel.GH_Exposure Exposure { get { return GH_Exposure.primary | GH_Exposure.obscure; } }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.Register_IntegerParam("Hour Number", "Hr", "The hour of the year to construct, =between 0 and 8759.  Defaults to -1, which produces an invalid Dhour.", -1, GH_ParamAccess.list);
            pManager.Register_StringParam("Keys", "Keys", "The named keys to store in this Dhour. Must be a list of equal length to the 'Vals' parameter", GH_ParamAccess.list);
            pManager.Register_DoubleParam("Values", "Vals", "The values to store in this Dhour.  Must be a list of equal length to the 'Keys' parameter", GH_ParamAccess.tree);
            pManager.Register_ColourParam("Color", "Clr", "Optional.  A color assigned to this hour.  Hours are typically assigned colors during an analysis in preparation for visualization", GH_ParamAccess.list);
            pManager.Register_PointParam("Posistion", "Pt", "Optional.  A point assigned to this hour. Hours are typically assigned positions during an analysis in preparation for visualization", GH_ParamAccess.list);

            this.Params.Input[3].Optional = true;
            this.Params.Input[4].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.RegisterParam(new GHParam_DHr(), "Dhour", "Dhour", "The resulting Dhour.", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<int> hrs = new List<int>();
            List<string> keys = new List<string>();
            Grasshopper.Kernel.Data.GH_Structure<Grasshopper.Kernel.Types.GH_Number> valtree = new Grasshopper.Kernel.Data.GH_Structure<Grasshopper.Kernel.Types.GH_Number>();
            if ((DA.GetDataList(1, keys)) && (DA.GetDataTree(2, out valtree)) && (DA.GetDataList(0, hrs)))
            {

                bool has_color = false;
                List<Color> colors = new List<Color>();
                has_color = DA.GetDataList(4, colors);

                bool has_pt = false;
                List<Point3d> points = new List<Point3d>();
                has_pt = DA.GetDataList(4, points);

                if (hrs.Count != valtree.Branches.Count) this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "List matching error.  Hours and Vals must match.  If you pass in more than one hour number, then you must pass in a tree of values with one branch per hour number, and vice-versa.");
                else foreach (List<GH_Number> branch in valtree.Branches) if (keys.Count != branch.Count) this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "List matching error.  Keys and Vals must offer lists of the same length.  If you pass in a tree of values, each branch must contain a list of the same length as the list of keys.");
                        else
                        {
                            if (((has_color) && (colors.Count != hrs.Count)) || ((has_pt) && (points.Count != hrs.Count)))
                            {
                                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "List matching error.");
                                return;
                            }
                            List<DHr> hours = new List<DHr>();
                            for (int n = 0; n < valtree.Branches.Count; n++)
                            {
                                DHr hr = new DHr(hrs[n]);
                                for (int m = 0; m < keys.Count; m++) hr.put(keys[m], (float)valtree.Branches[n][m].Value);
                                if (has_color) hr.color = colors[n];
                                if (has_pt) hr.pos = points[n];
                                hours.Add(hr);
                            }
                            DA.SetDataList(0, hours);
                        }
            }
        }

    }

    public class Dhr_DecomposeHourComponent : GH_Component
    {
        public Dhr_DecomposeHourComponent()
            //Call the base constructor
            : base("Decompose Hour", "Dhour", "Decomposes a Dhour into its constituent parts", "DYear", "Primitive") { }

        public override Guid ComponentGuid { get { return new Guid("{37481A99-FAF7-45FF-BC37-53F4A0D93481}"); } }
        protected override Bitmap Icon { get { return DYear.Properties.Resources.Olgay; } }
        public override Grasshopper.Kernel.GH_Exposure Exposure { get { return GH_Exposure.primary | GH_Exposure.obscure; } }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.RegisterParam(new GHParam_DHr(), "DHour", "Dhr", "The Dhour to decompose.", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.Register_IntegerParam("Hour Number", "Hr", "The hour of the year represented by this Dhour.", GH_ParamAccess.item);
            pManager.Register_StringParam("Keys", "Keys", "The keys stored in this Dhour.", GH_ParamAccess.list);
            pManager.Register_DoubleParam("Values", "Vals", "The values stored in this Dhour", GH_ParamAccess.list);
            pManager.Register_ColourParam("Color", "Clr", "The color assigned to this hour.", GH_ParamAccess.item);
            pManager.Register_PointParam("Posistion", "Pt", "The point assigned to this hour.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            DHr dhr = new DHr();
            if (DA.GetData(0, ref dhr))
            {
                DA.SetData(0, dhr.hr);
                DA.SetDataList(1, dhr.keys);
                DA.SetDataList(2, dhr.values);
                DA.SetData(3, dhr.color);
                DA.SetData(4, dhr.pos);
            }
        }

    }
        
    public class Dhr_GetValComponent : GH_Component {
        public Dhr_GetValComponent()
            //Call the base constructor
            : base("Get Value", "GetVal", "Extracts a value from a Dhour", "DYear", "Primitive") { }
        public override Grasshopper.Kernel.GH_Exposure Exposure { get { return GH_Exposure.secondary; } }
        public override Guid ComponentGuid { get { return new Guid("{1DB488D9-7709-423B-BAA3-F8E91E4185B1}"); } }
        protected override Bitmap Icon { get { return DYear.Properties.Resources.Olgay; } }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager) {
            pManager.Register_StringParam("Value Key", "Key", "The name of the value to extract", GH_ParamAccess.item);
            pManager.RegisterParam(new GHParam_DHr(), "DHour", "Dhr", "The Dhour from which to extract a value", GH_ParamAccess.list);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager) {
            pManager.Register_DoubleParam("Value", "Val", "The extracted value", GH_ParamAccess.list);
            pManager.Register_IntervalParam("Range", "Rng", "An interval that describes the range of values found in the given list of Dhours for this key", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA) {
            List<DHr> dhrs = new List<DHr>();
            string key = "";
            if ((DA.GetData(0, ref key)) && (DA.GetDataList(1, dhrs))) //if it works...
            {
                List<float> vals = new List<float>();
                float max = dhrs[0].val(key);
                float min = dhrs[0].val(key);
                foreach (DHr hr in dhrs) {
                    if (!hr.containsKey(key))
                    {
                        this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "key not found");
                    }
                    else
                    {
                        float val = hr.val(key);
                        vals.Add(val);
                        if (val > max) max = val;
                        if (val < min) min = val;
                    }
                }

                DA.SetDataList(0, vals);
                DA.SetData(1, new Interval(min, max));
            }
        }


    }

    public class Dhr_GetKeysComponent : GH_Component {
        public Dhr_GetKeysComponent()
            //Call the base constructor
            : base("Get Keys", "GetKeys", "Extracts the Keys from a Dhour or a list of Dhours", "DYear", "Primitive") { }
        public override Grasshopper.Kernel.GH_Exposure Exposure { get { return GH_Exposure.secondary | GH_Exposure.obscure; } }
        public override Guid ComponentGuid { get { return new Guid("{4093D0D1-6533-4196-80E1-FDD4FC880995}"); } }
        protected override Bitmap Icon { get { return DYear.Properties.Resources.Olgay; } }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager) {
            pManager.RegisterParam(new GHParam_DHr(), "DHours", "Dhrs", "The Dhours from which to extract values", GH_ParamAccess.list);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager) {

            pManager.Register_StringParam("Common Keys", "CKey", "The keys common to all Dhours", GH_ParamAccess.item);
            pManager.Register_StringParam("Orphan Keys", "OKey", "The keys found in some Dhours, but not all", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA) {
            List<DHr> dhrs = new List<DHr>();
            if (DA.GetDataList(0, dhrs)) {
                List<string> allKeys = new List<string>();
                foreach (DHr hr in dhrs) {
                    if (hr.IsValid) {
                        foreach (string key in hr.keys) { if (!allKeys.Contains(key)) { allKeys.Add(key); } }
                    }
                }
                List<string> commonKeys = new List<string>();
                List<string> orphanedKeys = new List<string>();
                foreach (string key in allKeys) {
                    bool isCommon = true;
                    foreach (DHr hr in dhrs) {
                        if (!hr.Value.m_vals.ContainsKey(key)) { isCommon = false; break; }
                    }
                    if (isCommon) { commonKeys.Add(key); } else {
                        orphanedKeys.Add(key);
                    }
                }
                DA.SetDataList(0, commonKeys);
                DA.SetDataList(1, orphanedKeys);
            }
        }

    }

    public class Dhr_GetColorComponent : GH_Component
    {
        public Dhr_GetColorComponent()
            //Call the base constructor
            : base("Get Color", "GetColor", "Extracts the color from a Dhour", "DYear", "Primitive") { }
        public override Grasshopper.Kernel.GH_Exposure Exposure { get { return GH_Exposure.secondary | GH_Exposure.obscure; } }
        public override Guid ComponentGuid { get { return new Guid("{3CC6C0C9-D78F-41DE-A924-B3E8C1BEC9FA}"); } }
        protected override Bitmap Icon { get { return DYear.Properties.Resources.Olgay; } }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.RegisterParam(new GHParam_DHr(), "DHour", "Dhr", "The Dhour from which to extract a color", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {

            pManager.Register_ColourParam("Color", "Clr", "The color of the Dhour", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            DHr dhr = new DHr();
            if (DA.GetData(0, ref dhr)) DA.SetData(0, dhr.color);
        }

    }

    public class Dhr_GetPositionComponent : GH_Component
    {
        public Dhr_GetPositionComponent()
            //Call the base constructor
            : base("Get Position", "GetPos", "Extracts the position from a Dhour", "DYear", "Primitive") { }
        public override Grasshopper.Kernel.GH_Exposure Exposure { get { return GH_Exposure.secondary | GH_Exposure.obscure; } }
        public override Guid ComponentGuid { get { return new Guid("{0302451B-3C14-4C92-BD58-136C2B787B62}"); } }
        protected override Bitmap Icon { get { return DYear.Properties.Resources.Olgay; } }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.RegisterParam(new GHParam_DHr(), "DHour", "Dhr", "The Dhour from which to extract a position", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {

            pManager.Register_PointParam("Position", "Pt", "The position of the Dhour", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            DHr dhr = new DHr();
            if (DA.GetData(0, ref dhr)) DA.SetData(0, dhr.pos);
        }

    }


}
