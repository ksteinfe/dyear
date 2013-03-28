using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Text.RegularExpressions;
using System.Text;
using System.IO;

namespace DYear {
    public class MDHr {
        internal Dictionary<string, float> m_vals;
        internal int m_hr; //hour of year
        public Point3d pos;
        public Color color;
        public bool is_surrogate; // does this hour stand in for a collection of hours?  set to 'true' when returning averaged data.
        public static float INVALID_VAL = 99999.99f;

        public MDHr() {
            m_vals = new Dictionary<string, float>();
            m_hr = -1;
            pos = new Point3d();
            color = Color.FromArgb(0, 0, 0);
            is_surrogate = false;
        }
        public MDHr(MDHr t) {
            this.m_hr = t.m_hr;
            this.m_vals = new Dictionary<string, float>(t.m_vals);
            pos = new Point3d(t.pos.X, t.pos.Y, t.pos.Z);
            color = Color.FromArgb(t.color.A, t.color.R, t.color.G, t.color.B);
            is_surrogate = t.is_surrogate;
        }
        public MDHr(int hr)
            : this() {
            if ((hr >= 0) && (hr < 8760)) { m_hr = hr; } else { m_hr = -1; }
            this.m_vals = new Dictionary<string, float>();
            pos = new Point3d();
            color = Color.FromArgb(0, 0, 0);
            is_surrogate = false;
        }
        public DateTime dt {
            get { return Util.datetimeFromHourOfYear(this.m_hr); }
            set { this.m_hr = Util.hourOfYearFromDatetime(value); }
        }

        public string[] keys {
            get { return new List<string>(m_vals.Keys).ToArray(); }
        }

        public static string[] commonkeys(MDHr[] hours) {
            List<string> allKeys = new List<string>();
            foreach (MDHr hr in hours) { foreach (string key in hr.keys) { if (!allKeys.Contains(key)) { allKeys.Add(key); } } }
            List<string> cmnKeys = new List<string>();
            foreach (string key in allKeys) {
                bool isCommon = true;
                foreach (MDHr hr in hours) {
                    if (!hr.m_vals.ContainsKey(key)) { isCommon = false; break; }
                }
                if (isCommon) { cmnKeys.Add(key); }
            }
            return cmnKeys.ToArray();
        }


    }

    public class DHr : GH_Goo<MDHr>, IComparable<DHr> {
        public DHr() : base() { this.Value = new MDHr(); }
        public DHr(MDHr instance) { this.Value = instance; }
        public DHr(DHr instance) { this.Value = new MDHr(instance.Value); }
        public override IGH_Goo Duplicate() { return new DHr(this); }


        public DHr(int hr) { this.Value = new MDHr(hr); }

        public static string cleankey(string key) {
            key = Regex.Replace(key, @"\s+", " "); // collapse multiple spaces
            key = Regex.Replace(key, @"[^\w\.@:\[\] -]", ""); // only allow normal word chars, brackets, dashes, spaces
            return key;
        }


        #region // GETTERS AND SETTERS

        public int hr {
            get { return Value.m_hr; }
            set { Value.m_hr = value; }
        }
        public DateTime dt {
            get { return Value.dt; }
            set { Value.dt = value; }
        }
        public Point3d pos {
            get { return Value.pos; }
            set { Value.pos = value; }
        }
        public float pos_x {
            get { return (float)(Value.pos.X); }
            set { Value.pos.X = value; }
        }
        public float pos_y {
            get { return (float)(Value.pos.Y); }
            set { Value.pos.Y = value; }
        }
        public float pos_z {
            get { return (float)(Value.pos.Z); }
            set { Value.pos.Z = value; }
        }

        public Color color {
            get { return Value.color; }
            set { Value.color = value; }
        }

        public bool is_surrogate {
            get { return Value.is_surrogate; }
            set { Value.is_surrogate = value; }
        }

