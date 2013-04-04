using System;
using System.Collections.Generic;
using System.Text;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System.Drawing;
using System.Linq;
using DYear.Statistics;

namespace DYear {
    
    public class Dhr_LimitKeysComponent : GH_Component {
        public Dhr_LimitKeysComponent()
            //Call the base constructor
            : base("Limit Keys", "LimitKeys", "Removes unwanted keys from a Dhour or list of Dhours", "DYear", "Filter") { }
        public override Grasshopper.Kernel.GH_Exposure Exposure { get { return GH_Exposure.primary; } }
        public override Guid ComponentGuid { get { return new Guid("{DE92A87D-73F0-46E4-AC6A-D4934587F2AF}"); } }
        protected override Bitmap Icon { get { return DYear.Properties.Resources.Olgay; } }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager) {
            pManager.Register_StringParam("Keys to Keep", "Keys", "The keys that should remain in the given Dhour", GH_ParamAccess.list);
            pManager.RegisterParam(new GHParam_DHr(), "DHour", "Dhr", "The given Dhour", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager) {

            pManager.RegisterParam(new GHParam_DHr(), "DHour", "Dhr", "The resulting Dhour", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA) {
            DHr hrIn = new DHr();
            if (DA.GetData(1, ref hrIn)) {
                DHr hrOut = new DHr(hrIn);
                hrOut.clear();
                List<string> keys_to_keep = new List<string>();
                DA.GetDataList(0, keys_to_keep);
                foreach (string key in keys_to_keep) {
                    if (hrIn.containsKey(key)) hrOut.put(key, hrIn.val(key));
                    else this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "key not found in given hour: " + key + "\nIf you are streaming from a panel component did you remember to uncheck the 'multiline data' option?");
                }
                DA.SetData(0, hrOut);
            }
        }

    }

