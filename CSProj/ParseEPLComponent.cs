using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System.Drawing;
using System.IO;



namespace DYear
{
    public class ParseEPLComponent : GH_Component
    {
        //http://www.grasshopper3d.com/forum/topics/can-the-solveinstance-method?commentId=2985220%3AComment%3A107325
        private int header_lines = 1;
        private string filepath = "";
        private List<string> persistentOutputParams = new List<string>() { "headers" };

        /// <summary>
        /// a dictionary of dictionaries of ints that describe the column structure of the loaded EPL data file
        /// each top-level item relates a string describing a zone_name with a lower-level dictionary that relates a column name with the index of that column in our CSV
        /// </summary>
        private Dictionary<string, Dictionary<string, int> > zone_dict;

        /// <summary>
        /// a dictionary of lists of hours
        /// each top-level item relates a string describing a zone_name with 8760 hours
        /// each hour contains keys corresponding to the columns associated with a zone
        /// </summary>
        private Dictionary<string, List<DHr>> zone_hours;


        //Add Constructor
        public ParseEPLComponent() 
        
            //Call the base constructor
            : base("ParseEPlus","EPL","Parses an Energy Plus Output file","DYear","Parse")
        { }


        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            //pManager.Register_StringParam("EP Output File", "EPW", "The path to the CSV file to be parsed",GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.Register_StringParam("headers", "headers", "for testing.  you shouldn't really see this.");
            
            //GHParam_DHr param = new GHParam_DHr();
            //pManager.RegisterParam(param, "Hours", "Dhr", "The hourly EPW data served up as a list of DHr objects", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // simply passes data stored in zone_hours dict to output params

            if (zone_hours.Count > 0)
            {
                // create a list of all the column headers we've found, so we can pass these to the 'headers' output 
                List<string> zonenames = new List<string>();
                foreach (KeyValuePair<string, List<DHr>> entry in zone_hours) {zonenames.Add(entry.Key);}
                //DA.SetDataList(0, zonenames);

                // for each output param, find the proper list of zone_hours and pass it along
                for (int n = 0; n < Params.Output.Count; n++)
                {
                    string key = Params.Output[n].NickName;
                    if (key == persistentOutputParams[0]) { DA.SetDataList(n, zonenames); }
                    else
                    {
                        if (zone_hours.ContainsKey(key)) { DA.SetDataList(n, zone_hours[key]); }
                        else { AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Selected file does not contain column: " + key); }
                    }
                }
                
            }
        }

        private bool DictParseFilepath()
        {
            zone_dict = new Dictionary<string, Dictionary<string, int>>();
 
            // get a list of the column headers
            List<string> headers;
            using (StreamReader headreader = new StreamReader(filepath)) { headers = new List<string>(headreader.ReadLine().Split(',')); }

            for (int n = 0; n < headers.Count; n++)
            {
                if (headers[n].Contains(":"))
                {
                    // register this column header in our zone_dict
                    string zone_name = headers[n].Split(':')[0];
                    string col_name = headers[n].Split(':')[1];

                    if (!zone_dict.ContainsKey(zone_name)) { zone_dict.Add(zone_name, new Dictionary<string, int>()); }
                    zone_dict[zone_name].Add(col_name, n);
                }
            }

            zone_hours = new Dictionary<string, List<DHr>>();
            foreach (string key in zone_dict.Keys) { zone_hours.Add(key, new List<DHr>()); } // empty list of hours for each zone

            StreamReader reader = new StreamReader(filepath);
            string line;
            int lnum = 0;
            while ((line = reader.ReadLine()) != null)
            {
                if (lnum >= header_lines)
                {
                    string[] linedata = line.Split(',');

                    foreach (KeyValuePair<string, Dictionary<string, int>> column_dict in zone_dict)
                    {
                        DHr hr = new DHr(lnum - header_lines);
                        try
                        {
                            foreach (KeyValuePair<string, int> column in column_dict.Value)
                            {
                                hr.put(column.Key, float.Parse(linedata[column.Value]));
                            }
                        }
                        catch
                        {
                            AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "parse error on line " + lnum);
                            return false;
                        }
                        zone_hours[column_dict.Key].Add(hr);
                    }
                }
                lnum++;
            }
            return true;
        }

        private void DictClear()
        {
            zone_dict = new Dictionary<string, Dictionary<string, int>>();
            zone_hours = new Dictionary<string, List<DHr>>();
        }

        private bool FilepathValid() { return File.Exists(this.filepath); }

