using System;
using System.Collections.Generic;
using System.Text;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System.Drawing;
using DYear.Statistics;

namespace DYear
{
    public class Dhr_GetValComponent : GH_Component
    {
        public Dhr_GetValComponent()
            //Call the base constructor
            : base("Get Value", "GetVal", "Extracts a value from a Dhour", "DYear", "Manipulate")
        { }


        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.Register_StringParam("Value Key", "Key", "The name of the value to extract", GH_ParamAccess.item);
            pManager.RegisterParam(new GHParam_DHr(), "DHour", "Dhr", "The Dhour from which to extract a value", GH_ParamAccess.item);
            
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
            : base("Get Keys", "GetKeys", "Extracts the Keys from a Dhour or a list of Dhours", "DYear", "Manipulate")
        { }


        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.RegisterParam(new GHParam_DHr(), "DHours", "Dhrs", "The Dhours from which to extract values", GH_ParamAccess.list);
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


    public class Dhr_PeriodStatsComponent : GH_Component
    {

        public CType cycle_type;
        public enum CType { Yearly, Monthly, MonthlyDiurnal, Daily, Invalid };

        public Dhr_PeriodStatsComponent()
            //Call the base constructor
            : base("Periodic Statistics", "Stats", "Performs statistical operations over a given time period (daily, monthly, or monthly diurnal) on a year's worth of Dhours", "DYear", "Manipulate")
        { this.cycle_type = CType.Daily; }


        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            GHParam_DHr param = new GHParam_DHr();
            pManager.RegisterParam(param, "DHours", "Dhrs", "The Dhours from which to calculate statistics", GH_ParamAccess.list);
            pManager.Register_StringParam("Period", "P", "The time period to cycle through.  Choose 'yearly', 'monthly', 'monthly diurnal', or 'daily'.");
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.RegisterParam(new GHParam_DHr(), "Mean Hours",             "Mean", "Hours that represent the mean (average) values of all hours in the selected time cycle", GH_ParamAccess.list);
            pManager.RegisterParam(new GHParam_DHr(), "Mode Hours",             "Mode", "Hours that represent the mode (most frequent) values of all hours in the selected time cycle", GH_ParamAccess.list);
            pManager.RegisterParam(new GHParam_DHr(), "High Hours",             "Q4", "Hours that represent the highest values of all hours in the selected time cycle", GH_ParamAccess.list);
            pManager.RegisterParam(new GHParam_DHr(), "Upper Quantile Hours",   "Q3", "Hours that represent the upper quantile (0.75) values of all hours in the selected time cycle", GH_ParamAccess.list);
            pManager.RegisterParam(new GHParam_DHr(), "Median Hours",           "Q2", "Hours that represent the median values of all hours in the selected time cycle", GH_ParamAccess.list);
            pManager.RegisterParam(new GHParam_DHr(), "Lower Quantile Hours",   "Q1", "Hours that represent the lower quantile (0.25) values of all hours in the selected time cycle", GH_ParamAccess.list);
            pManager.RegisterParam(new GHParam_DHr(), "Low Hours",              "Q0", "Hours that represent the lowest values of all hours in the selected time cycle", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<DHr> dhrs = new List<DHr>();
            string period_string = "";
            if ((DA.GetDataList(0, dhrs)) && (DA.GetData(1, ref period_string)))
            {
                if (period_string == "") { return; }
                period_string = period_string.ToLowerInvariant().Trim();
                this.cycle_type = CType.Invalid;
                if (period_string.Contains("year")) { this.cycle_type = CType.Yearly; }
                if (period_string.Contains("month")) { this.cycle_type = CType.Monthly; }
                if (period_string.Contains("day") || period_string.Contains("daily") ) { this.cycle_type = CType.Daily; }
                if (period_string.Contains("diurnal")) { this.cycle_type = CType.MonthlyDiurnal; }

                string[] commonKeys = DHr.commonkeys(dhrs.ToArray());
                Dictionary<string, List<DHr>> stat_hours = new Dictionary<string, List<DHr>>();
                stat_hours.Add("meanHrs", new List<DHr>());
                stat_hours.Add("modeHrs", new List<DHr>());
                stat_hours.Add("highHrs", new List<DHr>());
                stat_hours.Add("uqHrs", new List<DHr>());
                stat_hours.Add("medianHrs", new List<DHr>());
                stat_hours.Add("lqHrs", new List<DHr>());
                stat_hours.Add("lowHrs", new List<DHr>());
                
                HourMask mask = new HourMask();

                switch (this.cycle_type){
                    case CType.Monthly:
                        for (int mth = 0; mth < 12; mth++)
                        {
                            mask.maskByMonthOfYear(mth);
                            CalculateStats(dhrs, commonKeys, stat_hours, mask);
                        }
                        break;
                    case CType.Daily:
                        for (int day = 0; day < 365; day++)
                        {
                            mask.maskByDayOfYear(day, day); // passing in same day twice masks to this single day
                            CalculateStats(dhrs, commonKeys, stat_hours, mask);
                        }
                        break;
                    case CType.Yearly:
                        mask.fillMask(true); // all hours may pass
                        CalculateStats(dhrs, commonKeys, stat_hours, mask);
                        break;
                    default:
                        this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Time period option not yet implimented.  Cannot produce statistics.");
                        break;
            }

                DA.SetDataList(0, stat_hours["meanHrs"]);
                DA.SetDataList(1, stat_hours["modeHrs"]);
                DA.SetDataList(2, stat_hours["highHrs"]);
                DA.SetDataList(3, stat_hours["uqHrs"]);
                DA.SetDataList(4, stat_hours["medianHrs"]);
                DA.SetDataList(5, stat_hours["lqHrs"]);
                DA.SetDataList(6, stat_hours["lowHrs"]);
            }
        }

