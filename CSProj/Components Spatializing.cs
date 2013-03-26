using System;
using System.Collections.Generic;
using System.Text;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System.Drawing;
using System.Linq;
using DYear.Statistics;

namespace DYear {

    public class Dhr_SunPosGraphComponent : GH_Component {
        public Dhr_SunPosGraphComponent()
            //Call the base constructor
            : base("Solar Position Spatialization", "SunPos", "Assigns a position on a Sunchart Graph for each hour given, based on a given solar alt and azimuth key", "DYear", "Spatialize") { }
        public override Grasshopper.Kernel.GH_Exposure Exposure { get { return GH_Exposure.primary; } }
        public override Guid ComponentGuid { get { return new Guid("{84F09813-A273-4664-AAF4-19098CF1745B}"); } }
        protected override Bitmap Icon { get { return DYear.Properties.Resources.Olgay; } }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager) {
            pManager.RegisterParam(new GHParam_DHr(), "DHours", "Dhrs", "The Dhours to which to assign positions", GH_ParamAccess.list);
            pManager.Register_2DIntervalParam("Graph Dimensions", "Dim", "The dimensions of the resulting graph", GH_ParamAccess.item);
            pManager.Register_StringParam("Solar Altitude Key", "Alt Key", "The key related to the solar altitude", "solar_altitude", GH_ParamAccess.item);
            pManager.Register_StringParam("Solar Azimuth Key", "Azm Key", "The key related to the solar azimuth", "solar_azimuth", GH_ParamAccess.item);
            pManager.Register_BooleanParam("Cull Nighttime", "Cull Night", "Cull nighttime hours?", true, GH_ParamAccess.item);
            
            this.Params.Input[1].Optional = true;
            this.Params.Input[2].Optional = true;
            this.Params.Input[3].Optional = true;
            this.Params.Input[4].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager) {
            pManager.RegisterParam(new GHParam_DHr(), "DHours", "Dhrs", "The spatialized Dhours", GH_ParamAccess.list);
            pManager.Register_PointParam("Positions", "pts", "The assigned positions of the hours", GH_ParamAccess.list);
            pManager.Register_CurveParam("Lines", "lines", "The lines", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA) {
            List<DHr> hours = new List<DHr>();
            string alt_key = "solar_altitude";
            Interval alt_ival = new Interval(0, Math.PI / 2);
            string azm_key = "solar_azimuth";
            Interval azm_ival = new Interval(0, Math.PI * 2);
            bool cull_night = true;

            if (DA.GetDataList(0, hours)) {
                Grasshopper.Kernel.Types.UVInterval ival2d = new Grasshopper.Kernel.Types.UVInterval();
                if (!DA.GetData(1, ref ival2d)){
                    ival2d.U1 = 1.0;
                    ival2d.V1 = 1.0;
                }
                DA.GetData(2, ref alt_key);
                DA.GetData(3, ref azm_key);
                DA.GetData(4, ref cull_night);


                if (cull_night) {
                    List<DHr> day_hours = new List<DHr>();
                    for (int h = 1; h < hours.Count - 1; h++) {
                        if ((hours[h].val(alt_key) >= 0) || (hours[h - 1].val(alt_key) >= 0) || (hours[h + 1].val(alt_key) >= 0))
                            day_hours.Add(hours[h]);
                    }
                    hours = day_hours;
                }


                List<Point3d> points = new List<Point3d>();

                for (int h = 0; h < hours.Count; h++) {
                    //x = Interval.remap(hr.val("solar_azimuth"),Interval(0,math.pi*2),Interval(0,width))
                    //y = Interval.remap(hr.val("solar_altitude"),Interval(0,math.pi/2),Interval(0,height))
                    float x = (float) (azm_ival.NormalizedParameterAt(hours[h].val(azm_key)) * ival2d.U1 + ival2d.U0);
                    float y = (float)(alt_ival.NormalizedParameterAt(hours[h].val(alt_key)) * ival2d.V1 + ival2d.V0);
                    Point3d pt = new Point3d(x, y, 0);
                    points.Add(pt);
                    hours[h].pos = pt;
                }

                List<Polyline> plines = new List<Polyline>();
                for (int h = 0; h < points.Count; h++) {
                    List<Point3d> pts = new List<Point3d>();
                    if ((h > 0) && (hours[h - 1].pos_x < hours[h].pos_x)) pts.Add(interp(hours[h - 1].pos, hours[h].pos, 0.5));
                    pts.Add(hours[h].pos);
                    if ((h < points.Count -1) && (hours[h + 1].pos_x > hours[h].pos_x)) pts.Add(interp(hours[h + 1].pos, hours[h].pos, 0.5));
                    plines.Add(new Polyline(pts));
                }

                DA.SetDataList(0, hours);
                DA.SetDataList(1, points);
                DA.SetDataList(2, plines);
            }
        }

        protected Point3d interp(Point3d pa, Point3d pb, double t) {
            double x = (pb.X-pa.X)*t+pa.X;
            double y = (pb.Y - pa.Y) * t + pa.Y;
            double z = (pb.Z - pa.Z) * t + pa.Z;
            return new Point3d(x, y, z);
        }
    }



