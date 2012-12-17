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
            pManager.RegisterParam(new GHParam_DHr(), "DHour", "Dhr", "The Dhour from which to extract a value", GH_ParamAccess.list);
            
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.Register_DoubleParam("Value", "Val", "The extracted value", GH_ParamAccess.list);
            pManager.Register_IntervalParam("Range", "Rng", "An interval that describes the range of values found in the given list of Dhours", GH_ParamAccess.list);
            
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<DHr> dhrs = new List<DHr>();
            string key = "";
            if ((DA.GetData(0, ref key))&&(DA.GetDataList(1, dhrs))) //if it works...
            {
                List<float> vals = new List<float>();
                float max = dhrs[0].val(key);
                float min = dhrs[0].val(key);
                foreach (DHr hr in dhrs)
                {
                    float val = hr.val(key);
                    vals.Add(val);
                    if (val > max) max = val;
                    if (val < min) min = val;
                }

                DA.SetDataList(0, vals);

                DA.SetData(1, new Interval(min, max));
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

    public class Dhr_LimitKeysComponent : GH_Component
    {
        public Dhr_LimitKeysComponent()
            //Call the base constructor
            : base("Limit Keys", "LimitKeys", "Removes unwanted keys from a Dhour or list of Dhours", "DYear", "Manipulate")
        { }


        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.Register_StringParam("Keys to Keep", "Keys", "The keys that should remain in the given Dhour", GH_ParamAccess.list);
            pManager.RegisterParam(new GHParam_DHr(), "DHour", "Dhr", "The given Dhour", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {

            pManager.RegisterParam(new GHParam_DHr(), "DHour", "Dhr", "The resulting Dhour", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            DHr hrIn = new DHr();
            if (DA.GetData(1, ref hrIn))
            {
                DHr hrOut = new DHr(hrIn);
                hrOut.clear();
                List<string> keys_to_keep = new List<string>();
                DA.GetDataList(0, keys_to_keep);
                foreach (string key in keys_to_keep)
                {
                    if (hrIn.containsKey(key)) hrOut.put(key, hrIn.val(key));
                    else this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "key not found in given hour: "+key+"\nIf you are streaming from a panel component did you remember to uncheck the 'multiline data' option?");
                }
                DA.SetData(0, hrOut);
            }
        }
        public override Guid ComponentGuid { get { return new Guid("{DE92A87D-73F0-46E4-AC6A-D4934587F2AF}"); } }
        protected override Bitmap Icon { get { return DYear.Properties.Resources.Component; } }
    }

    public class Dhr_MergeHoursComponent : GH_Component , IGH_VariableParameterComponent
    {
        public Dhr_MergeHoursComponent()
            //Call the base constructor
            : base("Merge Hours", "MergeHours", "Merges two streams of Dhours.\nLooks for matching pairs of Dhours (those sharing an index) from each stream.\nWhen a pair is found, their keys are merged.\nAll keys are appended with the nickname of the input stream.\nAll non-keyed properties (position, color, etc) of DHours are not included in results.\nNaming an input 'none' will supress key renaming.", "DYear", "Manipulate")
        {
            index_of_new_param = -1;
            total_params_added = -1;
        }


        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.RegisterParam(new GHParam_DHr(), "DHours", "A", "The first set of Dhours", GH_ParamAccess.list);
            pManager.RegisterParam(new GHParam_DHr(), "DHours", "B", "The second set of Dhours", GH_ParamAccess.list);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {

            pManager.RegisterParam(new GHParam_DHr(), "DHours", "Dhrs", "The merged Dhours", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {

            Dictionary<string, List<DHr>> input_dict = new Dictionary<string, List<DHr>>();
            for (int i = 0; i < Params.Input.Count; i++)
            {
                List<DHr> hrs = new List<DHr>();
                if (DA.GetDataList(i, hrs)) { input_dict.Add(Params.Input[i].NickName.ToLowerInvariant().Trim(), hrs); }
            }

            if (input_dict.Count >= 2)
            {
                List<int> hour_indices = new List<int>(); // holds the combined list of hours represented in all lists
                Dictionary<string, List<DHr>> clean_dict = new Dictionary<string, List<DHr>>();
                #region Clean and Sort Incoming Hours
                foreach (KeyValuePair<string, List<DHr>> entry in input_dict)
                {
                    List<int> dups = new List<int>();
                    List<DHr> temp = new List<DHr>();
                    foreach (DHr hr in entry.Value)
                    {
                        if (!dups.Contains(hr.hr)) { temp.Add(hr); dups.Add(hr.hr); }
                        else { this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Found a duplicate hour in second list of hours at index " + hr.hr + "\nPlease clean up your hourstreams before merging"); }
                        if (!hour_indices.Contains(hr.hr)) { hour_indices.Add(hr.hr); }
                    }

                    clean_dict.Add(entry.Key, temp);
                }
                #endregion

                List<DHr> hrsOut = new List<DHr>();
                foreach (int i in hour_indices)
                {
                    DHr hrOut = new DHr(i);
                    foreach (KeyValuePair<string, List<DHr>> entry in clean_dict)
                    {
                        foreach (DHr hr in entry.Value) foreach (string key in hr.keys)
                            {
                                if (entry.Key == "none") hrOut.put(key, hr.val(key));
                                else hrOut.put((key + " :: " + entry.Key).ToLowerInvariant().Trim(), hr.val(key));
                            }
                    }
                    hrsOut.Add(hrOut);
                }
                DA.SetDataList(0, hrsOut);
            }
        }
        public override Guid ComponentGuid { get { return new Guid("{5EC55608-99F1-4CEF-8D28-819EB7EFCD62}"); } }
        protected override Bitmap Icon { get { return DYear.Properties.Resources.Component; } }

        #region Variable Param Stuff

        private int index_of_new_param;
        private int total_params_added;
        int max_params = 7;
        int min_params = 2;
        char[] alpha = "CDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();

        public bool CanInsertParameter(GH_ParameterSide side, int index)
        {
            if (index < min_params) return false;
            if ((side == GH_ParameterSide.Input) && (this.Params.Input.Count < max_params)) return true;
            return false;
        }

        public bool CanRemoveParameter(GH_ParameterSide side, int index)
        {
            if (index < min_params) return false;
            if ((side == GH_ParameterSide.Input) && (this.Params.Input.Count > min_params)) return true;
            return false;
        }

        public IGH_Param CreateParameter(GH_ParameterSide side, int index) {
            index_of_new_param = index;
            total_params_added++;
            if (total_params_added >= alpha.Length) total_params_added = 0;
            return new GHParam_DHr();  
       }

        public bool DestroyParameter(GH_ParameterSide side, int index) { return true; }

        public void VariableParameterMaintenance()
        {
            if (index_of_new_param >= 0)
            {
                Params.Input[index_of_new_param].NickName = alpha[total_params_added].ToString();
                Params.Input[index_of_new_param].Access = GH_ParamAccess.list;
                Params.Input[index_of_new_param].Optional = true;
                index_of_new_param = -1;
            }
        }

        #endregion
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

                    case CType.MonthlyDiurnal:
                        for (int mth = 0; mth < 12; mth++) for (int hour = 0; hour < 24; hour++)
                        {
                            mask.maskByMonthAndHour(mth,hour);
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
            int average_hour_of_year = 0;
            int count = 0;
            foreach (DHr hour in dhrs)
            {
                if (mask.eval(hour))
                {
                    count++;
                    average_hour_of_year += hour.hr;
                    foreach (string key in commonKeys) { value_dict[key].Add(hour.val(key)); }
                }
            }
            average_hour_of_year = average_hour_of_year / count;
            DHr meanHr = new DHr(average_hour_of_year);
            DHr modeHr = new DHr(average_hour_of_year);
            DHr highHr = new DHr(average_hour_of_year);
            DHr uqHr = new DHr(average_hour_of_year);
            DHr medianHr = new DHr(average_hour_of_year);
            DHr lqHr = new DHr(average_hour_of_year);
            DHr lowHr = new DHr(average_hour_of_year);
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