    public class Dhr_MergeHoursComponent : GH_Component, IGH_VariableParameterComponent {
        public Dhr_MergeHoursComponent()
            //Call the base constructor
            : base("Merge Hours", "MergeHours", "Merges two streams of Dhours.\nLooks for matching pairs of Dhours (those sharing an index) from each stream.\nWhen a pair is found, their keys are merged.\nAll keys are appended with the nickname of the input stream.\nAll non-keyed properties (position, color, etc) of DHours are not included in results.\nNaming an input 'none' will supress key renaming.", "DYear", "Filter") {
            index_of_new_param = -1;
            total_params_added = -1;
        }
        public override Grasshopper.Kernel.GH_Exposure Exposure { get { return GH_Exposure.primary; } }
        public override Guid ComponentGuid { get { return new Guid("{5EC55608-99F1-4CEF-8D28-819EB7EFCD62}"); } }
        protected override Bitmap Icon { get { return DYear.Properties.Resources.Olgay; } }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager) {
            pManager.RegisterParam(new GHParam_DHr(), "DHours", "A", "The first set of Dhours", GH_ParamAccess.list);
            pManager.RegisterParam(new GHParam_DHr(), "DHours", "B", "The second set of Dhours", GH_ParamAccess.list);

            pManager[0].Optional = false;
            pManager[1].Optional = false;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager) {

            pManager.RegisterParam(new GHParam_DHr(), "DHours", "Dhrs", "The merged Dhours", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA) {

            List<DHr>[] inputs = new List<DHr>[Params.Input.Count];
            for (int i = 0; i < Params.Input.Count; i++) {
                List<DHr> hrs = new List<DHr>();
                if (DA.GetDataList(i, hrs)) { inputs[i] = hrs; }
            }

            int hr_count = inputs[0].Count;
            int first_hour_int = inputs[0][0].hr;
            for (int i = 1; i < inputs.Length; i++) {
                if (inputs[i].Count != hr_count) hr_count = -1;
                if (inputs[i][0].hr != first_hour_int) first_hour_int = -999;
            }

            if ((hr_count > 0) && (first_hour_int >= -1)) {
                DHr[] hrsOut = new DHr[hr_count];
                for (int h = 0; h < hr_count; h++) hrsOut[h] = (new DHr(inputs[0][h].hr));

                for (int i = 0; i < inputs.Length; i++) {
                    string input_nickname = Params.Input[i].NickName;
                    for (int h = 0; h < hr_count; h++) {
                        DHr hr = inputs[i][h];
                        foreach (string key in hr.keys) {
                            if (input_nickname == "none") hrsOut[h].put(key, hr.val(key));
                            else hrsOut[h].put((key + " :: " + input_nickname).ToLowerInvariant().Trim(), hr.val(key));
                        }
                    }
                }
                DA.SetDataList(0, hrsOut);
            } else {
                // here we handle 

                List<int> hour_indices = new List<int>(); // holds the combined list of hours represented in all lists
                foreach (List<DHr> hours in inputs) {
                    foreach (DHr hr in hours) if (!hour_indices.Contains(hr.hr)) hour_indices.Add(hr.hr);
                }

                DHr[] hrsOut = new DHr[hour_indices.Count];
                foreach (int hour_index in hour_indices) hrsOut[hour_index] = (new DHr(hour_index));

                for (int i = 0; i < inputs.Length; i++) {
                    List<DHr> hours = inputs[i];
                    string input_nickname = Params.Input[i].NickName;
                    foreach (int hour_index in hour_indices) {
                        DHr hr = hours.Find(hr_src => hr_src.hr == hour_index);
                        foreach (string key in hr.keys) {
                            if (input_nickname == "none") hrsOut[hour_index].put(key, hr.val(key));
                            else hrsOut[hour_index].put((key + " :: " + input_nickname).ToLowerInvariant().Trim(), hr.val(key));
                        }
                    }
                }
                DA.SetDataList(0, hrsOut);
            }

        }


        #region Variable Param Stuff

        private int index_of_new_param;
        private int total_params_added;
        int max_params = 7;
        int min_params = 2;
        char[] alpha = "CDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();

        public bool CanInsertParameter(GH_ParameterSide side, int index) {
            if (index < min_params) return false;
            if ((side == GH_ParameterSide.Input) && (this.Params.Input.Count < max_params)) return true;
            return false;
        }

        public bool CanRemoveParameter(GH_ParameterSide side, int index) {
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

        public void VariableParameterMaintenance() {
            if (index_of_new_param >= 0) {
                Params.Input[index_of_new_param].NickName = alpha[total_params_added].ToString();
                Params.Input[index_of_new_param].Access = GH_ParamAccess.list;
                Params.Input[index_of_new_param].Optional = false;
                index_of_new_param = -1;
            }
        }

        #endregion
    }

    public class Dhr_PeriodStatsComponent : GH_Component {

        public CType cycle_type;
        public enum CType { Yearly, Monthly, MonthlyDiurnal, Weekly, WeeklyDiurnal, Daily, Invalid };

        public Dhr_PeriodStatsComponent()
            //Call the base constructor
            : base("Periodic Statistics", "Stats", "Performs statistical operations over a given time period (daily, monthly, or monthly diurnal) on a year's worth of Dhours", "DYear", "Filter") { this.cycle_type = CType.Daily; }
        public override Grasshopper.Kernel.GH_Exposure Exposure { get { return GH_Exposure.secondary; } }
        public override Guid ComponentGuid { get { return new Guid("{FE2E05AF-B869-4C75-B3E8-7BA09EA3984B}"); } }
        protected override Bitmap Icon { get { return DYear.Properties.Resources.Olgay; } }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager) {
            GHParam_DHr param = new GHParam_DHr();
            pManager.RegisterParam(param, "DHours", "Dhrs", "The Dhours from which to calculate statistics", GH_ParamAccess.list);
            pManager.Register_StringParam("Period", "P", "The time period to cycle through.  Choose 'yearly', 'monthly', 'monthly diurnal', 'weekly', 'weekly diurnal', or 'daily'.");
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager) {
            pManager.RegisterParam(new GHParam_DHr(), "Mean Hours", "Mean", "Hours that represent the mean (average) values of all hours in the selected time cycle", GH_ParamAccess.tree);
            pManager.RegisterParam(new GHParam_DHr(), "Mode Hours", "Mode", "Hours that represent the mode (most frequent) values of all hours in the selected time cycle", GH_ParamAccess.tree);
            pManager.RegisterParam(new GHParam_DHr(), "High Hours", "Q4", "Hours that represent the highest values of all hours in the selected time cycle", GH_ParamAccess.tree);
            pManager.RegisterParam(new GHParam_DHr(), "UpperQuartile  Hours", "Q3", "Hours that represent the upper quartile (0.75) values of all hours in the selected time cycle", GH_ParamAccess.tree);
            pManager.RegisterParam(new GHParam_DHr(), "Median Hours", "Q2", "Hours that represent the median values of all hours in the selected time cycle", GH_ParamAccess.tree);
            pManager.RegisterParam(new GHParam_DHr(), "Lower Quartile Hours", "Q1", "Hours that represent the lower quartile (0.25) values of all hours in the selected time cycle", GH_ParamAccess.tree);
            pManager.RegisterParam(new GHParam_DHr(), "Low Hours", "Q0", "Hours that represent the lowest values of all hours in the selected time cycle", GH_ParamAccess.tree);
            pManager.RegisterParam(new GHParam_DHr(), "Sum Hours", "Sum", "Hours that represent the summation of the values of all hours in the selected time cycle", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA) {
            List<DHr> dhrs = new List<DHr>();
            string period_string = "";
            if ((DA.GetDataList(0, dhrs)) && (DA.GetData(1, ref period_string))) {
                if (period_string == "") { return; }
                period_string = period_string.ToLowerInvariant().Trim();
                this.cycle_type = CType.Invalid;
                if (period_string.Contains("year")) { this.cycle_type = CType.Yearly; } else if (period_string.Contains("monthly diurnal")) { this.cycle_type = CType.MonthlyDiurnal; } else if (period_string.Contains("month")) { this.cycle_type = CType.Monthly; } else if (period_string.Contains("day") || period_string.Contains("daily")) { this.cycle_type = CType.Daily; } else if (period_string.Contains("weekly diurnal")) { this.cycle_type = CType.WeeklyDiurnal; } else if (period_string.Contains("weekly")) { this.cycle_type = CType.Weekly; } else {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "I don't understand the time period you're looking for.\nPlease choose 'yearly', 'monthly', 'monthly diurnal', 'weekly', 'weekly diurnal', or 'daily'.");
                }


                string[] commonKeys = DHr.commonkeys(dhrs.ToArray());
                Dictionary<string, List<DHr>> stat_hours = new Dictionary<string, List<DHr>>();
                InitStatHours(ref stat_hours);

                HourMask mask = new HourMask();

                switch (this.cycle_type) {

                    case CType.MonthlyDiurnal:
                    case CType.WeeklyDiurnal:
                        Grasshopper.Kernel.Data.GH_Structure<DHr> meanTree = new Grasshopper.Kernel.Data.GH_Structure<DHr>();
                        Grasshopper.Kernel.Data.GH_Structure<DHr> modeTree = new Grasshopper.Kernel.Data.GH_Structure<DHr>();
                        Grasshopper.Kernel.Data.GH_Structure<DHr> highTree = new Grasshopper.Kernel.Data.GH_Structure<DHr>();
                        Grasshopper.Kernel.Data.GH_Structure<DHr> uqTree = new Grasshopper.Kernel.Data.GH_Structure<DHr>();
                        Grasshopper.Kernel.Data.GH_Structure<DHr> medianTree = new Grasshopper.Kernel.Data.GH_Structure<DHr>();
                        Grasshopper.Kernel.Data.GH_Structure<DHr> lqTree = new Grasshopper.Kernel.Data.GH_Structure<DHr>();
                        Grasshopper.Kernel.Data.GH_Structure<DHr> lowTree = new Grasshopper.Kernel.Data.GH_Structure<DHr>();
                        Grasshopper.Kernel.Data.GH_Structure<DHr> sumTree = new Grasshopper.Kernel.Data.GH_Structure<DHr>();

                        switch (this.cycle_type) {
                            case CType.MonthlyDiurnal:
                                for (int mth = 0; mth < 12; mth++) {
                                    InitStatHours(ref stat_hours);
                                    for (int hour = 0; hour < 24; hour++) {
                                        mask.maskByMonthAndHour(mth, hour);
                                        int hh = Util.hourOfYearFromDatetime(Util.baseDatetime().AddMonths(mth).AddHours(hour)) + 1; // had to add one, looks like Util function was designed for parsing non-zero-indexed hours
                                        CalculateStats(dhrs, commonKeys, stat_hours, mask, hh, true);
                                    }
                                    meanTree.AppendRange(stat_hours["meanHrs"], new Grasshopper.Kernel.Data.GH_Path(mth));
                                    modeTree.AppendRange(stat_hours["modeHrs"], new Grasshopper.Kernel.Data.GH_Path(mth));
                                    highTree.AppendRange(stat_hours["highHrs"], new Grasshopper.Kernel.Data.GH_Path(mth));
                                    uqTree.AppendRange(stat_hours["uqHrs"], new Grasshopper.Kernel.Data.GH_Path(mth));
                                    medianTree.AppendRange(stat_hours["medianHrs"], new Grasshopper.Kernel.Data.GH_Path(mth));
                                    lqTree.AppendRange(stat_hours["lqHrs"], new Grasshopper.Kernel.Data.GH_Path(mth));
                                    lowTree.AppendRange(stat_hours["lowHrs"], new Grasshopper.Kernel.Data.GH_Path(mth));
                                    sumTree.AppendRange(stat_hours["sumHrs"], new Grasshopper.Kernel.Data.GH_Path(mth));
                                }
                                break;
                            case CType.WeeklyDiurnal:
                                for (int wk = 0; wk < 52; wk++) {
                                    InitStatHours(ref stat_hours);
                                    for (int hour = 0; hour < 24; hour++) {
                                        mask.maskByWeekAndHour(wk, hour);
                                        int hh = Util.hourOfYearFromDatetime(Util.baseDatetime().AddDays(wk * 7).AddHours(hour)) + 1; // had to add one, looks like Util function was designed for parsing non-zero-indexed hours
                                        CalculateStats(dhrs, commonKeys, stat_hours, mask, hh, true);
                                    }
                                    meanTree.AppendRange(stat_hours["meanHrs"], new Grasshopper.Kernel.Data.GH_Path(wk));
                                    modeTree.AppendRange(stat_hours["modeHrs"], new Grasshopper.Kernel.Data.GH_Path(wk));
                                    highTree.AppendRange(stat_hours["highHrs"], new Grasshopper.Kernel.Data.GH_Path(wk));
                                    uqTree.AppendRange(stat_hours["uqHrs"], new Grasshopper.Kernel.Data.GH_Path(wk));
                                    medianTree.AppendRange(stat_hours["medianHrs"], new Grasshopper.Kernel.Data.GH_Path(wk));
                                    lqTree.AppendRange(stat_hours["lqHrs"], new Grasshopper.Kernel.Data.GH_Path(wk));
                                    lowTree.AppendRange(stat_hours["lowHrs"], new Grasshopper.Kernel.Data.GH_Path(wk));
                                    sumTree.AppendRange(stat_hours["sumHrs"], new Grasshopper.Kernel.Data.GH_Path(wk));
                                }
                                break;
                        }
                        DA.SetDataTree(0, meanTree);
                        DA.SetDataTree(1, modeTree);
                        DA.SetDataTree(2, highTree);
                        DA.SetDataTree(3, uqTree);
                        DA.SetDataTree(4, medianTree);
                        DA.SetDataTree(5, lqTree);
                        DA.SetDataTree(6, lowTree);
                        DA.SetDataTree(7, sumTree);
                        break;


                    case CType.Daily:
                        for (int day = 0; day < 365; day++) {
                            mask.maskByDayOfYear(day, day); // passing in same day twice masks to this single day
                            int hh = Util.hourOfYearFromDatetime(Util.baseDatetime().AddDays(day).AddHours(0)) + 1; // had to add one, looks like Util function was designed for parsing non-zero-indexed hours
                            CalculateStats(dhrs, commonKeys, stat_hours, mask, hh, true);
                        }
                        SetOutputData(DA, stat_hours);
                        break;

                    case CType.Weekly:
                        for (int wk = 0; wk < 52; wk++) {
                            mask.maskByWeek(wk);
                            int hh = Util.hourOfYearFromDatetime(Util.baseDatetime().AddDays(wk * 7).AddHours(0)) + 1; // had to add one, looks like Util function was designed for parsing non-zero-indexed hours
                            CalculateStats(dhrs, commonKeys, stat_hours, mask, hh, true);
                        }
                        SetOutputData(DA, stat_hours);
                        break;

                    case CType.Monthly:
                        for (int mth = 0; mth < 12; mth++) {
                            mask.maskByMonthOfYear(mth);
                            int hh = Util.hourOfYearFromDatetime(Util.baseDatetime().AddMonths(mth).AddHours(0)) + 1; // had to add one, looks like Util function was designed for parsing non-zero-indexed hours
                            CalculateStats(dhrs, commonKeys, stat_hours, mask, hh, true);
                        }
                        SetOutputData(DA, stat_hours);
                        break;


                    case CType.Yearly:
                        mask.fillMask(true); // all hours may pass
                        int hhh = Util.hourOfYearFromDatetime(Util.baseDatetime().AddMonths(6).AddDays(15).AddHours(0)) + 1; // had to add one, looks like Util function was designed for parsing non-zero-indexed hours
                        CalculateStats(dhrs, commonKeys, stat_hours, mask, hhh, true);
                        SetOutputData(DA, stat_hours);
                        break;
                    default:
                        this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Time period option not yet implimented.  Cannot produce statistics.");
                        break;
                }


            }
        }

        private static void InitStatHours(ref Dictionary<string, List<DHr>> stat_hours) {
            stat_hours.Clear();
            stat_hours.Add("meanHrs", new List<DHr>());
            stat_hours.Add("modeHrs", new List<DHr>());
            stat_hours.Add("highHrs", new List<DHr>());
            stat_hours.Add("uqHrs", new List<DHr>());
            stat_hours.Add("medianHrs", new List<DHr>());
            stat_hours.Add("lqHrs", new List<DHr>());
            stat_hours.Add("lowHrs", new List<DHr>());
            stat_hours.Add("sumHrs", new List<DHr>());
        }



        private static void SetOutputData(IGH_DataAccess DA, Dictionary<string, List<DHr>> stat_hours) {
            DA.SetDataList(0, stat_hours["meanHrs"]);
            DA.SetDataList(1, stat_hours["modeHrs"]);
            DA.SetDataList(2, stat_hours["highHrs"]);
            DA.SetDataList(3, stat_hours["uqHrs"]);
            DA.SetDataList(4, stat_hours["medianHrs"]);
            DA.SetDataList(5, stat_hours["lqHrs"]);
            DA.SetDataList(6, stat_hours["lowHrs"]);
            DA.SetDataList(7, stat_hours["sumHrs"]);
        }

        private void CalculateStats(List<DHr> dhrs, string[] keys, Dictionary<string, List<DHr>> stat_hours_dict, HourMask mask, int assigned_hour_of_year, bool calculate_mode = false) {
            Dictionary<string, List<float>> value_dict = new Dictionary<string, List<float>>();
            foreach (string key in keys) { value_dict.Add(key, new List<float>()); }
            int count = 0;
            List<int> a = new List<int>();
            List<int> r = new List<int>();
            List<int> g = new List<int>();
            List<int> b = new List<int>();
            foreach (DHr hour in dhrs) {
                if (mask.eval(hour)) {
                    count++;
                    a.Add(hour.color.A);
                    r.Add(hour.color.R);
                    g.Add(hour.color.G);
                    b.Add(hour.color.B);
                    foreach (string key in keys) { value_dict[key].Add(hour.val(key)); }
                }
            }
            Color c = Color.FromArgb((int)a.Average(), (int)r.Average(), (int)g.Average(), (int)b.Average());

            DHr meanHr = new DHr(assigned_hour_of_year); meanHr.is_surrogate = true; meanHr.color = c;
            DHr modeHr = new DHr(assigned_hour_of_year); modeHr.is_surrogate = true; modeHr.color = c;
            DHr highHr = new DHr(assigned_hour_of_year); highHr.is_surrogate = true; highHr.color = c;
            DHr uqHr = new DHr(assigned_hour_of_year); uqHr.is_surrogate = true; uqHr.color = c;
            DHr medianHr = new DHr(assigned_hour_of_year); medianHr.is_surrogate = true; medianHr.color = c;
            DHr lqHr = new DHr(assigned_hour_of_year); lqHr.is_surrogate = true; lqHr.color = c;
            DHr lowHr = new DHr(assigned_hour_of_year); lowHr.is_surrogate = true; lowHr.color = c;
            DHr sumHr = new DHr(assigned_hour_of_year); sumHr.is_surrogate = true; sumHr.color = c;

            if (calculate_mode) {
                foreach (string key in keys) {
                    value_dict[key].Sort();
                    meanHr.put(key, value_dict[key].Mean());
                    highHr.put(key, value_dict[key][value_dict[key].Count - 1]);
                    uqHr.put(key, value_dict[key].Quartile(0.75f));
                    medianHr.put(key, value_dict[key].Median());
                    lqHr.put(key, value_dict[key].Quartile(0.25f));
                    lowHr.put(key, value_dict[key][0]);
                    sumHr.put(key, value_dict[key].Sum());

                    List<float> modes = value_dict[key].Modes().ToList<float>();
                    //if (modes.Count > 1) this.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, String.Format("Multiple values associated with the key '{0}' occur equally often, resulting in multiple Mode values.  I've returned the first mode encountered", key));
                    if (modes.Count >= 1) modeHr.put(key, modes[0]);
                    else {
                        //this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, String.Format("Each value associated with the key '{0}' is unique.  Unable to calculate Mode", key));
                        modeHr.put(key, MDHr.INVALID_VAL);
                    }
                }
            }
            stat_hours_dict["meanHrs"].Add(meanHr);
            if (calculate_mode) stat_hours_dict["modeHrs"].Add(modeHr);
            stat_hours_dict["highHrs"].Add(highHr);
            stat_hours_dict["uqHrs"].Add(uqHr);
            stat_hours_dict["medianHrs"].Add(medianHr);
            stat_hours_dict["lqHrs"].Add(lqHr);
            stat_hours_dict["lowHrs"].Add(lowHr);
            stat_hours_dict["sumHrs"].Add(sumHr);
        }


    }