    public class Dhr_HeatmapGraphComponent : GH_Component {
        public Dhr_HeatmapGraphComponent()
            //Call the base constructor
            : base("Heatmap Spatialization", "HeatMap", "Assigns a position on a Heatmap Graph for each hour given", "DYear", "Spatialize") { }
        public override Grasshopper.Kernel.GH_Exposure Exposure { get { return GH_Exposure.primary; } }
        public override Guid ComponentGuid { get { return new Guid("{AAC21149-51F7-4516-9DB5-36AE667B89A8}"); } }
        protected override Bitmap Icon { get { return DYear.Properties.Resources.Olgay; } }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager) {
            pManager.RegisterParam(new GHParam_DHr(), "DHours", "Dhrs", "The Dhours to which to assign positions", GH_ParamAccess.list);
            pManager.Register_2DIntervalParam("Graph Dimensions", "Dim", "The dimensions of the resulting graph", GH_ParamAccess.item);

            this.Params.Input[1].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager) {
            pManager.RegisterParam(new GHParam_DHr(), "DHours", "Dhrs", "The spatialized Dhours", GH_ParamAccess.list);
            pManager.Register_PointParam("Positions", "pts", "The assigned positions of the hours", GH_ParamAccess.list);
            pManager.Register_RectangleParam("Rectangles", "rects", "Rectangles plotted on the resulting heatmap, one per hour given.", GH_ParamAccess.list);
            pManager.Register_MeshParam("Mesh", "msh", "A mesh representing the resulting heatmap, with vertex colors assigned where applicable", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA) {
            List<DHr> hours = new List<DHr>();

            if (DA.GetDataList(0, hours)) {
                Grasshopper.Kernel.Types.UVInterval ival2d = new Grasshopper.Kernel.Types.UVInterval();
                if (!DA.GetData(1, ref ival2d)) {
                    ival2d.U0 = 0.0;
                    ival2d.U1 = 12.0;
                    ival2d.V0 = 1.0;
                    ival2d.V1 = 0.0;
                } else {
                    // swap v-interval so that hours start at top left of defined area
                    ival2d.V.Swap();
                }

                List<Point3d> points = new List<Point3d>();
                List<Rectangle3d> rects = new List<Rectangle3d>();
                double delta_x = Math.Abs(ival2d.U.Length) / 365.0 ;
                double delta_y = Math.Abs(ival2d.V.Length) / 24.0 ;
                double delta_x2 = delta_x/2.0 ;
                double delta_y2 = delta_y/2.0 ;

                // only produce a mesh if given a full year worth of hours
                bool doMesh = false;
                Rhino.Geometry.Mesh mesh = new Mesh();
                if (hours.Count == 8760) doMesh = true;

                for (int h = 0; h < hours.Count; h++) {
                    //x = (math.floor(hr_out.hr/24)/365.0)
                    //y = ((hr_out.hr % 24)/24.0)
                    float x = (float)((ival2d.U.ParameterAt(Math.Floor(hours[h].hr / 24.0) / 365.0))+delta_x2);
                    float y = (float)((ival2d.V.ParameterAt((hours[h].hr % 24) / 24.0)) + delta_y2);
                    Point3d pt = new Point3d(x, y, 0);
                    points.Add(pt);
                    hours[h].pos = pt;

                    if (doMesh) {
                        mesh.Vertices.Add(pt);
                        mesh.VertexColors.Add(hours[h].color);
                    }

                    Rectangle3d rect = new Rectangle3d(new Plane(new Point3d(pt.X - delta_x2, pt.Y - delta_y2, 0), new Vector3d(0, 0, 1)), delta_x, delta_y);
                    rects.Add(rect);
                }

                if (doMesh) {
                    for (int h = 0; h < 8760-24; h++) {
                        if (h % 24 != 0)  mesh.Faces.AddFace(h, h-1, h+23, h+24); 
                    }
                    mesh.Normals.ComputeNormals();
                    mesh.Compact();
                }

                DA.SetDataList(0, hours);
                DA.SetDataList(1, points);
                DA.SetDataList(2, rects);
                DA.SetData(3, mesh);

            }
        }

    }
    
}