        public float val(string key) { return Value.m_vals[key]; }
        public void put(string key, float val) { Value.m_vals[DHr.cleankey(key)] = val; }
        public void put_plus(string key, float val) { Value.m_vals[key] = Value.m_vals[key] + val; }
        public void put_mult(string key, float val) { Value.m_vals[key] = Value.m_vals[key] * val; }
        public void put_div(string key, float val) { Value.m_vals[key] = Value.m_vals[key] / val; }

        public string[] keys { get { return Value.keys; } }
        public float[] values {
            get {
                float[] ret = new float[keys.Length];
                for (int k = 0; k < keys.Length; k++) ret[k] = val(keys[k]);
                return ret;
            }
        }
        public string[] values_as_strings() {
            float[] floats = values;
            string[] ret = new string[floats.Length];
            for (int n = 0; n < floats.Length; n++) ret[n] = floats[n].ToString();
            return ret;
        }

        public static bool get_domain(string key, DHr[] dhrs, ref float[] vals, ref Interval domain) {
            key = cleankey(key);
            if (dhrs.Length == 0) return false;
            foreach (DHr hr in dhrs) if (!hr.containsKey(key)) return false;

            vals = new float[dhrs.Length];
            double max = dhrs[0].val(key);
            double min = dhrs[0].val(key);
            for (int h = 0; h < dhrs.Length; h++) {
                float val = dhrs[h].val(key);
                vals[h] = val;
                if (val > max) max = val;
                if (val < min) min = val;
            }
            domain = new Interval(min, max);

            return true;
        }


        public bool containsKey(string key) { return Value.m_vals.ContainsKey(key); }

        public void clear() { this.Value.m_vals.Clear(); }

        public static string[] commonkeys(DHr[] hours) {
            List<MDHr> mhours = new List<MDHr>();
            foreach (DHr hour in hours) { mhours.Add(hour.Value); }
            return MDHr.commonkeys(mhours.ToArray());
        }

        public int day_of_year { get { return dt.DayOfYear - 1; } }

        #endregion

        #region // GH STUFF

        public override bool IsValid {
            get {
                // hour is set to -1 if hour unset.  this may occur when producing "stand-in" hours for averaging or whatever.
                // hour is set to -999 when an error occurs
                return ((Value.m_hr >= -1) && (Value.m_hr <= 8760));
            }
        }
        public override object ScriptVariable() { return new DHr(this); }
        public override string ToString() {
            string datestring = this.dt.ToString("MMMdd h") + this.dt.ToString(" t").ToLowerInvariant().Trim();
            if (is_surrogate) datestring = "SURROGATE @ " + datestring;
            return string.Format("[{1} {2} keys] {0}", datestring, hr.ToString("0000"), keys.Length);
        }
        public override string TypeDescription { get { return "Represents a Data Hour"; } }
        public override string TypeName { get { return "Data Hour"; } }


        public override bool Write(GH_IO.Serialization.GH_IWriter writer) {
            writer.SetString("hr", hr.ToString());

            writer.SetString("color_r", color.R.ToString());
            writer.SetString("color_g", color.G.ToString());
            writer.SetString("color_b", color.B.ToString());
            writer.SetString("color_a", color.A.ToString());

            writer.SetString("pos_x", pos_x.ToString());
            writer.SetString("pos_y", pos_y.ToString());
            writer.SetString("pos_z", pos_z.ToString());

            writer.SetString("is_surrogate", is_surrogate.ToString());

            writer.SetString("keys", string.Join(",", keys));
            writer.SetString("values", string.Join(",", values_as_strings()));
            return base.Write(writer);
        }
        public override bool Read(GH_IO.Serialization.GH_IReader reader) {
            int h = ReadInt(reader, "hr"); if (h == -99999) return false; else hr = h;

            bool color_good = true;
            int cr = ReadInt(reader, "color_r"); if ((cr < 0)||(cr>255)) color_good =  false;
            int cg = ReadInt(reader, "color_g"); if ((cg < 0) || (cg > 255)) color_good = false;
            int cb = ReadInt(reader, "color_b"); if ((cb < 0) || (cb > 255)) color_good = false;
            int ca = ReadInt(reader, "color_a"); if ((ca < 0) || (ca > 255)) color_good = false;
            if (color_good) color = Color.FromArgb(ca, cr, cg, cb);

            bool point_good = true;
            double px = ReadDouble(reader, "pos_x"); if (px == -999.99) point_good = false;
            double py = ReadDouble(reader, "pos_y"); if (py == -999.99) point_good = false;
            double pz = ReadDouble(reader, "pos_z"); if (pz == -999.99) point_good = false;
            if (point_good) pos = new Point3d(px, py, pz);

            string keystring = "";
            string valstring = "";
            if (!reader.TryGetString("keys", ref keystring) || !reader.TryGetString("values", ref valstring)) return false;
            try {
                string[] keys = keystring.Split(',');
                string[] valstrings = valstring.Split(',');
                if (keys.Length == 0 || valstrings.Length == 0) return false;
                for (int i = 0; i < keys.Length; i++) put(keys[i], float.Parse(valstrings[i]));
            } catch {
                return false;
            }
            return base.Read(reader);
        }

