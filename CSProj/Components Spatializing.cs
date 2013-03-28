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

        public PType plot_type;
        public enum PType { Ortho, Stereo, Invalid };

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager) {
            pManager.RegisterParam(new GHParam_DHr(), "DHours", "Dhrs", "The Dhours to which to assign positions", GH_ParamAccess.list);
            pManager.Register_PlaneParam("Location", "Loc", "The location and orientation (as a plane) to draw this graph", new Plane(new Point3d(0, 0, 0), new Vector3d(0, 0, 1)), GH_ParamAccess.item);
            pManager.Register_2DIntervalParam("Graph Dimensions", "Dim", "The dimensions of the resulting graph", GH_ParamAccess.item);
            pManager.Register_StringParam("Plot Type", "Typ", "The type of graph to plot.  Choose 'Ortho' or 'Stereo', defaults to Stereo.", "Stereographic", GH_ParamAccess.item);

            pManager.Register_StringParam("Solar Altitude Key", "Alt Key", "The key related to the solar altitude", "solar_altitude", GH_ParamAccess.item);
            pManager.Register_StringParam("Solar Azimuth Key", "Azm Key", "The key related to the solar azimuth", "solar_azimuth", GH_ParamAccess.item);
            pManager.Register_BooleanParam("Cull Nighttime", "Cull Night", "Cull nighttime hours?", true, GH_ParamAccess.item);

            this.Params.Input[1].Optional = true;
            this.Params.Input[2].Optional = true;
            //this.Params.Input[3].Optional = true;
            this.Params.Input[4].Optional = true;
            this.Params.Input[5].Optional = true;
            this.Params.Input[6].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager) {
            pManager.RegisterParam(new GHParam_DHr(), "DHours", "Dhrs", "The spatialized Dhours", GH_ParamAccess.list);
            pManager.Register_PointParam("Positions", "Pts", "The assigned positions of the hours", GH_ParamAccess.list);
            pManager.Register_CurveParam("Lines", "Lns", "The lines", GH_ParamAccess.list);
            pManager.Register_MeshParam("Mesh", "Msh", "A mesh representing the resulting solarplot, with vertex colors assigned where applicable", GH_ParamAccess.item);
            pManager.Register_GeometryParam("Trim Boundary", "Bnd", "A Trimming Boundary.  May be a rectangle or a circle, depending on the plot type.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA) {
            List<DHr> hours = new List<DHr>();
            string alt_key = "solar_altitude";
            Interval alt_ival = new Interval(0, Math.PI / 2);
            string azm_key = "solar_azimuth";
            Interval azm_ival = new Interval(0, Math.PI * 2);
            bool cull_night = true;

            if (DA.GetDataList(0, hours)) {

                this.plot_type = PType.Invalid;
                String p_type_string = "";
                DA.GetData(3, ref p_type_string);
                p_type_string = p_type_string.ToLowerInvariant().Trim();
                if (p_type_string.Contains("stereo")) { this.plot_type = PType.Stereo; }
                if (p_type_string.Contains("ortho")) { this.plot_type = PType.Ortho; }
                if (this.plot_type == PType.Invalid) {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Plot type not recognized. Choose 'Ortho' or 'Stereo'.");
                    return;
                }

                Plane plane = new Plane(new Point3d(0, 0, 0), new Vector3d(0, 0, 1));
                DA.GetData(1, ref plane);

                Grasshopper.Kernel.Types.UVInterval ival2d = new Grasshopper.Kernel.Types.UVInterval();
                if (!DA.GetData(2, ref ival2d)) {
                    ival2d.U0 = 0.0;
                    ival2d.U1 = 1.0;
                    ival2d.V0 = 0.0;
                    ival2d.V1 = 1.0;
                }

                DA.GetData(4, ref alt_key);
                DA.GetData(5, ref azm_key);
                DA.GetData(6, ref cull_night);


                List<Point3d> points = new List<Point3d>();

                Plane drawPlane = new Plane(plane);
                if (this.plot_type == PType.Ortho) DA.SetData(4, new Rectangle3d(drawPlane, ival2d.U, ival2d.V)); // set the trimming boundary
                if (this.plot_type == PType.Stereo) {
                    drawPlane.Origin = new Point3d(plane.Origin.X, plane.Origin.Y + ival2d.V.Mid, plane.Origin.Z);
                    drawPlane.Rotate(-Math.PI / 2, drawPlane.ZAxis);
                    ival2d.V0 = ival2d.V.Length / 2; // sets the radius of stereographic plots
                    ival2d.V1 = 0.0;

                    Circle circ = new Circle(drawPlane, Math.Abs(ival2d.V.Length));
                    DA.SetData(4, circ); // set the trimming boundary
                }


                for (int h = 0; h < hours.Count; h++) {
                    double x = 0;
                    double y = 0;
                    switch (this.plot_type) {
                        case PType.Ortho:
                            x = ival2d.U.ParameterAt(azm_ival.NormalizedParameterAt(hours[h].val(azm_key)));
                            y = ival2d.V.ParameterAt(alt_ival.NormalizedParameterAt(hours[h].val(alt_key)));
                            break;

                        case PType.Stereo:
                            double radius = ival2d.V.ParameterAt(alt_ival.NormalizedParameterAt(hours[h].val(alt_key)));

                            x = radius * Math.Cos(hours[h].val(azm_key));
                            y = radius * Math.Sin(hours[h].val(azm_key));
                            break;
                    }
                    Point3d pt = drawPlane.PointAt(x, y);
                    points.Add(pt);
                    hours[h].pos = pt; // TODO: solar plots currently record the point in world space, switch to graph space
                }

                List<Polyline> plines = new List<Polyline>();
                for (int h = 0; h < points.Count; h++) {
                    List<Point3d> pts = new List<Point3d>();
                    DHr prev_hr; if (h > 0) prev_hr = hours[h - 1]; else prev_hr = hours[hours.Count - 1];
                    DHr this_hr = hours[h];
                    DHr next_hr; if (h < hours.Count - 1) next_hr = hours[h + 1]; else next_hr = hours[0];

                    switch (this.plot_type) {
                        case PType.Ortho:
                            if ((this_hr.hr - 1 == prev_hr.hr) && (this_hr.pos_x > prev_hr.pos_x)) pts.Add(interp(this_hr.pos, prev_hr.pos, 0.5));
                            pts.Add(this_hr.pos);
                            if ((this_hr.hr + 1 == next_hr.hr) && (this_hr.pos_x < next_hr.pos_x)) pts.Add(interp(this_hr.pos, next_hr.pos, 0.5));
                            break;

                        case PType.Stereo:
                            pts.Add(interp(this_hr.pos, prev_hr.pos, 0.5));
                            pts.Add(this_hr.pos);
                            pts.Add(interp(this_hr.pos, next_hr.pos, 0.5));
                            break;
                    }

                    plines.Add(new Polyline(pts));
                }

                Mesh mesh = CreateColoredMesh(hours, points, plines, cull_night, alt_key);
                mesh.Compact();
                mesh.Weld(0.1);

                if (cull_night) {
                    List<DHr> day_hours = new List<DHr>();
                    List<Point3d> day_points = new List<Point3d>();
                    List<Polyline> day_plines = new List<Polyline>();
                    for (int h = 1; h < hours.Count - 1; h++) {
                        if (hour_contains_day(hours, h, alt_key)) {
                            day_hours.Add(hours[h]);
                            day_points.Add(points[h]);
                            day_plines.Add(plines[h]);
                        }
                    }
                    hours = day_hours;
                    points = day_points;
                    plines = day_plines;

                    #region failed attempt to trim mesh
                    //trim mesh
                    /*
                    Plane trimPlane = new Plane(plane);
                    trimPlane.Rotate(Math.PI/2,plane.XAxis);
                    Mesh trimMesh = Mesh.CreateFromPlane(trimPlane, ival2d.U, new Interval(-0.1, 0.1), 10, 10);
                    //Mesh[] result = Mesh.CreateBooleanSplit(new Mesh[] { mesh }, new Mesh[] { trimMesh });
                    Mesh[] result = mesh.Split(trimPlane);
                    if (result.Length == 1) mesh = result[0];
                    else if (result.Length == 2) {
                        Point3d pt = plane.PointAt(0, 1);
                        Point3d p0 = result[0].ClosestPoint(pt);
                        Point3d p1 = result[1].ClosestPoint(pt);

                        if (p0.DistanceTo(pt) < p1.DistanceTo(pt)) mesh = result[0];
                        else mesh = result[1];
                    }
                     */

                    #endregion

                }

                DA.SetDataList(0, hours);
                DA.SetDataList(1, points);
                DA.SetDataList(2, plines);
                DA.SetData(3, mesh);
            }
        }

        private bool hour_contains_day(List<DHr> hours, int h, string alt_key) {
            return ((hours[h].val(alt_key) >= 0) || ((h > 0) && (hours[h - 1].val(alt_key) >= 0)) || ((h < hours.Count - 1) && (hours[h + 1].val(alt_key) >= 0)));

        }

        private Mesh CreateColoredMesh(List<DHr> hours, List<Point3d> points, List<Polyline> plines, bool cull_night, String alt_key) {
            Mesh mesh = new Mesh();

            for (int h = 0; h < hours.Count; h++) {
                //if (cull_night && ((hours[h].val(alt_key) < 0) && ((h > 0) && (hours[h - 1].val(alt_key) < 0)) && ((h < hours.Count - 1) && (hours[h + 1].val(alt_key) < 0)))) continue;
                if (cull_night && !(hour_contains_day(hours, h, alt_key))) continue;
                DHr this_hr = hours[h];
                Polyline this_pl = plines[h];

                if (this_pl.Count == 3) {

                    // previous day
                    int prev_i = hours.FindIndex(item => item.hr == this_hr.hr - 24);
                    if (prev_i >= 0) {
                        Polyline that_pl = plines[prev_i];
                        if (that_pl.Count == 3) {
                            mesh.Vertices.Add(points[h]);

                            mesh.Vertices.Add(this_pl[0]);
                            mesh.Vertices.Add(this_pl[2]);

                            mesh.Vertices.Add(that_pl[1]);

                            int pp = 4; // number of points added here
                            int p0 = mesh.Vertices.Count - pp; // index of first vertex added here
                            mesh.Faces.AddFace(p0, p0 + 1, p0 + 3);
                            mesh.Faces.AddFace(p0, p0 + 3, p0 + 2);

                            for (int n = 0; n < pp; n++) mesh.VertexColors.Add(hours[h].color);
                        }
                    }

                    // next day
                    int next_i = hours.FindIndex(item => item.hr == this_hr.hr + 24);
                    if (next_i >= 0) {
                        Polyline that_pl = plines[next_i];
                        if (that_pl.Count == 3) {
                            mesh.Vertices.Add(points[h]);

                            mesh.Vertices.Add(this_pl[0]);
                            mesh.Vertices.Add(this_pl[2]);

                            mesh.Vertices.Add(that_pl[0]);
                            mesh.Vertices.Add(that_pl[2]);

                            int pp = 5; // number of points added here
                            int p0 = mesh.Vertices.Count - pp; // index of first vertex added here
                            mesh.Faces.AddFace(p0, p0 + 3, p0 + 1);
                            mesh.Faces.AddFace(p0, p0 + 2, p0 + 4);

                            for (int n = 0; n < pp; n++) mesh.VertexColors.Add(hours[h].color);
                        }
                    }

                }
            }

            return mesh;
        }

        protected Point3d interp(Point3d pa, Point3d pb, double t) {
            double x = (pb.X - pa.X) * t + pa.X;
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
            pManager.Register_PlaneParam("Location", "Loc", "The location and orientation (as a plane) to draw this graph", new Plane(new Point3d(0, 0, 0), new Vector3d(0, 0, 1)), GH_ParamAccess.item);
            pManager.Register_2DIntervalParam("Graph Dimensions", "Dim", "The dimensions of the resulting graph", GH_ParamAccess.item);

            this.Params.Input[1].Optional = true;
            this.Params.Input[2].Optional = true;
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

                Plane plane = new Plane(new Point3d(0, 0, 0), new Vector3d(0, 0, 1));
                DA.GetData(1, ref plane);

                Grasshopper.Kernel.Types.UVInterval ival2d = new Grasshopper.Kernel.Types.UVInterval();
                if (!DA.GetData(2, ref ival2d)) {
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
                double delta_x = Math.Abs(ival2d.U.Length) / 365.0;
                double delta_y = Math.Abs(ival2d.V.Length) / 24.0;
                double delta_x2 = delta_x / 2.0;
                double delta_y2 = delta_y / 2.0;

                // only produce a mesh if given a full year worth of hours
                bool doMesh = false;
                Rhino.Geometry.Mesh mesh = new Mesh();
                if (hours.Count == 8760) doMesh = true;

                for (int h = 0; h < hours.Count; h++) {
                    //x = (math.floor(hr_out.hr/24)/365.0)
                    //y = ((hr_out.hr % 24)/24.0)
                    float x = (float)((ival2d.U.ParameterAt(Math.Floor(hours[h].hr / 24.0) / 365.0)) + delta_x2);
                    float y = (float)((ival2d.V.ParameterAt((hours[h].hr % 24) / 24.0)) + delta_y2);
                    Point3d pt = plane.PointAt(x, y, 0);
                    points.Add(pt);
                    hours[h].pos = new Point3d(x, y, 0);

                    if (doMesh) {
                        mesh.Vertices.Add(pt);
                        mesh.VertexColors.Add(hours[h].color);
                    }

                    Rectangle3d rect = new Rectangle3d(new Plane(new Point3d(pt.X - delta_x2, pt.Y - delta_y2, 0), new Vector3d(0, 0, 1)), delta_x, delta_y);
                    rects.Add(rect);
                }

                if (doMesh) {
                    for (int h = 0; h < 8760 - 24; h++) {
                        if (h % 24 != 0) mesh.Faces.AddFace(h, h - 1, h + 23, h + 24);
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


    public class Dhr_StackedHistogramGraphComponent : GH_Component {
        public Dhr_StackedHistogramGraphComponent()
            //Call the base constructor
            : base("Stacked Histogram Spatialization", "Histogram", "Assigns a position on a Histogram Graph for each hour given.  Hours are depicted as rectangles stacked according to pre-defined intervals.\nNote that a tree of Dhours is expected.\nUse the Hour Frequency component to prepare data for this component", "DYear", "Spatialize") { }
        public override Grasshopper.Kernel.GH_Exposure Exposure { get { return GH_Exposure.secondary; } }
        public override Guid ComponentGuid { get { return new Guid("{A577421B-A634-477E-BE7D-0D12D85E8800}"); } }
        protected override Bitmap Icon { get { return DYear.Properties.Resources.Olgay; } }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager) {
            pManager.RegisterParam(new GHParam_DHr(), "DHours", "Dhrs", "The Dhours to which to assign positions", GH_ParamAccess.tree);
            pManager.Register_PlaneParam("Location", "Loc", "The location and orientation (as a plane) to draw this graph", new Plane(new Point3d(0, 0, 0), new Vector3d(0, 0, 1)), GH_ParamAccess.item);
            pManager.Register_2DIntervalParam("Graph Dimensions", "Dim", "The dimensions of the resulting graph.\nNote that the y-dimension scales to the longest list of given hours.", GH_ParamAccess.item);
            pManager.Register_DoubleParam("Bar Width", "BWdth", "The width of each bar, as a percentage of available area (0->1).  Defaults to 1.0", 1.0, GH_ParamAccess.item);

            this.Params.Input[1].Optional = true;
            this.Params.Input[2].Optional = true;
            this.Params.Input[3].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager) {
            pManager.RegisterParam(new GHParam_DHr(), "DHours", "Dhrs", "The spatialized Dhours", GH_ParamAccess.tree);
            pManager.Register_PointParam("Positions", "pts", "The assigned positions of the hours (the center points of each stacked rectangle", GH_ParamAccess.tree);
            pManager.Register_RectangleParam("Stacked Rectangles", "srects", "Rectangles plotted on the resulting histogram, one per hour given.", GH_ParamAccess.tree);
            pManager.Register_RectangleParam("Rectangles", "rects", "Rectangles plotted on the resulting histogram, one per groups of hours given.", GH_ParamAccess.list);
            pManager.Register_MeshParam("Mesh", "msh", "A mesh representing the resulting histogram, with vertex colors assigned where applicable", GH_ParamAccess.list);
            pManager.Register_RectangleParam("Trim Boundary", "Bnd", "A Trimming Boundary. Useful for marking percentage of hours given against a 100% maximum", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA) {
            Grasshopper.Kernel.Data.GH_Structure<DHr> hourTreeIn = new Grasshopper.Kernel.Data.GH_Structure<DHr>();

            if (DA.GetDataTree(0, out hourTreeIn)) {
                Plane plane = new Plane(new Point3d(0, 0, 0), new Vector3d(0, 0, 1));
                DA.GetData(1, ref plane);

                Grasshopper.Kernel.Types.UVInterval ival2d = new Grasshopper.Kernel.Types.UVInterval();
                if (!DA.GetData(2, ref ival2d)) {
                    ival2d.U0 = 0.0;
                    ival2d.U1 = 1.0;
                    ival2d.V0 = 0.0;
                    ival2d.V1 = 2.0;
                }
                if (ival2d.U.IsDecreasing) ival2d.U.Swap();
                if (ival2d.V.IsDecreasing) ival2d.V.Swap();

                double barWidthScale = 1.0;
                DA.GetData(3, ref barWidthScale);


                Grasshopper.Kernel.Data.GH_Structure<DHr> hourTreeOut = new Grasshopper.Kernel.Data.GH_Structure<DHr>();
                Grasshopper.Kernel.Data.GH_Structure<Grasshopper.Kernel.Types.GH_Point> points = new Grasshopper.Kernel.Data.GH_Structure<Grasshopper.Kernel.Types.GH_Point>();
                Grasshopper.Kernel.Data.GH_Structure<Grasshopper.Kernel.Types.GH_Rectangle> srects = new Grasshopper.Kernel.Data.GH_Structure<Grasshopper.Kernel.Types.GH_Rectangle>();
                List<Rectangle3d> rects = new List<Rectangle3d>();
                List<Mesh> meshes = new List<Mesh>();

                int i = 0;//keeps track of which branch we're on... note, this assumes we've been passed a list of lists and nothing 'deeper'
                int maxHourCount = hourTreeIn.Branches[0].Count;
                foreach (List<DHr> hourList in hourTreeIn.Branches) if (hourList.Count > maxHourCount) maxHourCount = hourList.Count;
                Rectangle3d brect = new Rectangle3d(plane, new Interval(0, ival2d.U1), new Interval(0, ival2d.V.ParameterAt((double)hourTreeIn.DataCount / maxHourCount)));
                DA.SetData(5, brect);

                double dx = ival2d.U.Length / hourTreeIn.Branches.Count * barWidthScale;
                double dy = ival2d.V.Length / maxHourCount;
                foreach (List<DHr> hourList in hourTreeIn.Branches) {
                    Grasshopper.Kernel.Data.GH_Path path = new Grasshopper.Kernel.Data.GH_Path(i);
                    hourTreeOut.EnsurePath(path);
                    points.EnsurePath(path);
                    srects.EnsurePath(path);

                    double x = ival2d.U.ParameterAt((i + 0.5) / hourTreeIn.Branches.Count);
                    for (int j = 0; j < hourList.Count; j++) {
                        DHr dhour = hourList[j];

                        double y = ival2d.V.ParameterAt((j + 0.5) / maxHourCount);
                        Point3d pt = plane.PointAt(x, y);
                        dhour.pos = new Point3d(x, y, 0);
                        points.Append(new Grasshopper.Kernel.Types.GH_Point(pt), path);
                        hourTreeOut.Append(dhour, path);

                        Plane pln = new Plane(plane);
                        pln.Origin = plane.PointAt(x - dx / 2, y - dy / 2);
                        Rectangle3d rct = new Rectangle3d(pln, dx, dy);
                        srects.Append(new Grasshopper.Kernel.Types.GH_Rectangle(rct), path);
                    }

                    // make meshes & big rectangles
                    if (hourList.Count > 0) {
                        Mesh mesh = new Mesh();
                        // add bottom pts
                        mesh.Vertices.Add(plane.PointAt(x - dx / 2, 0));
                        mesh.VertexColors.Add(hourList[0].color);
                        mesh.Vertices.Add(plane.PointAt(x + dx / 2, 0));
                        mesh.VertexColors.Add(hourList[0].color);

                        for (int j = 0; j < hourList.Count; j++) {
                            double y = ival2d.V.ParameterAt((j + 0.5) / maxHourCount);
                            mesh.Vertices.Add(plane.PointAt(x - dx / 2, y));
                            mesh.VertexColors.Add(hourList[j].color);
                            mesh.Vertices.Add(plane.PointAt(x + dx / 2, y));
                            mesh.VertexColors.Add(hourList[j].color);
                        }

                        // add top pts
                        double yy = ival2d.V.ParameterAt((float)hourList.Count / maxHourCount);
                        mesh.Vertices.Add(plane.PointAt(x - dx / 2, yy));
                        mesh.VertexColors.Add(hourList[hourList.Count - 1].color);
                        mesh.Vertices.Add(plane.PointAt(x + dx / 2, yy));
                        mesh.VertexColors.Add(hourList[hourList.Count - 1].color);

                        for (int n = 2; n < mesh.Vertices.Count; n = n + 2) mesh.Faces.AddFace(n - 2, n - 1, n + 1, n);
                        meshes.Add(mesh);

                        Plane pln = new Plane(plane);
                        pln.Origin = plane.PointAt(x - dx / 2, 0);
                        Rectangle3d rct = new Rectangle3d(pln, dx, yy);
                        rects.Add(rct);
                    }

                    i++;
                }

                DA.SetDataTree(0, hourTreeOut);
                DA.SetDataTree(1, points);
                DA.SetDataList(2, srects);
                DA.SetDataList(3, rects);
                DA.SetDataList(4, meshes);

            }
        }

    }


    public class Dhr_TimeValueGraphComponent : GH_Component {
        public Dhr_TimeValueGraphComponent()
            //Call the base constructor
            : base("Time-Value Spatialization", "TimeVal", "Assigns a position on a Time-Value Graph (including bar graphs and line graphs) for each hour given.", "DYear", "Spatialize") { }
        public override Grasshopper.Kernel.GH_Exposure Exposure { get { return GH_Exposure.secondary; } }
        public override Guid ComponentGuid { get { return new Guid("{C1DEEFD9-BADF-4189-813C-AF3DFB01AF80}"); } }
        protected override Bitmap Icon { get { return DYear.Properties.Resources.Olgay; } }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager) {
            pManager.RegisterParam(new GHParam_DHr(), "DHours", "Dhrs", "The Dhours to which to assign positions", GH_ParamAccess.list);
            pManager.Register_StringParam("Key", "Key", "The key associated with the y-axis", GH_ParamAccess.item);
            pManager.Register_IntervalParam("Domain", "Rng", "The domain associating values with the graph height.  In effect, sets the vertical scale of the graph.  Defaults to the Max and Min of given values.", GH_ParamAccess.item);
            pManager.Register_PlaneParam("Location", "Loc", "The location and orientation (as a plane) to draw this graph", new Plane(new Point3d(0, 0, 0), new Vector3d(0, 0, 1)), GH_ParamAccess.item);
            pManager.Register_2DIntervalParam("Graph Dimensions", "Dim", "The dimensions of the resulting graph", GH_ParamAccess.item);
            pManager.Register_DoubleParam("Bar Width", "BWdth", "The width of each bar, as a percentage of available area (0->1).  Defaults to 1.0", 1.0, GH_ParamAccess.item);

            this.Params.Input[2].Optional = true;
            this.Params.Input[3].Optional = true;
            this.Params.Input[4].Optional = true;
            this.Params.Input[5].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager) {
            pManager.RegisterParam(new GHParam_DHr(), "DHours", "Dhrs", "The spatialized Dhours", GH_ParamAccess.list);
            pManager.Register_PointParam("Positions", "pts", "The assigned positions of the hours", GH_ParamAccess.list);
            pManager.Register_RectangleParam("Rectangles", "rects", "Rectangles that form a bar graph plotted on the resulting graph, one rectangle per hour given.", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA) {
            List<DHr> hours = new List<DHr>();
            String key = "";
            if (DA.GetDataList(0, hours) && DA.GetData(1, ref key)) {
                if ((hours[0].is_surrogate) && ((hours.Count != 1) && (hours.Count != 12) && (hours.Count != 52) && (hours.Count != 365))) { this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "This component can only plot unmasked surrogate hours for yearly, monthly, weekly, and daily statistics"); } 


                Interval ival_y = new Interval();
                float[] vals = new float[0];
                if (!(DA.GetData(2, ref ival_y))) DHr.get_domain(key, hours.ToArray(), ref vals, ref ival_y);
                else {
                    vals = new float[hours.Count];
                    for (int h = 0; h < hours.Count; h++) vals[h] = hours[h].val(key);
                }

                Plane plane = new Plane(new Point3d(0, 0, 0), new Vector3d(0, 0, 1));
                DA.GetData(3, ref plane);

                Grasshopper.Kernel.Types.UVInterval ival2d = new Grasshopper.Kernel.Types.UVInterval();
                if (!DA.GetData(4, ref ival2d)) {
                    ival2d.U0 = 0.0;
                    ival2d.U1 = 12.0;
                    ival2d.V0 = 0.0;
                    ival2d.V1 = 1.0;
                }

                double barWidth = 1.0;
                DA.GetData(5, ref barWidth);

                List<Point3d> points = new List<Point3d>();
                List<Rectangle3d> rects = new List<Rectangle3d>();
                //double delta_x = Math.Abs(ival2d.U.Length) / hours.Count;
                //double delta_x2 = delta_x / 2.0;

                for (int h = 0; h < hours.Count; h++) {
                    Point3d gpt = GraphPoint(hours[h].hr, vals[h], plane, ival_y, ival2d); // returns a point in graph coordinates
                    hours[h].pos = gpt; // the hour records the point in graph coordinates

                    Point3d wpt = plane.PointAt(gpt.X, gpt.Y);
                    points.Add(wpt); // adds this point in world coordinates

                    Interval ival_gx; // interval of horz space occupied by this hour in graphic units
                    Interval ival_gy = new Interval(ival2d.V0, gpt.Y); // interval of vertical space occupied by this hour in graphic units
                    if (!hours[h].is_surrogate) {
                        double delta_x2 = (Math.Abs(ival2d.U.Length) / 8760 / 2.0);
                        ival_gx = new Interval(gpt.X - delta_x2, gpt.X + delta_x2); // interval of horz space occupied by this hour in graphic units
                    } else {
                        // if we've been passed surrogate hours, the spacing between bars may not be consistant
                        // we assume we've been given an hour at the start of the range represented
                        double ival_gx_0 = gpt.X;
                        //if (h > 0) {
                        //    Point3d pt_prev = GraphPoint(hours[h - 1].hr, vals[h - 1], plane, ival_y, ival2d);
                        //    ival_gx_0 = gpt.X - (gpt.X - pt_prev.X) * barWidth;
                        //} else { ival_gx_0 = gpt.X - (gpt.X - ival2d.U0) * barWidth; }
                        double ival_gx_1;
                        if (h < hours.Count - 1) {
                            Point3d pt_next = GraphPoint(hours[h+1].hr, vals[h + 1], plane, ival_y, ival2d);
                            ival_gx_1 = gpt.X + (pt_next.X - gpt.X) * barWidth;
                        } else { ival_gx_1 = gpt.X + (ival2d.U1 - gpt.X) * barWidth; }
                        ival_gx = new Interval(ival_gx_0, ival_gx_1);
                        if (hours.Count == 1) ival_gx = ival2d.U;
                    }
                    Rectangle3d rect = new Rectangle3d(plane, ival_gx, ival_gy);
                    rects.Add(rect);
                }

                DA.SetDataList(0, hours);
                DA.SetDataList(1, points);
                DA.SetDataList(2, rects);
                //DA.SetData(3, mesh);

            }
        }

        private Point3d GraphPoint(int hour_of_year, float value, Plane plane, Interval ival_y, Grasshopper.Kernel.Types.UVInterval ival2d) {
            // returns a point in graph coordinates, ready to be plotted on a given plane
            double x = ival2d.U.ParameterAt((hour_of_year+0.5) / 8760.0);
            double y = ival2d.V.ParameterAt(ival_y.NormalizedParameterAt(value));
            Point3d pt = new Point3d(x, y, 0);
            return pt;
        }

    }



    public class Dhr_DiurnalTimeValueGraphComponent : GH_Component {
        public Dhr_DiurnalTimeValueGraphComponent()
            //Call the base constructor
            : base("Diurnal Time-Value Spatialization", "DiurnalTimeVal", "Assigns a position on a series of Diurnal Time-Value Subgraphs (including bar graphs and line graphs) for each hour given.", "DYear", "Spatialize") { }
        public override Grasshopper.Kernel.GH_Exposure Exposure { get { return GH_Exposure.secondary; } }
        public override Guid ComponentGuid { get { return new Guid("{DD351576-711F-461F-A85A-9E2BA8D86E6C}"); } }
        protected override Bitmap Icon { get { return DYear.Properties.Resources.Olgay; } }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager) {
            pManager.RegisterParam(new GHParam_DHr(), "DHours", "Dhrs", "The Dhours to which to assign positions", GH_ParamAccess.tree);
            pManager.Register_StringParam("Key", "Key", "The key associated with the y-axis", GH_ParamAccess.item);
            pManager.Register_IntervalParam("Domain", "Rng", "The domain associating values with the graph height.  In effect, sets the vertical scale of the graph.  Defaults to the Max and Min of given values.", GH_ParamAccess.item);
            pManager.Register_PlaneParam("Location", "Loc", "The location and orientation (as a plane) to draw this graph", new Plane(new Point3d(0, 0, 0), new Vector3d(0, 0, 1)), GH_ParamAccess.item);
            pManager.Register_2DIntervalParam("Graph Dimensions", "Dim", "The dimensions of the resulting graph", GH_ParamAccess.item);
            pManager.Register_DoubleParam("Subgraph Width", "Wid", "The width of each diurnal subgraph, as a percentage of available area (0->1).  Defaults to 1.0", 1.0, GH_ParamAccess.item);

            this.Params.Input[2].Optional = true;
            this.Params.Input[3].Optional = true;
            this.Params.Input[4].Optional = true;
            this.Params.Input[5].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager) {
            pManager.RegisterParam(new GHParam_DHr(), "DHours", "Dhrs", "The spatialized Dhours", GH_ParamAccess.tree);
            pManager.Register_PointParam("Positions", "pts", "The assigned positions of the hours", GH_ParamAccess.tree);
            pManager.Register_RectangleParam("Rectangles", "rects", "Rectangles that form a bar graph plotted on each resulting subgraph, one rectangle per hour given.", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA) {
            Grasshopper.Kernel.Data.GH_Structure<DHr> hourTreeIn = new Grasshopper.Kernel.Data.GH_Structure<DHr>();
            String key = "";
            if (DA.GetDataTree(0, out hourTreeIn) && DA.GetData(1, ref key)) {
                
                Interval ival_y = new Interval();
                float[] garbage = new float[0];
                if (!(DA.GetData(2, ref ival_y))) DHr.get_domain(key, hourTreeIn.ToArray(), ref garbage, ref ival_y); // vals are no good here, we would need a tree of values
                
                Plane plane = new Plane(new Point3d(0, 0, 0), new Vector3d(0, 0, 1));
                DA.GetData(3, ref plane);

                Grasshopper.Kernel.Types.UVInterval ival2d = new Grasshopper.Kernel.Types.UVInterval();
                if (!DA.GetData(4, ref ival2d)) {
                    ival2d.U0 = 0.0;
                    ival2d.U1 = 12.0;
                    ival2d.V0 = 0.0;
                    ival2d.V1 = 1.0;
                }

                double barWidth = 1.0;
                DA.GetData(5, ref barWidth);

                Grasshopper.Kernel.Data.GH_Structure<DHr> hourTreeOut = new Grasshopper.Kernel.Data.GH_Structure<DHr>();
                Grasshopper.Kernel.Data.GH_Structure<Grasshopper.Kernel.Types.GH_Point> points = new Grasshopper.Kernel.Data.GH_Structure<Grasshopper.Kernel.Types.GH_Point>();
                Grasshopper.Kernel.Data.GH_Structure<Grasshopper.Kernel.Types.GH_Rectangle> rects = new Grasshopper.Kernel.Data.GH_Structure<Grasshopper.Kernel.Types.GH_Rectangle>();

                double ival2d_u_delta = ival2d.U.Length / hourTreeIn.Branches.Count;

                for (int b = 0; b < hourTreeIn.Branches.Count; b++) {
                    List<DHr> hours = hourTreeIn.Branches[b];
                    Grasshopper.Kernel.Data.GH_Path path = new Grasshopper.Kernel.Data.GH_Path(b);
                    hourTreeOut.EnsurePath(path);
                    points.EnsurePath(path);
                    rects.EnsurePath(path);

                    Grasshopper.Kernel.Types.UVInterval ival2d_sub = new Grasshopper.Kernel.Types.UVInterval(new Interval(b * ival2d_u_delta, (b + 1) * ival2d_u_delta), ival2d.V);
                    double t0_sub = ival2d_sub.U.Mid - (ival2d_sub.U.Mid - ival2d_sub.U.T0) * barWidth;
                    double t1_sub = ival2d_sub.U.Mid + (ival2d_sub.U.T1 - ival2d_sub.U.Mid) * barWidth;
                    ival2d_sub.U = new Interval(t0_sub, t1_sub);

                    for (int h = 0; h < hours.Count; h++) {
                        Point3d gpt = GraphPoint(hours[h].hr%24, hours[h].val(key), plane, ival_y, ival2d_sub); // returns a point in graph coordinates
                        hours[h].pos = gpt; // the hour records the point in graph coordinates

                        Point3d wpt = plane.PointAt(gpt.X, gpt.Y);
                        points.Append(new Grasshopper.Kernel.Types.GH_Point(wpt), path);  // adds this point in world coordinates

                        double delta_x2 = (Math.Abs(ival2d_sub.U.Length) / 24.0 / 2.0);
                        Interval ival_gx = new Interval(gpt.X - delta_x2, gpt.X + delta_x2); // interval of horz space occupied by this hour in graphic units
                        Interval ival_gy = new Interval(ival2d_sub.V0, gpt.Y); // interval of vertical space occupied by this hour in graphic units

                        Rectangle3d rect = new Rectangle3d(plane, ival_gx, ival_gy);
                        rects.Append(new Grasshopper.Kernel.Types.GH_Rectangle(rect), path);  
                         
                    }
                }

                DA.SetDataTree(0, hourTreeOut);
                DA.SetDataTree(1, points);
                DA.SetDataTree(2, rects);

            }
        }

        private Point3d GraphPoint(int hour_of_day, float value, Plane plane, Interval ival_y, Grasshopper.Kernel.Types.UVInterval ival2d) {
            // returns a point in graph coordinates, ready to be plotted on a given plane
            double x = ival2d.U.ParameterAt((hour_of_day + 0.5) / 24);
            double y = ival2d.V.ParameterAt(ival_y.NormalizedParameterAt(value));
            Point3d pt = new Point3d(x, y, 0);
            return pt;
        }

    }


}
