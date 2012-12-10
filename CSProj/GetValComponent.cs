using System;
using System.Collections.Generic;
using System.Text;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System.Drawing;


namespace DYear
{
    public class GetValComponent : GH_Component
    {
        public GetValComponent()
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
}