        private static int ReadInt(GH_IO.Serialization.GH_IReader reader, string key) {
            string str = "";
            int ret = -99999;
            if (!reader.TryGetString(key, ref str)) return -99999;
            if (!int.TryParse(str, out ret)) return -99999;
            return ret;
        }

        private static double ReadDouble(GH_IO.Serialization.GH_IReader reader, string key) {
            string str = "";
            double ret = -999.99;
            if (!reader.TryGetString(key, ref str)) return -999.99;
            if (!double.TryParse(str, out ret)) return -999.99;
            return ret;
        }

        #endregion

        public static string BuildCSVString(List<DHr> hours) {
            StringBuilder sb = new StringBuilder();
            string delimiter = ",";
            List<string> keys = new List<string>(new string[] { "hour", "datetime" });
            keys.AddRange(DHr.commonkeys(hours.ToArray()));
            sb.AppendLine(string.Join(delimiter, keys.ToArray()));
            foreach (DHr hr in hours) {
                string[] vals = new string[keys.Count];
                vals[0] = hr.hr.ToString();
                vals[1] = hr.dt.ToString("MM/dd H:00");
                for (int k = 2; k < keys.Count; k++) vals[k] = hr.val(keys[k]).ToString();
                sb.AppendLine(string.Join(delimiter, vals));
            }
            return sb.ToString();
        }

        public int CompareTo(DHr other) { return this.hr.CompareTo(other.hr); }
    }

    public class GHParam_DHr : GH_PersistentParam<DHr> {
        public int hours_vol;
        public List<string> keys_vol;
        public List<string> okeys_vol;
        public bool contains_surrogates;

        public bool has_persistent_data {
            get { return this.PersistentDataCount > 0; }
        }

        public bool has_source_data {
            get { return this.SourceCount > 0; }
        }

        public GHParam_DHr()
            : base(new GH_InstanceDescription("Data Hour", "Dhr", "Represents a collection of Data Hours", "DYear", "Primitive")) {
            updateLocals();
        }
        public override System.Guid ComponentGuid { get { return new Guid("{1573577D-7B23-46C4-803D-594ECE47BA10}"); } }
        protected override Bitmap Icon { get { return DYear.Properties.Resources.Icons_Param_Dhr; } }
        public override Grasshopper.Kernel.GH_Exposure Exposure { get { return GH_Exposure.primary; } }


        public override void CreateAttributes() { m_attributes = new GHParam_DHr_Attributes(this); }

        protected override void CollectVolatileData_FromSources() {
            base.CollectVolatileData_FromSources();
            updateLocals();
        }

