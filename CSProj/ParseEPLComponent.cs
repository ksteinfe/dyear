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
        private int header_lines = 1;
        private string filepath = "";
        private List<string> persistentOutputParams = new List<string>() { "keys" };

        /// <summary>
        /// a dictionary of dictionaries of ints that describe the column structure of the loaded EPL data file
        /// each top-level item relates a string describing a zone_name with a lower-level dictionary that relates a column name with the index of that column in our CSV
        /// </summary>
        private Dictionary<string, Dictionary<string, int> > col_mapping;

        /// <summary>
        /// a dictionary of lists of hours
        /// each top-level item relates a string describing a zone_name with 8760 hours
        /// each hour contains keys corresponding to the columns associated with a zone
        /// </summary>
        private Dictionary<string, List<DHr>> zone_hours;


        //Add Constructor
        public ParseEPLComponent() 
        
            //Call the base constructor
            : base("ParseEPlus", "EPL", "Parses an Energy Plus Output file", "Dhour", "Aquire")
        { }
        public override Grasshopper.Kernel.GH_Exposure Exposure { get { return GH_Exposure.primary; } }
        public override Guid ComponentGuid { get { return new Guid("{ECA812CB-6007-41B5-90EA-A3E19F5CD9AF}"); } }
        protected override Bitmap Icon { get { return DYear.Properties.Resources.Icons_aquire_parseEnergyplus; } }


        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager){ }
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager) { 
            pManager.Register_StringParam("keys", "keys", "a list of all keys found in all parsed hours.  formatted as 'zonename :: key' or sometimes 'zonename :: subzone : key"); 
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // simply passes data stored in zone_hours dict to output params
            if (zone_hours.Count > 0)
            {
                // create a list of all the column headers we've found, so we can pass these to the 'headers' output 
                List<string> zonenames = new List<string>();
                foreach (KeyValuePair<string, Dictionary<string, int>> entry in col_mapping)
                {
                    foreach (KeyValuePair<string, int> subentry in entry.Value) { zonenames.Add(entry.Key+" :: "+subentry.Key); }
                }

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

        private bool SetFilepath( string newfilepath)
        {
            // do we  really want to do this before testing that the file exists?  if we do, and the file does not exist, at least the user can see what file was meant to be loaded.
            this.filepath = newfilepath;
            this.NickName = Path.GetFileNameWithoutExtension(filepath); // set nickname to the new filepath

            if (!File.Exists(newfilepath)) {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Failed to set filepath.  Check that file exists: " + newfilepath);
                return false;
            }
            else {
                ClearParsedData(); // clear dictionaries

                // parse current filepath and store information in col_mapping and zone_hours dictionaries
                if (!ParseFilepath()) { return false; }
                

                // if successful, create a new output parameter for each entry in zone_hours
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
                return true;

                
            }
        }

        private bool ParseFilepath()
        {
            if (!FilepathValid()) { return false; }
            col_mapping = new Dictionary<string, Dictionary<string, int>>();
 
            // get a list of the column headers
            List<string> headers;
            using (StreamReader headreader = new StreamReader(new FileStream(filepath,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))) { headers = new List<string>(headreader.ReadLine().Split(',')); }

            for (int n = 0; n < headers.Count; n++)
            {
                if (headers[n].Contains(":"))
                {
                    // process zone name
                    string zone_name = headers[n].Split(':')[0].Trim();
                    zone_name = zone_name.ToLower();
                    zone_name = DHr.cleankey(zone_name);

                    // process col name
                    string col_name = headers[n].Split(':')[1];
                    int offset = col_name.IndexOf("(");
                    if (offset >= 0)
                        col_name = col_name.Substring(0, offset);
                    col_name = col_name.Trim();
                    col_name = col_name.ToLower();
                    col_name = DHr.cleankey(col_name);

                    // register this column header in our zone_dict
                    if (!col_mapping.ContainsKey(zone_name)) { col_mapping.Add(zone_name, new Dictionary<string, int>()); }
                    col_mapping[zone_name].Add(col_name, n);
                }
            }

            #region //CLEAN COLUMN HEADERS
            // eplus does some stupid stuff with the way it names column headers.  let's see if we can fix that.
            // our col_mapping dict may have some duplicate entries such as "lower_zone" and then later "lower_zone lights2"
            // remove "lower_zone lights2", and then move all the entries in into "lower_zone"

            List<string> zonenames_to_delete = new List<string>();
            foreach (KeyValuePair<string, Dictionary<string, int>> entry in col_mapping)
            {
                string this_zonename = entry.Key;
                foreach (string that_zonename in col_mapping.Keys)
                {
                    if (this_zonename.Contains(that_zonename) && (!this_zonename.Equals(that_zonename, StringComparison.OrdinalIgnoreCase)))
                    {
                        // we've found a duplicate.  copy over all the entries, with the keys prepended by this zonename
                        char[] charsToTrim = { '_', ' ', ':'};
                        string prefix = this_zonename.Replace(that_zonename, "").Trim(charsToTrim);
                        foreach (KeyValuePair<string, int> mapping in entry.Value) { col_mapping[that_zonename].Add(prefix + " : " + mapping.Key, mapping.Value); }
                        zonenames_to_delete.Add(this_zonename);
                        break;
                    }
                }
            }
            foreach (string key in zonenames_to_delete) { col_mapping.Remove(key); }
            #endregion


            zone_hours = new Dictionary<string, List<DHr>>();
            foreach (string key in col_mapping.Keys) { zone_hours.Add(key, new List<DHr>()); } // empty list of hours for each zone

            StreamReader reader = new StreamReader(new FileStream(filepath,FileMode.Open,FileAccess.Read,FileShare.ReadWrite));
            string line;
            int lnum = 0;
            bool sizing_complete = false;
            while ((line = reader.ReadLine()) != null)
            {
                if (lnum >= header_lines)
                {
                    string[] linedata = line.Split(',');


                    DateTime dt = Util.baseDatetime();
                    string[] dt_strings = linedata[0].Trim().Split(new char[] {' '},StringSplitOptions.RemoveEmptyEntries);
                    if (!DateTime.TryParse(dt_strings[0] + "/" + Util.defaultYear, out dt)) // we're adding the default year here
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "could not parse datetime on line " + lnum);
                        return false; 
                    }
                    int h = 0;
                    if (Int32.TryParse(dt_strings[1].Split(':')[0],out h)){dt = dt.AddHours(h);} else {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "could not parse datetime on line " + lnum);
                        return false; 
                    }
                    int hourOfYear = Util.hourOfYearFromDatetime(dt);
                    if ((hourOfYear > 8759) || (hourOfYear < 0)) { AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Found an odd-looking datetime on line " + lnum); }
                    if ((!sizing_complete) && (hourOfYear == 0)) { sizing_complete = true; }  // check if the two-day sizing period is done with
                    if (sizing_complete)
                    {
                        foreach (KeyValuePair<string, Dictionary<string, int>> column_dict in col_mapping)
                        {
                            DHr hr = new DHr(hourOfYear);
                            try
                            {
                                foreach (KeyValuePair<string, int> column in column_dict.Value)
                                {
                                    hr.put(column.Key, float.Parse(linedata[column.Value]));
                                }
                            }
                            catch
                            {
                                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "parse error on line " + lnum);
                                return false;
                            }
                            zone_hours[column_dict.Key].Add(hr);
                        }
                    }
                }
                lnum++;
            }
            return true;
        }

        private void ClearParsedData()
        {
            col_mapping = new Dictionary<string, Dictionary<string, int>>();
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

        //public override void AddedToDocument(GH_Document document){ Menu_SetFilepathClicked(new object(), new EventArgs());}

        public override bool Write(GH_IO.Serialization.GH_IWriter writer)
        {
            writer.SetString("filepath", this.filepath);
            return base.Write(writer);
        }
        public override bool Read(GH_IO.Serialization.GH_IReader reader)
        {
            string newfilepath = "";
            reader.TryGetString("filepath", ref newfilepath);
            SetFilepath(newfilepath); 
            return base.Read(reader);
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
                string newfilepath = fpDialog.FileName;
                SetFilepath(newfilepath);
            }

        }
        private void Menu_RefreshFilepathClicked(Object sender, EventArgs e)  {  SetFilepath(this.filepath);   }

        private void Menu_FilepathClicked(Object sender, EventArgs e) { System.Diagnostics.Process.Start("explorer.exe", @"/select, " + this.filepath); }
        
        public void VariableParameterMaintenance() { foreach (IGH_Param param in Params.Output) { param.MutableNickName = false;} }

    }
}
