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
            pManager.Register_2DIntervalParam("Graph Dimensions", "Dim", "The dimensions of the resulting orthographic graph.  This parameter is ignored when plotting stereographic graphs", GH_ParamAccess.item);
            pManager.Register_DoubleParam("Graph Radius", "Rad", "The dimensions of the resulting stereographic graph.  This parameter is ignored when plotting orthographic graphs", 3.0, GH_ParamAccess.item);
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
            pManager.Register_CurveParam("Lines", "Lns", "Lines that represent the area on the resulting graph occupied by each hour given.", GH_ParamAccess.list);
            pManager.Register_ColourParam("Colors", "Clrs", "Colors corresponding to each line produced.", GH_ParamAccess.item);
            pManager.Register_MeshParam("Mesh", "Msh", "A mesh representing the resulting graph, with vertex colors assigned where applicable.", GH_ParamAccess.item);
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
                DA.GetData(4, ref p_type_string);
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
                double radius_gr = 3.0;
                DA.GetData(3, ref radius_gr);

                DA.GetData(5, ref alt_key);
                DA.GetData(6, ref azm_key);
                DA.GetData(7, ref cull_night);


                List<Point3d> points = new List<Point3d>();

                Plane drawPlane = new Plane(plane);

                if (this.plot_type == PType.Ortho) DA.SetData(5, new Rectangle3d(drawPlane, ival2d.U, ival2d.V)); // set the trimming boundary
                if (this.plot_type == PType.Stereo)
                {
                    drawPlane.Origin = new Point3d(plane.Origin.X, plane.Origin.Y + ival2d.V.Mid, plane.Origin.Z);
                    drawPlane.Rotate(-Math.PI / 2, drawPlane.ZAxis);
                    ival2d.V0 = radius_gr; // sets the radius of stereographic plots
                    ival2d.V1 = 0.0;

                    Circle circ = new Circle(drawPlane, Math.Abs(ival2d.V.Length));
                    DA.SetData(5, circ); // set the trimming boundary
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
                            double theta = -hours[h].val(azm_key); // reversed theta to ensure clockwise direction
                            x = radius * Math.Cos(theta);
                            y = radius * Math.Sin(theta);
                            break;
                    }
                    Point3d pt = drawPlane.PointAt(x, y);
                    points.Add(pt);
                    hours[h].pos = pt; // TODO: solar plots currently record the point in world space, switch to graph space
                }

                List<Polyline> plines = new List<Polyline>();
                List<Color> colors = new List<Color>();
                for (int h = 0; h < points.Count; h++)
                {
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
                    colors.Add(hours[h].color);
                }

                Mesh mesh = CreateColoredMesh(hours, points, plines, cull_night, alt_key);
                mesh.Compact();
                mesh.Weld(0.1);

                if (cull_night) {
                    List<DHr> day_hours = new List<DHr>();
                    List<Point3d> day_points = new List<Point3d>();
                    List<Polyline> day_plines = new List<Polyline>();

                    List<Color> day_colors = new List<Color>();
                    for (int h = 1; h < hours.Count - 1; h++)
                    {
                        if (hour_contains_day(hours, h, alt_key))
                        {
                            day_hours.Add(hours[h]);
                            day_points.Add(points[h]);
                            day_plines.Add(plines[h]);
                            day_colors.Add(colors[h]);
                        }
                    }
                    hours = day_hours;
                    points = day_points;
                    plines = day_plines;
                    colors = day_colors;

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

                DA.SetDataList(0, new List<DHr>(hours));
                DA.SetDataList(1, points);
                DA.SetDataList(2, plines);
                DA.SetDataList(3, colors);
                DA.SetData(4, mesh);
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
            pManager.Register_PointParam("Positions", "Pts", "The assigned positions of the hours", GH_ParamAccess.list);
            pManager.Register_RectangleParam("Regions", "Rgns", "Regions that represent the area on the resulting graph occupied by each hour given.", GH_ParamAccess.list);
            pManager.Register_MeshParam("Mesh", "Msh", "A mesh representing the resulting graph, with vertex colors assigned where applicable.", GH_ParamAccess.item);
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
            pManager.Register_PointParam("Positions", "Pts", "The assigned positions of the hours (the center points of each stacked rectangle", GH_ParamAccess.tree);
            pManager.Register_RectangleParam("Regions", "Rgns", "Regions that represent the area on the resulting graph occupied by each hour given.", GH_ParamAccess.tree);
            pManager.Register_RectangleParam("Rectangles", "Rcts", "Rectangles plotted on the resulting histogram, one per groups of hours given.", GH_ParamAccess.list);
            pManager.Register_MeshParam("Mesh", "Msh", "A mesh representing the resulting graph, with vertex colors assigned where applicable.", GH_ParamAccess.item);
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
            pManager.Register_IntervalParam("Scale", "Scl", "An interval that associates hour values with the graph height.  In effect, sets the vertical scale of the graph.  Defaults to the Max and Min of given values.", GH_ParamAccess.item);
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
            pManager.Register_PointParam("Positions", "Pts", "The assigned positions of the hours", GH_ParamAccess.list);
            pManager.Register_RectangleParam("Regions", "Rgns", "Regions that represent the area on the resulting graph occupied by each hour given.  These rectangles form a bar graph plotted on the resulting graph, one rectangle per hour given.", GH_ParamAccess.list);
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
                            Point3d pt_next = GraphPoint(hours[h + 1].hr, vals[h + 1], plane, ival_y, ival2d);
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
            double x = ival2d.U.ParameterAt((hour_of_year + 0.5) / 8760.0);
            double y = ival2d.V.ParameterAt(ival_y.NormalizedParameterAt(value));
            Point3d pt = new Point3d(x, y, 0);
            return pt;
        }

    }


    public class Dhr_StackedTimeValueGraphComponent : GH_Component {
        public Dhr_StackedTimeValueGraphComponent()
            //Call the base constructor
            : base("Stacked Time-Value Spatialization", "StackedTimeVal", "Assigns a position on a Stacked Time-Value Graph (including stacked bar graphs and area graphs) for each hour given.", "DYear", "Spatialize") { }
        public override Grasshopper.Kernel.GH_Exposure Exposure { get { return GH_Exposure.tertiary; } }
        public override Guid ComponentGuid { get { return new Guid("{441D0C56-8D20-4DF4-8334-169BA17C985E}"); } }
        protected override Bitmap Icon { get { return DYear.Properties.Resources.Olgay; } }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager) {
            pManager.RegisterParam(new GHParam_DHr(), "DHours", "Dhrs", "The Dhours to which to assign positions", GH_ParamAccess.list);
            pManager.Register_StringParam("Keys", "Keys", "The keys associated with the y-axis.  Values will be stacked in the order in which these keys are given.", GH_ParamAccess.list);
            pManager.Register_IntervalParam("Scale", "Scl", "An interval that associates hour values with the graph height.  In effect, sets the vertical scale of the graph.  Defaults to 0->(Max/Min) of the values found in all the given keys.", GH_ParamAccess.item);
            pManager.Register_PlaneParam("Location", "Loc", "The location and orientation (as a plane) to draw this graph", new Plane(new Point3d(0, 0, 0), new Vector3d(0, 0, 1)), GH_ParamAccess.item);
            pManager.Register_2DIntervalParam("Graph Dimensions", "Dim", "The dimensions of the resulting graph", GH_ParamAccess.item);
            pManager.Register_DoubleParam("Bar Width", "BWdth", "The width of each bar, as a percentage of available area (0->1).  Defaults to 1.0", 1.0, GH_ParamAccess.item);

            this.Params.Input[2].Optional = true;
            this.Params.Input[3].Optional = true;
            this.Params.Input[4].Optional = true;
            this.Params.Input[5].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager) {
            pManager.RegisterParam(new GHParam_DHr(), "DHours", "Dhrs", "The spatialized Dhours.  A new stream of hours is produced for each key given.", GH_ParamAccess.tree);
            pManager.Register_IntervalParam("Ranges", "Ranges", "A tree of Intervals.  The first branch contains the interval plotted to the graph (the interval found in the Scale input, if set).  The second branch contains a list of intervals that describe the range of values found for the given list keys", GH_ParamAccess.tree);
            pManager.Register_PointParam("Positions", "Pts", "The assigned positions of the hours", GH_ParamAccess.tree);
            pManager.Register_PointParam("Base Points", "BsPts", "A point on the x-axis of the graph for each hour given.  Useful for making area charts.", GH_ParamAccess.tree);
            pManager.Register_RectangleParam("Regions", "Rgns", "Regions that represent the area on the resulting graph occupied by each hour given.  These rectangles form a bar graph plotted on the resulting graph, one rectangle per hour given.", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA) {
            List<DHr> hours = new List<DHr>();
            List<String> keys = new List<string>();
            if (DA.GetDataList(0, hours) && DA.GetDataList(1, keys)) {
                if ((hours[0].is_surrogate) && ((hours.Count != 1) && (hours.Count != 12) && (hours.Count != 52) && (hours.Count != 365))) { this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "This component can only plot unmasked surrogate hours for yearly, monthly, weekly, and daily statistics"); }


                Interval ival_y = new Interval();
                bool calc_ival_y = false;
                if (!(DA.GetData(2, ref ival_y))) {
                    ival_y.T0 = hours[0].val(keys[0]);
                    ival_y.T1 = hours[0].val(keys[0]);
                    calc_ival_y = true;
                }
                Dictionary<string, float[]> val_dict = new Dictionary<string, float[]>();
                List<Interval> val_ranges = new List<Interval>();
                foreach (string key in keys) {
                    Interval ival = new Interval();
                    float[] vals = new float[0];
                    DHr.get_domain(key, hours.ToArray(), ref vals, ref ival);
                    vals = new float[hours.Count];
                    for (int h = 0; h < hours.Count; h++) vals[h] = hours[h].val(key);
                    val_dict.Add(key, vals);
                    val_ranges.Add(ival);
                }

                bool force_start_at_zero = true;

                int stack_dir = StackDirection(val_ranges[0]);
                if (stack_dir==0) this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "The first key returned nothing but zero values.  I can't deal!");
                foreach (Interval ival in val_ranges) {
                    if (StackDirection(ival) != stack_dir) this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "A stacked graph can only handle all negative or all positive numbers.");
                    if (calc_ival_y) {
                        if (stack_dir < 1) {
                            if (ival.T1 > ival_y.T0) ival_y.T0 = ival.T1;
                            if (ival.T0 < ival_y.T1) ival_y.T1 = ival.T0;
                        } else {
                            if (ival.T0 < ival_y.T0) ival_y.T0 = ival.T0;
                            if (ival.T1 > ival_y.T1) ival_y.T1 = ival.T1;
                        }
                    }
                }

                if (force_start_at_zero && calc_ival_y) ival_y.T0 = 0;

                Grasshopper.Kernel.Data.GH_Structure<Grasshopper.Kernel.Types.GH_Interval> intervalTreeOut = new Grasshopper.Kernel.Data.GH_Structure<Grasshopper.Kernel.Types.GH_Interval>();
                intervalTreeOut.Append(new Grasshopper.Kernel.Types.GH_Interval(ival_y),new Grasshopper.Kernel.Data.GH_Path(0));
                foreach (Interval ival in val_ranges) intervalTreeOut.Append(new Grasshopper.Kernel.Types.GH_Interval(ival), new Grasshopper.Kernel.Data.GH_Path(1));


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
                Grasshopper.Kernel.Data.GH_Structure<Grasshopper.Kernel.Types.GH_Point> pointTreeOut = new Grasshopper.Kernel.Data.GH_Structure<Grasshopper.Kernel.Types.GH_Point>();
                Grasshopper.Kernel.Data.GH_Structure<Grasshopper.Kernel.Types.GH_Rectangle> rectTreeOut = new Grasshopper.Kernel.Data.GH_Structure<Grasshopper.Kernel.Types.GH_Rectangle>();
                List<Point3d> basepoints = new List<Point3d>();
                for (int h = 0; h < hours.Count; h++) {
                    Point3d gpt = GraphPoint(hours[h].hr, 0.0f, plane, ival_y, ival2d);
                    Point3d wpt = plane.PointAt(gpt.X, 0.0);
                    basepoints.Add(wpt);
                }

                for (int k = 0; k < keys.Count; k++) {
                    Grasshopper.Kernel.Data.GH_Path key_path = new Grasshopper.Kernel.Data.GH_Path(k);
                    List<Point3d> points = new List<Point3d>();
                    List<Rectangle3d> rects = new List<Rectangle3d>();
                    
                    for (int h = 0; h < hours.Count; h++) {
                        float val = val_dict[keys[k]][h];
                        for (int kk = 0; kk < k; kk++) val += val_dict[keys[kk]][h]; // add in all previous values
                        Point3d gpt = GraphPoint(hours[h].hr, val, plane, ival_y, ival2d); // returns a point in graph coordinates

                        hours[h].pos = gpt; // the hour records the point in graph coordinates
                        hourTreeOut.Append(new DHr(hours[h]), key_path);

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
                            double ival_gx_1;
                            if (h < hours.Count - 1) {
                                Point3d pt_next = GraphPoint(hours[h + 1].hr, val_dict[keys[k]][h + 1], plane, ival_y, ival2d);
                                ival_gx_1 = gpt.X + (pt_next.X - gpt.X) * barWidth;
                            } else { ival_gx_1 = gpt.X + (ival2d.U1 - gpt.X) * barWidth; }
                            ival_gx = new Interval(ival_gx_0, ival_gx_1);
                            if (hours.Count == 1) ival_gx = ival2d.U;
                            
                        }
                        Rectangle3d rect = new Rectangle3d(plane, ival_gx, ival_gy);
                        rects.Add(rect);
                    }

                    List<Grasshopper.Kernel.Types.GH_Point> gh_points = new List<Grasshopper.Kernel.Types.GH_Point>();
                    foreach (Point3d pt in points) gh_points.Add(new Grasshopper.Kernel.Types.GH_Point(pt));
                    pointTreeOut.AppendRange(gh_points, key_path);

                    List<Grasshopper.Kernel.Types.GH_Rectangle> gh_rects = new List<Grasshopper.Kernel.Types.GH_Rectangle>();
                    foreach (Rectangle3d rec in rects) gh_rects.Add(new Grasshopper.Kernel.Types.GH_Rectangle(rec));
                    rectTreeOut.AppendRange(gh_rects, key_path);

                    
                }

                DA.SetDataTree(0, hourTreeOut);
                DA.SetDataTree(1, intervalTreeOut);
                DA.SetDataTree(2, pointTreeOut);
                DA.SetDataList(3, basepoints);
                DA.SetDataTree(4, rectTreeOut);

            }
        }

        private static int StackDirection(Interval range) {
            int stack_dir = 0;

            if ((range.T0 == 0) && (range.T1 > 0)) {
                stack_dir = 1; //increasing stack
            } else if ((range.T1 == 0) && (range.T0 < 0)) {
                stack_dir = -1; //decreasing stack
            } else if (!(((range.T0 < 0) ^ (range.T1 < 0)))) {
                // t1 and t0 share a sign
                if (range.T0 > 0) stack_dir = 1;
                if (range.T0 < 0) stack_dir = -1;
            }
            return stack_dir;
        }

        private Point3d GraphPoint(int hour_of_year, float value, Plane plane, Interval ival_y, Grasshopper.Kernel.Types.UVInterval ival2d) {
            // returns a point in graph coordinates, ready to be plotted on a given plane
            double x = ival2d.U.ParameterAt((hour_of_year + 0.5) / 8760.0);
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
            pManager.Register_IntervalParam("Scale", "Scl", "An interval that associates hour values with the graph height.  In effect, sets the vertical scale of the graph.  Defaults to the Max and Min of given values.", GH_ParamAccess.item);
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
            pManager.Register_PointParam("Positions", "Pts", "The assigned positions of the hours", GH_ParamAccess.tree);
            pManager.Register_RectangleParam("Regions", "Rgns", "Regions that represent the area on the resulting graph occupied by each hour given.  These rectangles form a bar graph plotted on each resulting subgraph, one rectangle per hour given.", GH_ParamAccess.tree);
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
                        Point3d gpt = GraphPoint(hours[h].hr % 24, hours[h].val(key), plane, ival_y, ival2d_sub); // returns a point in graph coordinates
                        hours[h].pos = gpt; // the hour records the point in graph coordinates
                        hourTreeOut.Append(hours[h], path);

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


    public class Dhr_RadialValueValueGraphComponent : GH_Component {
        public Dhr_RadialValueValueGraphComponent()
            //Call the base constructor
            : base("Radial Value-Value Spatialization", "RadialValVal", "Assigns a position on a Radial Value-Value Graphs (like a radar plot or wind rose) for each hour given.", "DYear", "Spatialize") { }
        public override Grasshopper.Kernel.GH_Exposure Exposure { get { return GH_Exposure.tertiary; } }
        public override Guid ComponentGuid { get { return new Guid("{D33CA97B-223A-4E9C-ACB3-42365D176AD7}"); } }
        protected override Bitmap Icon { get { return DYear.Properties.Resources.Olgay; } }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager) {
            pManager.RegisterParam(new GHParam_DHr(), "DHours", "Dhrs", "The Dhours to which to assign positions", GH_ParamAccess.list);
            pManager.Register_StringParam("Radius Key", "KeyR", "The key associated with the radial dimension of the graph.", GH_ParamAccess.item);
            pManager.Register_StringParam("Angle Key", "KeyA", "The key associated with the angular dimension of the graph.", GH_ParamAccess.item);
            pManager.Register_IntervalParam("Radius Scale", "SclR", "An interval that associates hour values with the graph radius.  In effect, sets the radial scale of the graph.  Defaults to the Max and Min of given values.", GH_ParamAccess.item);
            pManager.Register_IntervalParam("Angle Scale", "SclA", "An interval that associates hour values with the graph angle.  In effect, sets the angular scale of the graph.  Defaults to 0->360.", new Interval(0, 360), GH_ParamAccess.item);
            pManager.Register_PlaneParam("Location", "Loc", "The location and orientation (as a plane) to draw this graph.  Note an angular value of zero will align with the x-axis of this plane.", new Plane(new Point3d(0, 0, 0), new Vector3d(0, 0, 1)), GH_ParamAccess.item);
            pManager.Register_IntervalParam("Graph Radii", "Rad", "An interval that sets the inner and outer radii of the resulting graph.  Defaults to 1.0->3.0", new Interval(1.0, 3.0), GH_ParamAccess.item);
            pManager.Register_IntervalParam("Subdivisions", "Divs", "An interval of two integer numbers that describe the number of subregions desired.  The first number corresponds to radius divisions, and the second to angular divisions.", new Interval(4, 3), GH_ParamAccess.item);

            this.Params.Input[3].Optional = true;
            this.Params.Input[5].Optional = true;
            this.Params.Input[6].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager) {
            pManager.RegisterParam(new GHParam_DHr(), "DHours", "Dhrs", "The spatialized Dhours", GH_ParamAccess.tree);
            pManager.Register_PointParam("Positions", "Pts", "The assigned positions of the hours", GH_ParamAccess.tree);
            pManager.Register_CurveParam("Regions", "Rgns", "Regions that represent the area on the resulting graph occupied by each hour given.  Useful in conjunction with HourFreq2 component.", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA) {
            List<DHr> hours = new List<DHr>();
            String key_r = "";
            String key_a = "";
            if (DA.GetDataList(0, hours) && DA.GetData(1, ref key_r) && DA.GetData(2, ref key_a)) {

                Interval ival_r = new Interval();
                Interval ival_a = new Interval();
                float[] vals_r = new float[0];
                float[] vals_a = new float[0];

                if (!(DA.GetData(3, ref ival_r))) DHr.get_domain(key_r, hours.ToArray(), ref vals_r, ref ival_r);
                else {
                    ival_r = new Interval(hours[0].val(key_r), hours[0].val(key_r));

                    vals_r = new float[hours.Count];
                    for (int h = 0; h < hours.Count; h++) {
                        vals_r[h] = hours[h].val(key_r);
                        if (vals_r[h] < ival_r.T0) ival_r.T0 = vals_r[h];
                        if (vals_r[h] > ival_r.T1) ival_r.T1 = vals_r[h];
                    }
                }

                DHr.get_domain(key_a, hours.ToArray(), ref vals_a, ref ival_a);
                DA.GetData(4, ref ival_a);

                Plane plane = new Plane(new Point3d(0, 0, 0), new Vector3d(0, 0, 1));
                DA.GetData(5, ref plane);

                Interval ival_gr = new Interval();
                Interval ival_ga = new Interval(0, Math.PI * 2);
                DA.GetData(6, ref ival_gr);

                Interval subdivs = new Interval();
                DA.GetData(7, ref subdivs);
                int subdivs_r = (int)Math.Floor(subdivs.T0);
                int subdivs_a = (int)Math.Floor(subdivs.T1);

                List<Point3d> points = new List<Point3d>();
                for (int h = 0; h < hours.Count; h++) {
                    double radius = ival_gr.ParameterAt(ival_r.NormalizedParameterAt(vals_r[h]));
                    double theta = ival_a.NormalizedParameterAt(vals_a[h]) * Math.PI * 2;
                    Point3d gpt = PointByCylCoords(radius, theta); // a point in graph coordinates
                    hours[h].pos = gpt; // the hour records the point in graph coordinates

                    Point3d wpt = plane.PointAt(gpt.X, gpt.Y);
                    points.Add(wpt);  // adds this point in world coordinates
                }

                Grasshopper.Kernel.Data.GH_Structure<Grasshopper.Kernel.Types.GH_Curve> regions = new Grasshopper.Kernel.Data.GH_Structure<Grasshopper.Kernel.Types.GH_Curve>();

                int segments_in_whole_circle = 36;
                //double step_r = ival_r.Length / subdivs_r;
                //double step_a = Math.PI*2 / subdivs_a;

                for (int r = 0; r < subdivs_r; r++)
                    for (int a = 0; a < subdivs_a; a++) {
                        Interval rad = new Interval(ival_gr.ParameterAt(r / (float)subdivs_r), ival_gr.ParameterAt((r + 1) / (float)subdivs_r));
                        Interval ang = new Interval(ival_ga.ParameterAt(a / (float)subdivs_a), ival_ga.ParameterAt((a + 1) / (float)subdivs_a));

                        int cnt = (int)Math.Ceiling(segments_in_whole_circle * ang.Length / Math.PI * 2);
                        Polyline pcrv = new Polyline();
                        pcrv.AddRange(FakeArc(plane, rad.T0, ang.T0, ang.T1, cnt));
                        pcrv.AddRange(FakeArc(plane, rad.T1, ang.T1, ang.T0, cnt));
                        pcrv.Add(pcrv[0]);

                        Grasshopper.Kernel.Types.GH_Curve gh_curve = new Grasshopper.Kernel.Types.GH_Curve();
                        Grasshopper.Kernel.GH_Convert.ToGHCurve(pcrv, GH_Conversion.Both, ref gh_curve);
                        regions.Append(gh_curve, new Grasshopper.Kernel.Data.GH_Path(new int[] { r, a }));
                    }


                DA.SetDataList(0, hours);
                DA.SetDataList(1, points);
                DA.SetDataTree(2, regions);

            }
        }

        private static Point3d PointByCylCoords(double r, double t) {
            return new Point3d(r * Math.Cos(t), r * Math.Sin(t), 0);
        }

        private static Point3d[] FakeArc(Plane plane, double r, double t0, double t1, int count) {
            Point3d[] parr = new Point3d[count];
            double step = ((t1 - t0) / (count - 1));
            for (int n = 0; n < count; n++) {
                double t = t0 + n * step;
                Point3d pt = PointByCylCoords(r, t);
                parr[n] = plane.PointAt(pt.X, pt.Y);

            }
            return parr;
        }

    }


    public class Dhr_RadialTimeValueGraphComponent : GH_Component {
        public Dhr_RadialTimeValueGraphComponent()
            //Call the base constructor
            : base("Radial Time-Value Spatialization", "RadialTimeVal", "Assigns a position on a Radial Time-Value Graph (like a clock graph) for each hour given.", "DYear", "Spatialize") { }
        public override Grasshopper.Kernel.GH_Exposure Exposure { get { return GH_Exposure.tertiary; } }
        public override Guid ComponentGuid { get { return new Guid("{64575900-973A-4FD3-83D6-8D1DFAF51573}"); } }
        protected override Bitmap Icon { get { return DYear.Properties.Resources.Olgay; } }

        public CType cycle_type;
        public enum CType { Year, Day, None, Invalid };

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager) {
            pManager.RegisterParam(new GHParam_DHr(), "DHours", "Dhrs", "The Dhours to which to assign positions", GH_ParamAccess.list);
            pManager.Register_StringParam("Key", "Key", "The key associated with the radial dimension of the graph.", GH_ParamAccess.item);
            pManager.Register_IntervalParam("Radius Scale", "SclR", "An interval that associates hour values with the graph radius, which, in effect, sets the radial scale of the graph.  Defaults to the Max and Min of given values.", GH_ParamAccess.item);
            pManager.Register_StringParam("Period", "Prd", "The time period associated with one revolution around the graph.  Choose 'year', 'day', or 'none' (default).  The default setting of 'none' results in a graph that plots hours in the order in which they were given, ignoring their timestamp, and filling the 360deg of the circle.", "none", GH_ParamAccess.item);
            pManager.Register_PlaneParam("Location", "Loc", "The location and orientation (as a plane) to draw this graph.  Note an angular value of zero will align with the x-axis of this plane.", new Plane(new Point3d(0, 0, 0), new Vector3d(0, 0, 1)), GH_ParamAccess.item);
            pManager.Register_IntervalParam("Graph Radii", "Rad", "An interval that sets the inner and outer radii of the resulting graph.  Defaults to 1.0->3.0", new Interval(1.0, 3.0), GH_ParamAccess.item);

            this.Params.Input[2].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager) {
            pManager.RegisterParam(new GHParam_DHr(), "DHours", "Dhrs", "The spatialized Dhours", GH_ParamAccess.tree);
            pManager.Register_PointParam("Positions", "Pts", "The assigned positions of the hours", GH_ParamAccess.tree);
            pManager.Register_MeshParam("Mesh", "Msh", "A mesh representing the resulting graph, with vertex colors assigned where applicable.", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA) {
            List<DHr> hours = new List<DHr>();
            String key = "";
            if (DA.GetDataList(0, hours) && DA.GetData(1, ref key)) {
                if (hours.Count == 0) {
                    //TODO: we may want to return null values here instead.
                    return;
                }
                Interval ival_r = new Interval();
                float[] vals = new float[0];

                if (!(DA.GetData(2, ref ival_r))) DHr.get_domain(key, hours.ToArray(), ref vals, ref ival_r);
                else
                {
                    vals = new float[hours.Count];
                    for (int h = 0; h < hours.Count; h++) vals[h] = hours[h].val(key);
                    /* FROM HEAD:
                    ival_r = new Interval(hours[0].val(key), hours[0].val(key));
                    vals = new float[hours.Count];
                    for (int h = 0; h < hours.Count; h++) {
                        vals[h] = hours[h].val(key);
                        if (vals[h] < ival_r.T0) ival_r.T0 = vals[h];
                        if (vals[h] > ival_r.T1) ival_r.T1 = vals[h];
                    }
                     */
                }

                String period_string = "none";
                cycle_type = CType.Invalid;
                DA.GetData(3, ref period_string);

                if (period_string.Contains("year")) { this.cycle_type = CType.Year; }
                else if (period_string.Contains("day") || period_string.Contains("daily")) { this.cycle_type = CType.Day; }
                else if (period_string.Contains("none")) { this.cycle_type = CType.None; }
                if (cycle_type == CType.Invalid)
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "I don't understand the time period you're looking for.\nPlease choose 'year', 'day', or 'none'.");
                    return;
                }

                Plane plane = new Plane(new Point3d(0, 0, 0), new Vector3d(0, 0, 1));
                DA.GetData(4, ref plane);

                Interval ival_gr = new Interval();
                Interval ival_ga = new Interval(Math.PI * 2, 0); // reversed interval to ensure clockwise direction
                DA.GetData(5, ref ival_gr);



                switch (this.cycle_type) {

                    case CType.None: {
                            List<Point3d> points = new List<Point3d>();
                            Rhino.Geometry.Mesh mesh = new Mesh();
                            for (int h = 0; h < hours.Count; h++) {
                                double radius = ival_gr.ParameterAt(ival_r.NormalizedParameterAt(vals[h]));
                                double theta = ival_ga.ParameterAt(h / (double)hours.Count);
                                Point3d gpt = PointByCylCoords(radius, theta); // a point in graph coordinates
                                hours[h].pos = gpt; // the hour records the point in graph coordinates

                                Point3d wpt = plane.PointAt(gpt.X, gpt.Y);
                                points.Add(wpt);  // adds this point in world coordinates

                                mesh.Vertices.Add(wpt);
                                mesh.VertexColors.Add(hours[h].color);
                                Point3d wbpt = PointByCylCoords(ival_gr.ParameterAt(-0.01), theta);
                                mesh.Vertices.Add(plane.PointAt(wbpt.X, wbpt.Y));
                                mesh.VertexColors.Add(hours[h].color);
                                if (h > 0) mesh.Faces.AddFace(h * 2, h * 2 + 1, h * 2 - 1);
                                if (h > 0) mesh.Faces.AddFace(h * 2 - 1, h * 2 - 2, h * 2);
                            }
                            mesh.Normals.ComputeNormals();
                            mesh.Compact();

                            DA.SetDataList(0, hours);
                            DA.SetDataList(1, points);
                            //DA.SetDataTree(2, regions);
                            DA.SetData(2, mesh);
                        }
                        break;

                    case CType.Day: {
                            Grasshopper.Kernel.Data.GH_Structure<Grasshopper.Kernel.Types.GH_Point> points = new Grasshopper.Kernel.Data.GH_Structure<Grasshopper.Kernel.Types.GH_Point>();
                            Grasshopper.Kernel.Data.GH_Structure<DHr> hourTreeOut = new Grasshopper.Kernel.Data.GH_Structure<DHr>();
                            List<Mesh> meshes = new List<Mesh>();
                            Rhino.Geometry.Mesh mesh = new Mesh();
                            int hour_of_day = 0;
                            int cycle_count = 0;
                            for (int i = 0; i < hours.Count; i++) {
                                if (hours[i].hr % 24 != hour_of_day) {
                                    cycle_count++;
                                    hour_of_day = hours[i].hr % 24;
                                    mesh.Normals.ComputeNormals();
                                    mesh.Compact();
                                    meshes.Add(mesh);
                                    mesh = new Mesh();
                                }
                                double radius = ival_gr.ParameterAt(ival_r.NormalizedParameterAt(vals[i]));
                                double theta = ival_ga.ParameterAt(hour_of_day / 24.0);
                                Point3d gpt = PointByCylCoords(radius, theta); // a point in graph coordinates
                                hours[i].pos = gpt; // the hour records the point in graph coordinates

                                Point3d wpt = plane.PointAt(gpt.X, gpt.Y);

                                points.Append(new Grasshopper.Kernel.Types.GH_Point(wpt),new Grasshopper.Kernel.Data.GH_Path(cycle_count)); // adds this point in world coordinates
                                hourTreeOut.Append(hours[i], new Grasshopper.Kernel.Data.GH_Path(cycle_count));


                                mesh.Vertices.Add(wpt);
                                mesh.VertexColors.Add(hours[i].color);
                                Point3d wbpt = PointByCylCoords(ival_gr.ParameterAt(-0.01), theta);
                                mesh.Vertices.Add(plane.PointAt(wbpt.X, wbpt.Y));
                                mesh.VertexColors.Add(hours[i].color);
                                if (hour_of_day > 0) mesh.Faces.AddFace(hour_of_day * 2, hour_of_day * 2 + 1, hour_of_day * 2 - 1);
                                if (hour_of_day > 0) mesh.Faces.AddFace(hour_of_day * 2 - 1, hour_of_day * 2 - 2, hour_of_day * 2);

                                hour_of_day++;
                            }

                            DA.SetDataTree(0, hourTreeOut);
                            DA.SetDataTree(1, points);
                            //DA.SetDataTree(2, regions);
                            DA.SetDataList(2, meshes);
                        }
                        break;
                }








            }
        }

        private static Point3d PointByCylCoords(double r, double t) {
            return new Point3d(r * Math.Cos(t), r * Math.Sin(t), 0);
        }

        private static Point3d[] FakeArc(Plane plane, double r, double t0, double t1, int count) {
            Point3d[] parr = new Point3d[count];
            double step = ((t1 - t0) / (count - 1));
            for (int n = 0; n < count; n++) {
                double t = t0 + n * step;
                Point3d pt = PointByCylCoords(r, t);
                parr[n] = plane.PointAt(pt.X, pt.Y);

            }
            return parr;
        }

    }



    public class Dhr_PieGraphComponent : GH_Component {
        public Dhr_PieGraphComponent()
            //Call the base constructor
            : base("Pie Graph", "PieGraph", "Plots a Pie Graph based on a given list of values.", "DYear", "Spatialize") { }
        public override Grasshopper.Kernel.GH_Exposure Exposure { get { return GH_Exposure.tertiary; } }
        public override Guid ComponentGuid { get { return new Guid("{F2B7D8F6-DE7D-44C3-99AC-9222D910E5C8}"); } }
        protected override Bitmap Icon { get { return DYear.Properties.Resources.Olgay; } }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager) {
            pManager.Register_DoubleParam("Values", "Vals", "A list of values, each associated with a slice of the Pie Graph.  The sum of these values will be used to set the 'scale' of the graph.", GH_ParamAccess.list);
            pManager.Register_ColourParam("Colors", "Clrs", "A list of colors to be associated with each slice of the Pie Graph.", GH_ParamAccess.list);
            pManager.Register_PlaneParam("Location", "Loc", "The location and orientation (as a plane) to draw this graph", new Plane(new Point3d(0, 0, 0), new Vector3d(0, 0, 1)), GH_ParamAccess.item);
            pManager.Register_IntervalParam("Graph Radii", "Rad", "An interval that sets the inner and outer radii of the resulting graph.  Defaults to 1.0->3.0", new Interval(1.0, 3.0), GH_ParamAccess.item);

            this.Params.Input[1].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager) {
            pManager.Register_CurveParam("Regions", "Rgns", "Regions that represent each slice on the resulting Pie Graph.", GH_ParamAccess.list);
            pManager.Register_MeshParam("Mesh", "Msh", "Meshes that represent the resulting graph, with vertex colors assigned where applicable.", GH_ParamAccess.list);
            pManager.Register_ColourParam("Colors", "Clrs", "Colors corresponding to each region", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA) {
            List<double> values = new List<double>();

            if (DA.GetDataList(0, values)) {
                bool has_colors = false;
                List<Color> colors = new List<Color>();
                has_colors = DA.GetDataList(1, colors);
                if ((has_colors) && (colors.Count != values.Count)) {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "If you're going to pass in colors, please pass in a list of colors that is the same length as the list of values you gave me.");
                    return;
                }
                Plane plane = new Plane(new Point3d(0, 0, 0), new Vector3d(0, 0, 1));
                DA.GetData(2, ref plane);

                Interval ival_gr = new Interval();
                //Interval ival_ga = new Interval(0, Math.PI * 2);
                DA.GetData(3, ref ival_gr);
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "You've set your inner or outer radius to 0.  Invalid curves will result.");

                //Interval ival_va = new Interval(0, values.Sum());

                double sum = values.Sum();
                int segments_in_whole_circle = 36;
                Interval ival_angle = new Interval(0, 0);
                List<Grasshopper.Kernel.Types.GH_Curve> regions = new List<Grasshopper.Kernel.Types.GH_Curve>();
                List<Color> colors_out = new List<Color>();
                List<Mesh> meshes = new List<Mesh>();

                for (int n = 0; n < values.Count; n++) {
                    if (values[n] == 0) continue;
                    ival_angle.T1 = ival_angle.T0 + (Math.PI * 2 / sum * values[n]);

                    int cnt = Math.Max(4, (int)Math.Ceiling(segments_in_whole_circle * ival_angle.Length / Math.PI * 2));
                    Polyline pcrv = new Polyline();
                    pcrv.AddRange(FakeArc(plane, ival_gr.T0, ival_angle.T0, ival_angle.T1, cnt));
                    pcrv.AddRange(FakeArc(plane, ival_gr.T1, ival_angle.T1, ival_angle.T0, cnt));
                    pcrv.Add(pcrv[0]);

                    Grasshopper.Kernel.Types.GH_Curve gh_curve = new Grasshopper.Kernel.Types.GH_Curve();
                    Grasshopper.Kernel.GH_Convert.ToGHCurve(pcrv, GH_Conversion.Both, ref gh_curve);
                    regions.Add(gh_curve);
                    colors_out.Add(colors[n]);

                    Rhino.Geometry.Mesh mesh = new Mesh();
                    foreach (Point3d pt in FakeArc(plane, ival_gr.T0, ival_angle.T0, ival_angle.T1, cnt)) {
                        mesh.Vertices.Add(pt);
                        if (has_colors) mesh.VertexColors.Add(colors[n]);
                    }
                    foreach (Point3d pt in FakeArc(plane, ival_gr.T1, ival_angle.T0, ival_angle.T1, cnt)) {
                        mesh.Vertices.Add(pt);
                        if (has_colors) mesh.VertexColors.Add(colors[n]);
                    }
                    for (int i = 0; i < cnt - 1; i++) {
                        mesh.Faces.AddFace(i, i + 1, i + cnt);
                        mesh.Faces.AddFace(i + 1, i + cnt + 1, i + cnt);
                    }
                    mesh.Normals.ComputeNormals();
                    mesh.Compact();
                    meshes.Add(mesh);

                    ival_angle.T0 = ival_angle.T1;
                }


                DA.SetDataList(0, regions);
                DA.SetDataList(1, meshes);
                DA.SetDataList(2, colors_out);
            }
        }

        private static Point3d PointByCylCoords(double r, double t) {
            return new Point3d(r * Math.Cos(t), r * Math.Sin(t), 0);
        }

        private static Point3d[] FakeArc(Plane plane, double r, double t0, double t1, int count) {
            Point3d[] parr = new Point3d[count];
            double step = ((t1 - t0) / (count - 1));
            for (int n = 0; n < count; n++) {
                double t = t0 + n * step;
                Point3d pt = PointByCylCoords(r, t);
                parr[n] = plane.PointAt(pt.X, pt.Y);

            }
            return parr;
        }
    }




}
