using System;
using System.Collections.Generic;
using System.Text;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System.Drawing;


namespace DYear
{
    public class Dhr_GetValComponent : GH_Component
    {
        public Dhr_GetValComponent()
            //Call the base constructor
            : base("Get Value","GetVal","Extracts a value from a DHr","DYear","Manipulate")
        { }


        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.Register_StringParam("Value Key", "Key", "The name of the value to extract", GH_ParamAccess.item);

            GHParam_DHr param = new GHParam_DHr();
            pManager.RegisterParam(param, "DHour", "Dhr", "The Dhour from which to extract a value", GH_ParamAccess.item);
            
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.Register_DoubleParam("Value", "Val", "The extracted value", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            DHr dhr = new DHr();
            string key = "";
            if ((DA.GetData(0, ref key))&&(DA.GetData(1, ref dhr))) //if it works...
            {
                DA.SetData(0, dhr.val(key));
            }
        }

        public override Guid ComponentGuid{get {return new Guid("{1DB488D9-7709-423B-BAA3-F8E91E4185B1}");}}
        protected override Bitmap Icon { get { return DYear.Properties.Resources.Component; } }

    }


    public class Dhr_GetKeysComponent : GH_Component
    {
        public Dhr_GetKeysComponent()
            //Call the base constructor
            : base("Get Keys", "GetKeys", "Extracts the Keys from a DHr or a list of DHrs", "DYear", "Manipulate")
        { }


        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            GHParam_DHr param = new GHParam_DHr();
            pManager.RegisterParam(param, "DHour", "Dhr", "The Dhour from which to extract a value", GH_ParamAccess.list);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {

            pManager.Register_StringParam("Common Keys", "CKey", "The keys common to all hours", GH_ParamAccess.item);
            pManager.Register_StringParam("Orphan Keys", "OKey", "The keys found in some hours, but not all", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<DHr> dhrs = new List<DHr>();
            if (DA.GetDataList(0, dhrs))
            {
                List<string> allKeys = new List<string>();
                foreach (DHr hr in dhrs)
                {
                    if (hr.IsValid)
                    {
                        foreach (string key in hr.keys) { if (!allKeys.Contains(key)) { allKeys.Add(key); } }
                    }
                }
                List<string> commonKeys = new List<string>();
                List<string> orphanedKeys = new List<string>();
                foreach (string key in allKeys)
                {
                    bool isCommon = true;
                    foreach (DHr hr in dhrs)
                    {
                        if (!hr.Value.m_vals.ContainsKey(key)) { isCommon = false; break; }
                    }
                    if (isCommon) { commonKeys.Add(key); }
                    else
                    {
                        orphanedKeys.Add(key);
                    }
                }
                DA.SetDataList(0, commonKeys);
                DA.SetDataList(1, orphanedKeys);
            }
        }
        public override Guid ComponentGuid { get { return new Guid("{4093D0D1-6533-4196-80E1-FDD4FC880995}"); } }
        protected override Bitmap Icon { get { return DYear.Properties.Resources.Component; } }
    }


}