        private void updateLocals() {
            int n = 0;
            hours_vol = 0;
            keys_vol = new List<string>();
            okeys_vol = new List<string>();
            contains_surrogates = false;

            List<string> allKeys = new List<string>();
            List<DHr> data = ContainedHrs();
            foreach (DHr hr in data) {
                if (hr.IsValid) {
                    foreach (string key in hr.keys) { if (!allKeys.Contains(key)) { allKeys.Add(key); } }
                    hours_vol++;
                    if (hr.is_surrogate) contains_surrogates = true;
                } else {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Invalid Dhr found at index" + n);
                }
                n++;
            }

            foreach (string key in allKeys) {
                bool isCommon = true;
                foreach (DHr hr in data) {
                    if (!hr.Value.m_vals.ContainsKey(key)) { isCommon = false; break; }
                }
                if (isCommon) { keys_vol.Add(key); } else {
                    okeys_vol.Add(key);
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "An hour in this collection contains the unique key '" + key + "'.");
                }
            }
            keys_vol.Sort();
            okeys_vol.Sort();
        }

        public List<DHr> ContainedHrs() {
            Grasshopper.Kernel.Data.GH_Structure<DHr> tree = PersistentData;
            if (has_source_data) tree = this.m_data;
            List<DHr> data = new List<DHr>();
            foreach (var v in tree.AllData(true)) data.Add((DHr)v);
            return data;
        }

        public override void AppendAdditionalMenuItems(ToolStripDropDown menu) {
            //base.AppendAdditionalMenuItems(menu);

            Menu_AppendFlattenParameter(menu);

            Menu_AppendWireDisplay(menu);
            Menu_AppendDisconnectWires(menu);
            Menu_AppendSeparator(menu);
            Menu_AppendDestroyPersistent(menu);
            Menu_AppendInternaliseData(menu);
            Menu_AppendExtractParameter(menu);

            if (hours_vol > 0) {
                Menu_AppendSeparator(menu);
                Menu_AppendItem(menu, "Export Hours to CSV", Menu_ExportHoursClicked);
            }

            if (keys_vol.Count > 0) {
                Menu_AppendSeparator(menu);
                Menu_AppendItem(menu, "-- KEYS -- (click below to copy to clipboard)");
                foreach (string key in keys_vol) Menu_AppendItem(menu, key, Menu_KeyItemClicked);
            }
        }

        private void Menu_ExportHoursClicked(Object sender, EventArgs e) {
            SaveFileDialog sDialog = new SaveFileDialog();
            sDialog.Filter = "csv|*.csv";
            if (sDialog.ShowDialog() == DialogResult.OK) {
                string csvString = DHr.BuildCSVString(ContainedHrs());
                File.WriteAllText(sDialog.FileName, csvString);
            }
        }

        private void Menu_KeyItemClicked(Object sender, EventArgs e) {
            System.Windows.Forms.ToolStripMenuItem ti = (System.Windows.Forms.ToolStripMenuItem)(sender);
            System.Windows.Forms.Clipboard.SetText(ti.Text);
        }

        public override void AddedToDocument(GH_Document document) {
            base.AddedToDocument(document);
            updateLocals();
        }
        protected override void OnVolatileDataCollected() {
            base.OnVolatileDataCollected();
            updateLocals();
        }