        public override bool AppendMenuItems(ToolStripDropDown menu)
        {
            //base.AppendMenuItems(menu);
            Menu_AppendEnableItem(menu);
            Menu_AppendWarningsAndErrors(menu);
            Menu_AppendObjectHelp(menu);
            Menu_AppendSeparator(menu);

            if (FilepathValid()) { Menu_AppendItem(menu, this.filepath, Menu_FilepathClicked); }
            if (FilepathValid()) { Menu_AppendItem(menu, "Refresh Filepath", Menu_RefreshFilepathClicked); }
            Menu_AppendItem(menu, "Set Filepath", Menu_SetFilepathClicked);

            return true;
        }

        public override void AddedToDocument(GH_Document document)
        {
            Menu_SetFilepathClicked(new object(), new EventArgs());
        }


        private void Menu_SetFilepathClicked(Object sender, EventArgs e)
        {
            OpenFileDialog fpDialog = new OpenFileDialog();
            //fpDialog.InitialDirectory = "c:\\";
            fpDialog.Title = "Please Select an Energy Plus Output File";
            fpDialog.Filter = "csv files (*.csv)|*.csv|All files (*.*)|*.*";
            fpDialog.FilterIndex = 1;
            fpDialog.RestoreDirectory = true;
            fpDialog.CheckFileExists = true;
            fpDialog.CheckPathExists = true;

            if ((fpDialog.ShowDialog() == DialogResult.OK) && (File.Exists(fpDialog.FileName)))
            {
                filepath = fpDialog.FileName;
                this.NickName = Path.GetFileNameWithoutExtension(filepath);
                Menu_RefreshFilepathClicked(sender, e);
            }

        }
        private void Menu_RefreshFilepathClicked(Object sender, EventArgs e)
        {
            DictClear(); // clear dictionaries

            // parse current filepath
            if (DictParseFilepath())
            {
                // if successful, create a new output parameter for each entry in zone_hours
                GHParam_DHr param = new GHParam_DHr();
                this.Params.RegisterOutputParam(param);
            }

            #region // THANK YOU DAVID RUTTEN

            //Step 2. cache all existing parameters.
            List<IGH_Param> existingParams = new List<IGH_Param>(Params.Output);

            //Step 3. create a sync object for cleanup.
            object sync = Params.EmitSyncObject();

            //Step 4. remove all parameters manually, this is naughty, normally you'd call Params.UnregisterOutputParameter()
            Params.Output.Clear();

            //Step 5. recreate all parameters.
            List<string> zonenames = new List<string>();
            zonenames.AddRange(persistentOutputParams);
            foreach (KeyValuePair<string, List<DHr>> entry in zone_hours) { zonenames.Add(entry.Key); }


            foreach (string zonename in zonenames)
            {
                IGH_Param zoneParam = null;

                //First, we need to check whether a parameter pointing at this file used to exist.
                //If that's the case, recycle it.
                foreach (IGH_Param oldParam in existingParams)
                {
                    if ((oldParam.NickName.Equals(zonename, StringComparison.OrdinalIgnoreCase)))
                    {
                        zoneParam = oldParam;
                        existingParams.Remove(oldParam);
                        break;
                    }
                }

                if (zoneParam == null)
                {
                    //It would seem there was no parameter for this file, create a new one.
                    zoneParam = new GHParam_DHr();
                    if (zonename == this.persistentOutputParams[0]) { zoneParam = new Grasshopper.Kernel.Parameters.Param_String(); }
                    zoneParam.Name = zonename;
                    zoneParam.NickName = zonename;
                }

                Params.RegisterOutputParam(zoneParam);
            }

            //Step 6. use the sync object to perform cleanup on all lost parameters.
            Params.Sync(sync);

            //Step 7. make sure everyone knows we've just been naughty.
            Params.OnParametersChanged();

            //Step 8. invoke the cleanup code.
            VariableParameterMaintenance();

            //Step 9. invoke a new solution.
            ExpireSolution(true);

            #endregion


            this.Params.OnParametersChanged();
            this.OnAttributesChanged();
            ExpireSolution(true);
        }
        private void Menu_FilepathClicked(Object sender, EventArgs e) { System.Diagnostics.Process.Start("explorer.exe", @"/select, " + this.filepath); }
        
        public override Guid ComponentGuid { get { return new Guid("{ECA812CB-6007-41B5-90EA-A3E19F5CD9AF}"); } }

        protected override Bitmap Icon { get { return DYear.Properties.Resources.Component; } }

        public void VariableParameterMaintenance()
        {
            //We'll add some logic here where we'll make all output parameter nicknamed unmutable.
            foreach (IGH_Param param in Params.Output)
            {
                param.MutableNickName = false;
            }
        }

    }
}
