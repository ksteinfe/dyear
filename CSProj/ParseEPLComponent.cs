﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
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
            pManager.Register_StringParam("EP Output File", "EPW", "The path to the CSV file to be parsed",GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {

            GHParam_DHr param = new GHParam_DHr();
            pManager.RegisterParam(param,"Hours","Dhr","The hourly EPW data served up as a list of DHr objects",GH_ParamAccess.list);
            pManager.Register_StringParam("headers", "headers", "for testing");
            
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string filepath = "";

            if (DA.GetData(0, ref filepath)) //if it works...
            {
                DictClear();
                DictParseFilepath(filepath);


                List<string> zonenames = new List<string>();
                foreach (KeyValuePair<string, List<DHr>> entry in zone_hours)
                {
                    zonenames.Add(entry.Key);
                }

                DA.SetDataList(1, zonenames);

            }
        }

        private void DictParseFilepath(string filepath)
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

                            hr.put("testing", float.Parse(linedata[1]));
                        }
                        catch
                        {
                            AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "parse error on line " + lnum);
                        }
                        zone_hours[column_dict.Key].Add(hr);
                    }
                }
                lnum++;
            }
        }

        private void DictClear()
        {
            zone_hours = new Dictionary<string, List<DHr>>();
        }


        public override void AppendAdditionalMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalMenuItems(menu);
            Menu_AppendSeparator(menu);
            Menu_AppendItem(menu, "Set Filepath", Menu_SetFilepathClicked);
        }

        private void Menu_SetFilepathClicked(Object sender, EventArgs e)
        {
            GHParam_DHr param = new GHParam_DHr();
            this.Params.RegisterOutputParam(param);
            this.Params.OnParametersChanged();
            this.OnAttributesChanged();

            // none of these worked to redraw this component
            //this.ClearData();
            //this.ExpirePreview(true);
            //this.Attributes.ExpireLayout();

            Rhino.RhinoApp.WriteLine("CLICKED!");
        }
        
        
        public override Guid ComponentGuid { get { return new Guid("{ECA812CB-6007-41B5-90EA-A3E19F5CD9AF}"); } }

        protected override Bitmap Icon { get { return DYear.Properties.Resources.Component; } }

    }
}