        protected override GH_GetterResult Prompt_Singular(ref DHr value) {
            return GH_GetterResult.cancel;
        }
        protected override GH_GetterResult Prompt_Plural(ref List<DHr> values) {
            return GH_GetterResult.cancel;
        }


    }

    public class GHParam_DHr_Attributes : GH_Attributes<GHParam_DHr> {
        public GHParam_DHr_Attributes(GHParam_DHr owner) : base(owner) { }

        int xtra_height = 17;

        protected override void Layout() {
            // Compute the width of the NickName of the owner (plus some extra padding), 
            // then make sure we have at least 100 pixels.
            int width = GH_FontServer.StringWidth(Owner.NickName, GH_FontServer.Standard);
            width = Math.Max(width + 10, 100);

            // The height of our object is always 100
            int height = 50;
            if (Owner.has_persistent_data) height = height + xtra_height;
            if (Owner.contains_surrogates) height = height + xtra_height;

            // Assign the width and height to the Bounds property.
            // Also, make sure the Bounds are anchored to the Pivot
            Bounds = new RectangleF(Pivot, new SizeF(width, height));
        }
        public override void ExpireLayout() {
            base.ExpireLayout();
            // Destroy any data you have that becomes 
            // invalid when the layout expires. 
        }

        protected override void Render(GH_Canvas canvas, Graphics graphics, GH_CanvasChannel channel) {
            // Render all the wires that connect the Owner to all its Sources.
            if (channel == GH_CanvasChannel.Wires) {
                RenderIncomingWires(canvas.Painter, Owner.Sources, Owner.WireDisplay);
                return;
            }
            // Render the parameter capsule and any additional text on top of it.
            if (channel == GH_CanvasChannel.Objects) {
                // Define the default palette.
                GH_Palette palette = GH_Palette.Normal;
                

                // Adjust palette based on the Owner's worst case messaging level.
                switch (Owner.RuntimeMessageLevel) {
                    case GH_RuntimeMessageLevel.Warning:
                        palette = GH_Palette.Warning;
                        break;

                    case GH_RuntimeMessageLevel.Error:
                        palette = GH_Palette.Error;
                        break;
                }

                // Create a new Capsule without text or icon.
                RectangleF capBounds = new RectangleF(Pivot, new SizeF(Bounds.Width, 50));
                if (Owner.contains_surrogates) capBounds = new RectangleF(new PointF(Pivot.X, Pivot.Y + xtra_height), new SizeF(Bounds.Width, 50));
                GH_Capsule capsule = GH_Capsule.CreateCapsule(capBounds, palette);

                capsule = GH_Capsule.CreateCapsule(capBounds, palette);

                capsule.AddInputGrip(this.InputGrip.Y);
                capsule.AddOutputGrip(this.OutputGrip.Y);

                // Render the capsule using the current Selection, Locked and Hidden states.
                // DYr parameters are always hidden since they cannot be drawn in the rhino viewport.
                capsule.Render(graphics, Selected, Owner.Locked, true);
                capsule.Dispose(); // Always dispose of a GH_Capsule when you're done with it.
                capsule = null;

                // Now it's time to draw the text on top of the capsule.
                // First we'll draw the Owner NickName using a standard font and a black brush.
                // We'll also align the NickName in the center of the Bounds.
                StringFormat format = new StringFormat();
                format.Alignment = StringAlignment.Center;
                format.LineAlignment = StringAlignment.Center;
                format.Trimming = StringTrimming.EllipsisCharacter;


                // Our entire capsule is 60 pixels high, and we'll draw 
                // three lines of text, each 20 pixels high.
                RectangleF textRectangle = capBounds;
                textRectangle.Height = 15;

                // Draw the NickName in a Standard Grasshopper font.
                graphics.DrawString(Owner.NickName, GH_FontServer.StandardBold, Brushes.Black, textRectangle, format);


                // Now we need to draw the median and mean information.
                // Adjust the formatting and the layout rectangle.
                textRectangle.Inflate(-5, 0);
                format.Alignment = StringAlignment.Near;

                textRectangle.Y += 20;
                graphics.DrawString(String.Format("{0} Hours", Owner.hours_vol), GH_FontServer.Standard, Brushes.Black, textRectangle, format);

                textRectangle.Y += 15;
                graphics.DrawString(String.Format("{0} Keys", Owner.keys_vol.Count), GH_FontServer.Standard, Brushes.Black, textRectangle, format);

                if (Owner.has_persistent_data) {

                    RectangleF xtraBounds = new RectangleF(new PointF(capBounds.Location.X, capBounds.Location.Y + 52), new SizeF(Bounds.Width, 15));
                    RectangleF xTextRectangle = xtraBounds;

                    palette = GH_Palette.Black;
                    if (Owner.has_source_data) palette = GH_Palette.Grey;

                    GH_Capsule tcap = GH_Capsule.CreateTextCapsule(xtraBounds, xTextRectangle, palette, "INTERNAL DHRS");
                    tcap.Font = GH_FontServer.Small;

                    tcap.Render(graphics, Selected, Owner.Locked, true);
                    tcap.Dispose(); // Always dispose of a GH_Capsule when you're done with it.
                    tcap = null;
                }

                if (Owner.contains_surrogates) {

                    RectangleF xtraBounds;
                    //if (Owner.has_persistent_data) xtraBounds = new RectangleF(new PointF(capBounds.Location.X, capBounds.Location.Y + 67), new SizeF(Bounds.Width, 15));
                    //else xtraBounds = new RectangleF(new PointF(capBounds.Location.X, capBounds.Location.Y + 52), new SizeF(Bounds.Width, 15));
                    xtraBounds = new RectangleF(new PointF(capBounds.Location.X, capBounds.Location.Y - 17), new SizeF(Bounds.Width, 15)); 
                    RectangleF xTextRectangle = xtraBounds;

                    palette = GH_Palette.Transparent;
                    GH_Capsule tcap = GH_Capsule.CreateTextCapsule(xtraBounds, xTextRectangle, palette, "SURROGATE DHRS");
                    tcap.Font = GH_FontServer.Small;

                    tcap.Render(graphics, Selected, Owner.Locked, true);
                    tcap.Dispose(); // Always dispose of a GH_Capsule when you're done with it.
                    tcap = null;
                }
                // Always dispose of any GDI+ object that implement IDisposable.
                format.Dispose();
            }
        }


    }

    public class Dhr_MakeHourComponent : GH_Component {
        public Dhr_MakeHourComponent()
            //Call the base constructor
            : base("Construct Hour", "Dhour", "Constructs a Dhour out of its constituent parts", "DYear", "Primitive") { }

        public override Guid ComponentGuid { get { return new Guid("{39DD748B-79FE-4B9E-9108-32ACF9751688}"); } }
        protected override Bitmap Icon { get { return DYear.Properties.Resources.Olgay; } }
        public override Grasshopper.Kernel.GH_Exposure Exposure { get { return GH_Exposure.secondary; } }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager) {
            pManager.Register_IntegerParam("Hour Number", "Hr", "The hour of the year to construct, =between 0 and 8759.  Defaults to -1, which produces an invalid Dhour.", -1, GH_ParamAccess.list);
            pManager.Register_StringParam("Keys", "Keys", "The named keys to store in this Dhour. Must be a list of equal length to the 'Vals' parameter", GH_ParamAccess.list);
            pManager.Register_DoubleParam("Values", "Vals", "The values to store in this Dhour.  Must be a list of equal length to the 'Keys' parameter", GH_ParamAccess.tree);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager) {
            pManager.RegisterParam(new GHParam_DHr(), "Dhour", "Dhour", "The resulting Dhour.", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA) {
            List<int> hrs = new List<int>();
            List<string> keys = new List<string>();
            Grasshopper.Kernel.Data.GH_Structure<Grasshopper.Kernel.Types.GH_Number> valtree = new Grasshopper.Kernel.Data.GH_Structure<Grasshopper.Kernel.Types.GH_Number>();
            if ((DA.GetDataList(1, keys)) && (DA.GetDataTree(2, out valtree)) && (DA.GetDataList(0, hrs))) {

                if (hrs.Count != valtree.Branches.Count) this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "List matching error.  Hours and Vals must match.  If you pass in more than one hour number, then you must pass in a tree of values with one branch per hour number, and vice-versa.");
                else foreach (List<GH_Number> branch in valtree.Branches) if (keys.Count != branch.Count) this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "List matching error.  Keys and Vals must offer lists of the same length.  If you pass in a tree of values, each branch must contain a list of the same length as the list of keys.");
                        else {
                            List<DHr> hours = new List<DHr>();
                            for (int n = 0; n < valtree.Branches.Count; n++) {
                                DHr hr = new DHr(hrs[n]);
                                for (int m = 0; m < keys.Count; m++) hr.put(keys[m], (float)valtree.Branches[n][m].Value);
                                hours.Add(hr);
                            }
                            DA.SetDataList(0, hours);
                        }
            }
        }

    }




}