        private static void CalculateStats(List<DHr> dhrs, string[] commonKeys, Dictionary<string, List<DHr>> stat_hours, HourMask mask)
        {
            Dictionary<string, List<float>> value_dict = new Dictionary<string, List<float>>();
            foreach (string key in commonKeys) { value_dict.Add(key, new List<float>()); }
            foreach (DHr hour in dhrs)
            {
                if (mask.eval(hour))
                {
                    foreach (string key in commonKeys) { value_dict[key].Add(hour.val(key)); }
                }
            }
            DHr meanHr = new DHr();
            DHr modeHr = new DHr();
            DHr highHr = new DHr();
            DHr uqHr = new DHr();
            DHr medianHr = new DHr();
            DHr lqHr = new DHr();
            DHr lowHr = new DHr();
            foreach (string key in commonKeys)
            {
                value_dict[key].Sort();
                meanHr.put(key, value_dict[key].Mean());
                foreach (float f in value_dict[key].Modes()) { modeHr.put(key, f); }
                highHr.put(key, value_dict[key][value_dict[key].Count - 1]);
                uqHr.put(key, value_dict[key].Quartile(0.75f));
                medianHr.put(key, value_dict[key].Median());
                lqHr.put(key, value_dict[key].Quartile(0.75f));
                lowHr.put(key, value_dict[key][0]);
            }
            stat_hours["meanHrs"].Add(meanHr);
            stat_hours["modeHrs"].Add(modeHr);
            stat_hours["highHrs"].Add(highHr);
            stat_hours["uqHrs"].Add(uqHr);
            stat_hours["medianHrs"].Add(medianHr);
            stat_hours["lqHrs"].Add(lqHr);
            stat_hours["lowHrs"].Add(lowHr);
        }
        public override Guid ComponentGuid { get { return new Guid("{FE2E05AF-B869-4C75-B3E8-7BA09EA3984B}"); } }
        protected override Bitmap Icon { get { return DYear.Properties.Resources.Component; } }
    }


    public class Dhr_RunningAverageComponent : GH_Component
    {
        public Dhr_RunningAverageComponent()
            //Call the base constructor
            : base("Rolling Mean", "RollMean", "Computes the rolling mean for each key in each Dhour in a collection of Dhours.\nReturns the modified set of Dhours", "DYear", "Manipulate")
        { }


        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.RegisterParam(new GHParam_DHr(), "DHours", "Dhrs", "The Dhours from which to extract values", GH_ParamAccess.list);
            pManager.Register_IntegerParam("Scope", "S", "The scope of the rolling mean - the number of nearby hours to average, both before and after the given hour", 24,GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {

             pManager.RegisterParam(new GHParam_DHr(),"DHours", "Dhrs", "The averaged Dhours", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<DHr> dhrs = new List<DHr>();
            int scope = -1;
            if ((DA.GetDataList(0, dhrs)) && (DA.GetData(1, ref scope)))
            {
                string[] commonKeys = DHr.commonkeys(dhrs.ToArray());

                List<DHr> hrsOut = new List<DHr>();
                for (int n = 0; n < dhrs.Count; n++)
                {
                    DHr dhr = new DHr(dhrs[n]);
                    foreach (string key in commonKeys) {
                        float sum = 0;
                        for (int di = -scope / 2; di <= scope / 2; di++)
                        {
                            int m = n + di;
                            while ((m < 0) || (m > dhrs.Count - 1))
                            {
                                if (m < 0) m = dhrs.Count + m;
                                if (m > dhrs.Count - 1) m = m - dhrs.Count;
                            }
                            sum += dhrs[m].val(key);
                        }
                        sum = sum / ((float) scope+1);
                        dhr.put(key, sum);
                    }
                    hrsOut.Add(dhr);
                }

                DA.SetDataList(0, hrsOut);
            }
        }
        public override Guid ComponentGuid { get { return new Guid("{1B7EAAA1-BB1F-4CBD-9538-D2B004D306D9}"); } }
        protected override Bitmap Icon { get { return DYear.Properties.Resources.Component; } }
    }


    public class Dhr_MaskHoursComponent : GH_Component
    {
        public Dhr_MaskHoursComponent()
            //Call the base constructor
            : base("Mask Hours", "MaskHours", "Filters a given set of Dhours through an Hourmask.\nOnly those hours allowed by the mask are returned", "DYear", "Manipulate")
        { }


        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.RegisterParam(new GHParam_DHr(), "DHours", "Dhrs", "The Dhours from which to extract values", GH_ParamAccess.list);
            pManager.RegisterParam(new GHParam_HourMask(), "Hourmask", "HMask", "The Hourmask that does the filtering", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {

            pManager.RegisterParam(new GHParam_DHr(), "DHours", "Dhrs", "The masked Dhours", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<DHr> hrsIn = new List<DHr>();
            HourMask mask = new HourMask();
            mask.fillMask(true);
            DA.GetData(1, ref mask);
            if (DA.GetDataList(0, hrsIn))
            {
                List<DHr> hrsOut = new List<DHr>();
                foreach (DHr hour in hrsIn) if (mask.eval(hour)) hrsOut.Add(hour);
                DA.SetDataList(0, hrsOut);
            }
        }
        public override Guid ComponentGuid { get { return new Guid("{7171C527-5E98-427D-9B91-200DA77F9F8D}"); } }
        protected override Bitmap Icon { get { return DYear.Properties.Resources.Component; } }
    }

}