    public class Dhr_RunningAverageComponent : GH_Component {
        public Dhr_RunningAverageComponent()
            //Call the base constructor
            : base("Rolling Mean", "RollMean", "Computes the rolling mean for each key in each Dhour in a collection of Dhours.\nReturns the modified set of Dhours", "DYear", "Filter") { }
        public override Grasshopper.Kernel.GH_Exposure Exposure { get { return GH_Exposure.secondary; } }
        public override Guid ComponentGuid { get { return new Guid("{1B7EAAA1-BB1F-4CBD-9538-D2B004D306D9}"); } }
        protected override Bitmap Icon { get { return DYear.Properties.Resources.Olgay; } }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager) {
            pManager.RegisterParam(new GHParam_DHr(), "DHours", "Dhrs", "The Dhours from which to extract values", GH_ParamAccess.list);
            pManager.Register_IntegerParam("Scope", "S", "The scope of the rolling mean - the number of nearby hours to average, both before and after the given hour", 24, GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager) {

            pManager.RegisterParam(new GHParam_DHr(), "DHours", "Dhrs", "The averaged Dhours", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA) {
            List<DHr> dhrs = new List<DHr>();
            int scope = -1;
            if ((DA.GetDataList(0, dhrs)) && (DA.GetData(1, ref scope))) {
                string[] commonKeys = DHr.commonkeys(dhrs.ToArray());

                List<DHr> hrsOut = new List<DHr>();
                for (int n = 0; n < dhrs.Count; n++) {
                    DHr dhr = new DHr(dhrs[n]);
                    foreach (string key in commonKeys) {
                        float sum = 0;
                        for (int di = -scope / 2; di <= scope / 2; di++) {
                            int m = n + di;
                            while ((m < 0) || (m > dhrs.Count - 1)) {
                                if (m < 0) m = dhrs.Count + m;
                                if (m > dhrs.Count - 1) m = m - dhrs.Count;
                            }
                            sum += dhrs[m].val(key);
                        }
                        sum = sum / ((float)scope + 1);
                        dhr.put(key, sum);
                    }
                    hrsOut.Add(dhr);
                }

                DA.SetDataList(0, hrsOut);
            }
        }

    }


    public class Dhr_HourFreqComponent : GH_Component {
        public Dhr_HourFreqComponent()
            //Call the base constructor
            : base("Hour Frequency", "HourFreq", "", "DYear", "Filter") { }
        public override Grasshopper.Kernel.GH_Exposure Exposure { get { return GH_Exposure.secondary; } }
        public override Guid ComponentGuid { get { return new Guid("{D468F5AB-2272-483A-947D-93257818873F}"); } }
        protected override Bitmap Icon { get { return DYear.Properties.Resources.Olgay; } }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager) {
            pManager.RegisterParam(new GHParam_DHr(), "DHours", "Dhrs", "The Dhours from which to extract values", GH_ParamAccess.tree);
            pManager.Register_StringParam("Key", "Key", "The key to count hours on", GH_ParamAccess.item);
            pManager.Register_IntervalParam("Interval", "Ival", "The overall interval to sample.  This interval will be subdivided into a number of subintervals.  Defaults to min and max of given values.  Any values that fall outside of this iterval will be appended to the highest or lowest count.", GH_ParamAccess.item);
            pManager.Register_IntegerParam("Subdivisions", "Div", "The number of subintervals to divide the above interval into", 10, GH_ParamAccess.item);

            this.Params.Input[2].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager) {
            pManager.Register_IntegerParam("Frequencies", "Freqs", "A list of frequencies describing the number of times an hour falls within a corresponding interval, returned below", GH_ParamAccess.tree);
            pManager.Register_IntervalParam("Subintervals", "Ivals", "A list of intervals, produced by subdividing the given interval, that describe ranges of values used to calculate the above frequencies", GH_ParamAccess.tree);
            pManager.RegisterParam(new GHParam_DHr(), "DHours", "Dhrs", "Hours that fall into the above subintervals", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA) {
            Grasshopper.Kernel.Data.GH_Structure<DHr> hourTreeIn = new Grasshopper.Kernel.Data.GH_Structure<DHr>();
            string key = "";
            int subdivs = 0;
            if ((DA.GetDataTree(0, out hourTreeIn)) && (DA.GetData(1, ref key)) && (DA.GetData(3, ref subdivs)))
            {
                Interval ival_overall = new Interval();
                if (!DA.GetData(2, ref ival_overall)) {
                    // go thru the given hours and find the max and min value for the given key
                    ival_overall.T0 = MDHr.INVALID_VAL;
                    ival_overall.T1 = MDHr.INVALID_VAL;
                    foreach (DHr dhr in hourTreeIn.AllData(true)) {
                        float val = dhr.val(key);
                        if ((ival_overall.T0 == MDHr.INVALID_VAL) || (val < ival_overall.T0)) ival_overall.T0 = val;
                        if ((ival_overall.T1 == MDHr.INVALID_VAL) || (val > ival_overall.T1)) ival_overall.T1 = val;
                    }
                }

                Grasshopper.Kernel.Data.GH_Structure<DHr> hourTreeOut = new Grasshopper.Kernel.Data.GH_Structure<DHr>();
                Grasshopper.Kernel.Data.GH_Structure<Grasshopper.Kernel.Types.GH_Integer> freqTreeOut = new Grasshopper.Kernel.Data.GH_Structure<Grasshopper.Kernel.Types.GH_Integer>();
                Grasshopper.Kernel.Data.GH_Structure<Grasshopper.Kernel.Types.GH_Interval> ivalTreeOut = new Grasshopper.Kernel.Data.GH_Structure<Grasshopper.Kernel.Types.GH_Interval>();
                
                List<Interval> ivalList = new List<Interval>();
                if (ival_overall.IsDecreasing) ival_overall.Swap();
                double delta = ival_overall.Length / subdivs;
                for (int n = 0; n < subdivs; n++) {  ivalList.Add(new Interval(ival_overall.T0 + n * delta, ival_overall.T0 + ((n + 1) * delta)));  }
                
                for (int b = 0; b < hourTreeIn.Branches.Count; b++)
                {
                    Grasshopper.Kernel.Data.GH_Structure<DHr> hourBranch = new Grasshopper.Kernel.Data.GH_Structure<DHr>();
                    for (int n = 0; n < subdivs; n++) { hourBranch.EnsurePath(n); }
                    List<int> freqOut = new List<int>();

                    List<DHr> hrsIn = hourTreeIn.Branches[b];
                    foreach (DHr dhr in hrsIn)
                    {
                        if (dhr.val(key) < ivalList[0].T0)
                        {
                            hourBranch.Append(dhr, new Grasshopper.Kernel.Data.GH_Path(0));
                            continue;
                        }
                        if (dhr.val(key) > ivalList[ivalList.Count - 1].T1)
                        {
                            hourBranch.Append(dhr, new Grasshopper.Kernel.Data.GH_Path(ivalList.Count - 1));
                            continue;
                        }
                        for (int n = 0; n < subdivs; n++)
                        {
                            if (ivalList[n].IncludesParameter(dhr.val(key)))
                            {
                                hourBranch.Append(dhr,new Grasshopper.Kernel.Data.GH_Path(n));
                                break;
                            }
                        }
                    }

                    
                    for (int bb = 0; bb < hourBranch.Branches.Count; bb++)
                    {
                        Grasshopper.Kernel.Data.GH_Path branch_path = hourTreeIn.Paths[b].AppendElement(bb);
                        hourTreeOut.AppendRange(hourBranch.Branches[bb], branch_path);
                        Grasshopper.Kernel.Types.GH_Integer freq = new Grasshopper.Kernel.Types.GH_Integer(hourBranch.Branches[bb].Count);
                        freqTreeOut.Append(freq, branch_path);
                        Grasshopper.Kernel.Types.GH_Interval ival = new Grasshopper.Kernel.Types.GH_Interval(ivalList[bb]);
                        ivalTreeOut.Append(ival, branch_path);
                    }
                    
                }

                hourTreeOut.Simplify(Grasshopper.Kernel.Data.GH_SimplificationMode.CollapseAllOverlaps);
                freqTreeOut.Simplify(Grasshopper.Kernel.Data.GH_SimplificationMode.CollapseAllOverlaps);
                ivalTreeOut.Simplify(Grasshopper.Kernel.Data.GH_SimplificationMode.CollapseAllOverlaps);
                
                DA.SetDataTree(0, freqTreeOut);
                DA.SetDataTree(1, ivalTreeOut);
                DA.SetDataTree(2, hourTreeOut);
            }
        }

    }


    public class Dhr_HourFreq2Component : GH_Component {
        public Dhr_HourFreq2Component()
            //Call the base constructor
            : base("Hour Frequency Two", "HourFreq2", "", "DYear", "Filter") { }
        public override Grasshopper.Kernel.GH_Exposure Exposure { get { return GH_Exposure.secondary; } }
        public override Guid ComponentGuid { get { return new Guid("{58ED367D-9315-4133-BBE9-FCCD452075CC}"); } }
        protected override Bitmap Icon { get { return DYear.Properties.Resources.Olgay; } }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager) {
            pManager.RegisterParam(new GHParam_DHr(), "DHours", "Dhrs", "The Dhours from which to extract values", GH_ParamAccess.list);
            pManager.Register_StringParam("Key U", "Key U", "The first key to count hours on", GH_ParamAccess.item);
            pManager.Register_StringParam("Key U", "Key V", "The second key to count hours on", GH_ParamAccess.item);
            pManager.Register_2DIntervalParam("UV Interval", "Ival2", "The overall two-dimensional interval to sample.  This interval will be subdivided into a number of subintervals.", GH_ParamAccess.item);
            pManager.Register_IntervalParam("Subdivisions", "Divs", "An interval of two integer numbers that describe the number of subdivisions desired in the U and V dimensions", new Interval(4, 3), GH_ParamAccess.item);
            pManager.Register_BooleanParam("Cull Outliers", "Cul", "If true, outlying values will be culled.  If false (default), any values that fall outside of this iterval will be appended to the highest or lowest count.", false, GH_ParamAccess.item);

            this.Params.Input[3].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager) {
            pManager.Register_IntegerParam("Frequencies", "Freqs", "A tree of frequencies describing the number of times an hour falls within a corresponding interval, returned below", GH_ParamAccess.tree);
            pManager.Register_2DIntervalParam("Subintervals", "Ivals", "A tree of intervals, produced by subdividing the given interval, that describe ranges of values used to calculate the above frequencies", GH_ParamAccess.tree);
            pManager.RegisterParam(new GHParam_DHr(), "DHours", "Dhrs", "Hours that fall into the above subintervals", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA) {
            List<DHr> dhrs = new List<DHr>();
            string key_u = "";
            string key_v = "";
            Interval subdivs = new Interval();
            if ((DA.GetDataList(0, dhrs)) && (DA.GetData(1, ref key_u)) && (DA.GetData(2, ref key_v)) && (DA.GetData(4, ref subdivs)))
            {
                int subdivs_u = (int) Math.Floor(subdivs.T0);
                int subdivs_v = (int) Math.Floor(subdivs.T1);
                
                Grasshopper.Kernel.Types.UVInterval ival_overall = new Grasshopper.Kernel.Types.UVInterval();
                if (!DA.GetData(3, ref ival_overall)) {
                    // go thru the given hours and find the max and min value for the given key
                    Interval ival_temp_u = new Interval(MDHr.INVALID_VAL, MDHr.INVALID_VAL);
                    Interval ival_temp_v = new Interval(MDHr.INVALID_VAL, MDHr.INVALID_VAL);
                    foreach (DHr dhr in dhrs) {
                        float val_u = dhr.val(key_u);
                        float val_v = dhr.val(key_v);
                        if ((ival_temp_u.T0 == MDHr.INVALID_VAL) || (val_u < ival_temp_u.T0)) ival_temp_u.T0 = val_u;
                        if ((ival_temp_u.T1 == MDHr.INVALID_VAL) || (val_u > ival_temp_u.T1)) ival_temp_u.T1 = val_u;
                        if ((ival_temp_v.T0 == MDHr.INVALID_VAL) || (val_v < ival_temp_v.T0)) ival_temp_v.T0 = val_v;
                        if ((ival_temp_v.T1 == MDHr.INVALID_VAL) || (val_v > ival_temp_v.T1)) ival_temp_v.T1 = val_v;
                    }
                }

                bool cull_outliers = false;
                DA.GetData(5, ref cull_outliers);


                Grasshopper.Kernel.Data.GH_Structure<DHr> hrsOut = new Grasshopper.Kernel.Data.GH_Structure<DHr>();
                Grasshopper.Kernel.Data.GH_Structure<Grasshopper.Kernel.Types.GH_Integer> freqOut = new Grasshopper.Kernel.Data.GH_Structure<Grasshopper.Kernel.Types.GH_Integer>();
                Grasshopper.Kernel.Data.GH_Structure<Grasshopper.Kernel.Types.GH_Interval2D> ivalsOut = new Grasshopper.Kernel.Data.GH_Structure<Grasshopper.Kernel.Types.GH_Interval2D>();

                if (ival_overall.U.IsDecreasing) ival_overall.U.Swap();
                if (ival_overall.V.IsDecreasing) ival_overall.V.Swap();
                double delta_u = ival_overall.U.Length / subdivs_u;
                double delta_v = ival_overall.V.Length / subdivs_v;
                for (int u = 0; u < subdivs_u; u++) {
                    for (int v = 0; v < subdivs_v; v++) {
                        Grasshopper.Kernel.Data.GH_Path path = new Grasshopper.Kernel.Data.GH_Path(new int[] { u, v });
                        ivalsOut.EnsurePath(path);
                        hrsOut.EnsurePath(path);

                        Interval sub_u = new Interval(ival_overall.U.T0 + u * delta_u, ival_overall.U.T0 + ((u + 1) * delta_u));
                        Interval sub_v = new Interval(ival_overall.V.T0 + v * delta_v, ival_overall.V.T0 + ((v + 1) * delta_v));
                        Grasshopper.Kernel.Types.UVInterval sub_uv = new Grasshopper.Kernel.Types.UVInterval(sub_u, sub_v);
                        Grasshopper.Kernel.Types.GH_Interval2D i2d = new Grasshopper.Kernel.Types.GH_Interval2D();
                        i2d.Value = sub_uv;
                        ivalsOut.Append(i2d, path);
                    }
                }
                
                foreach (DHr dhr in dhrs) {
                    int[] address = new int[] { -1 , -1};
                    for (int u = 0; u < subdivs_u; u++)
                    {
                        Grasshopper.Kernel.Data.GH_Path path_u = new Grasshopper.Kernel.Data.GH_Path(new int[] { u, 0 });
                        Interval sub_u = ivalsOut.get_DataItem(path_u, 0).Value.U;
                        double val = dhr.val(key_u);
                        if ((sub_u.IncludesParameter(val)) || ((!cull_outliers) && (u == 0) && (val <= sub_u.Min)) || ((!cull_outliers) && (u == subdivs_u - 1) && (val >= sub_u.Max)))
                        {
                            address[0] = u;
                            break;
                        }
                    }

                    for (int v = 0; v < subdivs_v; v++)
                    {
                        Grasshopper.Kernel.Data.GH_Path path_v = new Grasshopper.Kernel.Data.GH_Path(new int[] { 0, v });
                        Interval sub_v = ivalsOut.get_DataItem(path_v, 0).Value.V;
                        double val = dhr.val(key_v);
                        if ((sub_v.IncludesParameter(val)) || ((!cull_outliers) && (v == 0) && (val <= sub_v.Min)) || ((!cull_outliers) && (v == subdivs_v - 1) && (val >= sub_v.Max)))
                        {
                            address[1] = v;
                            break;
                        }
                    }
                    if ((address[0] < 0) || (address[1] < 0))
                    {
                        if (!cull_outliers) this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Crud. Could not place an outlier into any intervals.  What gives?!");
                    }
                    else
                    {
                        Grasshopper.Kernel.Data.GH_Path path = new Grasshopper.Kernel.Data.GH_Path(address);
                        hrsOut.Append(dhr, path);
                    }
                }


                foreach (Grasshopper.Kernel.Data.GH_Path path in hrsOut.Paths)
                {
                    int n = hrsOut.get_Branch(path).Count;
                    freqOut.Append(new Grasshopper.Kernel.Types.GH_Integer(n), path);
                }

                
                DA.SetDataTree(0, freqOut);
                DA.SetDataTree(1, ivalsOut);
                DA.SetDataTree(2, hrsOut);
            }
        }

    }



    public class Dhr_ExtremePeriodsComponent : GH_Component {
        public Dhr_ExtremePeriodsComponent()
            //Call the base constructor
            : base("Extreme Periods", "Extremes", "Returns the Dhours containing the min and max of a given key from a collection of Dhours. If multiple instances of the value are encountered, the first occurrence is returned.", "DYear", "Filter") { }
        public override Grasshopper.Kernel.GH_Exposure Exposure { get { return GH_Exposure.secondary; } }
        public override Guid ComponentGuid { get { return new Guid("{0C50E2C4-8719-42C0-B3F9-283A8A8F80E1}"); } }
        protected override Bitmap Icon { get { return DYear.Properties.Resources.Olgay; } }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager) {
            pManager.RegisterParam(new GHParam_DHr(), "DHours", "Dhrs", "The Dhours to search for extremes within", GH_ParamAccess.list);
            pManager.Register_StringParam("Value Key", "Key", "The name of the value to test", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager) {
            pManager.RegisterParam(new GHParam_DHr(), "Maximum Day", "MaxDay", "a 24 hour period that includes the maximum value of the given key", GH_ParamAccess.list);
            pManager.RegisterParam(new GHParam_DHr(), "Minimum Day", "MinDay", "a 24 hour period that includes the minimum value of the given key", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA) {
            List<DHr> dhrs = new List<DHr>();
            string key = "";
            if ((DA.GetDataList(0, dhrs)) && (DA.GetData(1, ref key))) {
                float maxval = dhrs[0].val(key);
                float minval = dhrs[0].val(key);
                int max_day = 0;
                int min_day = 0;
                for (int n = 1; n < dhrs.Count; n++) {
                    float val = dhrs[n].val(key);
                    if (val > maxval) {
                        maxval = val;
                        max_day = dhrs[n].day_of_year;
                    }
                    if (val < minval) {
                        minval = val;
                        min_day = dhrs[n].day_of_year;
                    }
                }
                HourMask max_mask = new HourMask();
                max_mask.maskByDayOfYear(max_day, max_day);
                HourMask min_mask = new HourMask();
                min_mask.maskByDayOfYear(min_day, min_day);

                List<DHr> maxHrs = new List<DHr>();
                foreach (DHr hr in dhrs) if (max_mask.eval(hr)) maxHrs.Add(hr);
                List<DHr> minHrs = new List<DHr>();
                foreach (DHr hr in dhrs) if (min_mask.eval(hr)) minHrs.Add(hr);


                DA.SetDataList(0, maxHrs);
                DA.SetDataList(1, minHrs);
            }
        }

    }

    public class Dhr_MaskHoursComponent : GH_Component {
        public Dhr_MaskHoursComponent()
            //Call the base constructor
            : base("Mask Hours", "MaskHours", "Filters a given set of Dhours through an Hourmask.\nOnly those hours allowed by the mask are returned", "DYear", "Filter") { }
        public override Grasshopper.Kernel.GH_Exposure Exposure { get { return GH_Exposure.tertiary; } }
        public override Guid ComponentGuid { get { return new Guid("{7171C527-5E98-427D-9B91-200DA77F9F8D}"); } }
        protected override Bitmap Icon { get { return DYear.Properties.Resources.Olgay; } }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager) {
            pManager.RegisterParam(new GHParam_DHr(), "DHours", "Dhrs", "The Dhours from which to extract values", GH_ParamAccess.list);
            pManager.RegisterParam(new GHParam_HourMask(), "Hourmask", "HMask", "The Hourmask that does the filtering", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager) {

            pManager.RegisterParam(new GHParam_DHr(), "DHours", "Dhrs", "The masked Dhours", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA) {
            List<DHr> hrsIn = new List<DHr>();
            HourMask mask = new HourMask();
            mask.fillMask(true);
            DA.GetData(1, ref mask);
            if (DA.GetDataList(0, hrsIn)) {
                List<DHr> hrsOut = new List<DHr>();
                foreach (DHr hour in hrsIn) if (mask.eval(hour)) hrsOut.Add(hour);
                DA.SetDataList(0, hrsOut);
            }
        }

    }

    public class Dhr_SortHoursComponent : GH_Component {
        public Dhr_SortHoursComponent()
            //Call the base constructor
            : base("Sort Hours", "SortHours", "Sorts a given list of Dhours by the values found in a given key", "DYear", "Filter") { }
        public override Grasshopper.Kernel.GH_Exposure Exposure { get { return GH_Exposure.tertiary; } }
        public override Guid ComponentGuid { get { return new Guid("{76D71582-FBA0-4376-B6D4-4FF6A4326D1E}"); } }
        protected override Bitmap Icon { get { return DYear.Properties.Resources.Olgay; } }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager) {
            pManager.RegisterParam(new GHParam_DHr(), "DHours", "Dhrs", "The Dhours from which to extract values", GH_ParamAccess.list);
            pManager.Register_StringParam("Key", "Key", "The key to sort on", GH_ParamAccess.item);
            pManager.Register_BooleanParam("Ascending/Decending", "Asc", "Set to false to sort decending.", true);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager) {

            pManager.RegisterParam(new GHParam_DHr(), "DHours", "Dhrs", "The masked Dhours", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA) {
            List<DHr> dhrs = new List<DHr>();
            string key = "";
            bool asc = true;
            if ((DA.GetDataList(0, dhrs)) && (DA.GetData(1, ref key)) && (DA.GetData(2, ref asc))) {
                //dhrs.Sort((x, y) => );
                List<DHr> hrsOut;
                if (asc) hrsOut = dhrs.OrderBy(s => s.val(key)).ToList();
                else hrsOut = dhrs.OrderByDescending(s => s.val(key)).ToList();
                DA.SetDataList(0, hrsOut);
            }
        }

    }

    public class Dhr_FilterConditional : GH_Component {
        public Dhr_FilterConditional()
            //Call the base constructor
            : base("Hour Conditional Filter", "CondFilter", "Filters a given set of Dhours through a conditional satement.\nOnly those hours that satisfy this condition are returned", "DYear", "Filter") { }
        public override Grasshopper.Kernel.GH_Exposure Exposure { get { return GH_Exposure.tertiary; } }
        public override Guid ComponentGuid { get { return new Guid("{202FD0E9-70A4-4970-934D-46DD39491534}"); } }
        protected override Bitmap Icon { get { return DYear.Properties.Resources.Olgay; } }

        CType comparison_type;
        public enum CType { eq, ne, gt, ge, lt, le, invalid };

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager) {
            pManager.RegisterParam(new GHParam_DHr(), "DHours", "Dhrs", "The Dhours from which to extract values", GH_ParamAccess.list);
            pManager.Register_StringParam("Key", "Key", "The key to test", GH_ParamAccess.item);
            pManager.Register_StringParam("Operator", "Opr", "The comparison operator.  Choose '==' (equal to), '!=' (not equal to),'>'(greater than),'>=' (greater than or equal to), '<' (less than), or '<=' (less than or equal to),   ", GH_ParamAccess.item);
            pManager.Register_DoubleParam("Value", "Val", "The value to test against", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager) {

            pManager.RegisterParam(new GHParam_DHr(), "DHours", "Dhrs", "The Dhours that satisfy the conditional statement.", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA) {
            List<DHr> hrsIn = new List<DHr>();
            string key = "";
            string op_string = "";
            double val = 0.0;
            this.comparison_type = CType.invalid;
            if (DA.GetDataList(0, hrsIn) && DA.GetData(1, ref key) && DA.GetData(2, ref op_string) && DA.GetData(3, ref val)) {
                
                if (op_string.Contains("!=")) { this.comparison_type = CType.ne; } 
                else if (op_string.Contains(">=")||op_string.Contains("=>")) { this.comparison_type = CType.ge; } 
                else if (op_string.Contains("<=")||op_string.Contains("=<")) { this.comparison_type = CType.le; } 
                else if (op_string.Contains("=")) { this.comparison_type = CType.eq; } 
                else if (op_string.Contains(">")) { this.comparison_type = CType.gt; } 
                else if (op_string.Contains("<")) { this.comparison_type = CType.le; } 
                else {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "I don't understand the given comparison operator.\nPlease choose '==', '!=', '>', '>=', '<', or '<='.");
                    }

                List<DHr> hrsOut = new List<DHr>();
                foreach (DHr hour in hrsIn) {
                    switch (this.comparison_type) {
                        case CType.eq: if (hour.val(key) == val) hrsOut.Add(hour);break;
                        case CType.ne: if (hour.val(key) != val) hrsOut.Add(hour);break;
                        case CType.gt: if (hour.val(key) > val) hrsOut.Add(hour); break;
                        case CType.ge: if (hour.val(key) >= val) hrsOut.Add(hour); break;
                        case CType.lt: if (hour.val(key) < val) hrsOut.Add(hour); break;
                        case CType.le: if (hour.val(key) <= val) hrsOut.Add(hour); break;
                    }         
                }
                DA.SetDataList(0, hrsOut);
            }
        }

    }



}
