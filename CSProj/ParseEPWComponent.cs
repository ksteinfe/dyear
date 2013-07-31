using System;
using System.Collections.Generic;
using System.Text;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System.Drawing;
using System.IO;

namespace DYear
{
    public class ParseEPWComponent : GH_Component
    {
        private int header_lines = 8;

        //Add Constructor
        public ParseEPWComponent() 
        
            //Call the base constructor
            : base("ParseEPW","EPW","Parses an EPW file","Dhour","Aquire")
        { }
        public override Grasshopper.Kernel.GH_Exposure Exposure { get { return GH_Exposure.primary; } }
        public override Guid ComponentGuid { get { return new Guid("{73D41AD8-7772-4822-9B06-DA56EDAE090C}"); } }
        protected override Bitmap Icon { get { return DYear.Properties.Resources.Icons_aquire_parseEPW; } }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            //pManager.Register_LineParam("Line", "L", "The starting line");
            //Can become optional
            //pManager[0].Optional = true;

            pManager.Register_StringParam("EPW File", "EPW", "The path to the EPW file to be parsed",GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.Register_StringParam("Name", "Loc", "The location name of this weather station");
            pManager.Register_IntegerParam("Number", "Wmo", "The World Meteorological Organization designation number of this weather station");
            pManager.Register_DoubleParam("Timezone", "Tmz", "The time zone of this weather station relative to GMT");
            pManager.Register_VectorParam("Coords", "Crd", "The latitude, longitude, and elevation of the weather station.\n Coordinates expressed as a 3d Vector, with latitude and longitude in degrees and elevation in meters");

            GHParam_DHr param = new GHParam_DHr();
            pManager.RegisterParam(param,"Hours","Dhr","The hourly EPW data served up as a list of DHr objects",GH_ParamAccess.list);
            
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string filestring = "";
            if (DA.GetData(0, ref filestring)) //if it works...
            {
                string sname = "unnamed";
                int swmo = -1;
                double stmz = 999.99;
                Vector3d coords = new Vector3d();
                
                List<DHr> shours = new List<DHr>();


                StreamReader reader = new StreamReader(filestring);
                string line;
                int lnum = 0;
                while ((line = reader.ReadLine()) != null) {
                    if (lnum >= header_lines)
                    {
                        DHr hr = new DHr(lnum - header_lines);
                        string[] cols = line.Split(',');
                        try
                        {
                            hr.put("et_rad_horz", float.Parse(cols[10]));
                            hr.put("et_rad_norm", float.Parse(cols[11]));
                            hr.put("global_horz_irrad", float.Parse(cols[13]));
                            hr.put("direct_norm_irrad", float.Parse(cols[14]));
                            hr.put("diffuse_horz_irrad", float.Parse(cols[15]));
                            hr.put("global_horz_illum", float.Parse(cols[16]));
                            hr.put("direct_norm_illum", float.Parse(cols[17]));
                            hr.put("diffuse_horz_illum", float.Parse(cols[18]));
                            hr.put("zenith_luminance", float.Parse(cols[19]));
                            hr.put("total_sky_cvr", float.Parse(cols[22]));
                            hr.put("opaque_sky_cvr", float.Parse(cols[23]));
                            hr.put("dry_bulb_temp", float.Parse(cols[6]));
                            hr.put("dew_point_temp", float.Parse(cols[7]));
                            hr.put("relative_humidity", float.Parse(cols[8]));
                            hr.put("pressure", float.Parse(cols[9]));
                            hr.put("wind_dir", float.Parse(cols[20]));
                            hr.put("wind_speed", float.Parse(cols[21]));
                            hr.put("horz_visibility", float.Parse(cols[24]));
                            hr.put("ceiling_height", float.Parse(cols[25]));
                            hr.put("precipt_water", float.Parse(cols[28]));
                            hr.put("aerosol_depth", float.Parse(cols[29]));
                            hr.put("snow_depth", float.Parse(cols[30]));
                            hr.put("days_since_snow", float.Parse(cols[31]));

                            /*
                            hr.put("EtRadHorz", float.Parse(cols[10]));
                            hr.put("EtRadNorm", float.Parse(cols[11]));
                            hr.put("GblHorzIrad", float.Parse(cols[13]));
                            hr.put("DirNormIrad", float.Parse(cols[14]));
                            hr.put("DifHorzIrad", float.Parse(cols[15]));
                            hr.put("GblHorzIllum", float.Parse(cols[16]));
                            hr.put("DirNormIllum", float.Parse(cols[17]));
                            hr.put("DifHorzIllum", float.Parse(cols[18]));
                            hr.put("ZenLum", float.Parse(cols[19]));
                            hr.put("TotSkyCvr", float.Parse(cols[22]));
                            hr.put("OpqSkyCvr", float.Parse(cols[23]));
                            hr.put("DryBulbTemp", float.Parse(cols[6]));
                            hr.put("DewPtTemp", float.Parse(cols[7]));
                            hr.put("RelHumid", float.Parse(cols[8]));
                            hr.put("Pressure", float.Parse(cols[9]));
                            hr.put("WindDir", float.Parse(cols[20]));
                            hr.put("WindSpd", float.Parse(cols[21]));
                            hr.put("HorzVis", float.Parse(cols[24]));
                            hr.put("CeilHght", float.Parse(cols[25]));
                            hr.put("PreciptWater", float.Parse(cols[28]));
                            hr.put("AeroDepth", float.Parse(cols[29]));
                            hr.put("SnowDepth", float.Parse(cols[30]));
                            hr.put("DaysSinceSnow", float.Parse(cols[31]));
                             */
                        }
                        catch
                        {
                            AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "parse error on line "+lnum);
                        }

                        shours.Add(hr);
                    }
                    else
                    {
                        // here we parse header stuff
                        if (lnum == 0)
                        {
                            string[] cols = line.Split(',');
                            sname = cols[1];
                            try
                            {
                                swmo = int.Parse(cols[5]);
                                stmz = double.Parse(cols[8]);
                                coords = new Vector3d(float.Parse(cols[6]), float.Parse(cols[7]), float.Parse(cols[9]));
                            }
                            catch
                            {
                                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "parse error on line " + lnum);
                            }
                        }
                    }
                    lnum++;
                }

                // for each hour, calculate solar positon and append to dictionary
                foreach (DHr hr in shours)
                {
                    //TODO: check that i added timezone properly
                    DateTime dt = hr.dt.AddHours(-stmz);
                    Vector2d sunpos = SunPosition.CalculateSunPosition(dt,coords.X,coords.Y);
                    hr.put("solar_altitude", (float)(sunpos.X));
                    hr.put("solar_azimuth", (float)(sunpos.Y));
                    if (sunpos.X >= 0) { hr.put("sun_is_up", 1); } else { hr.put("sun_is_up", 0); }
                }

                // fore ach hour, calculate absolute humidty and append to dictionary
                foreach (DHr hr in shours) { hr.put("absolute_humidity", Util.dbrh_to_ah(hr.val("dry_bulb_temp"), hr.val("relative_humidity"))); }

                DA.SetData(0, sname);
                DA.SetData(1, swmo);
                DA.SetData(2, stmz);
                DA.SetData(3, coords);
                DA.SetDataList(4, shours);
            }
        }



    }
}
