using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using System.Drawing;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel.Attributes;

namespace DYear
{

    public class HourMask : GH_Goo<List<bool>>
    {
        public HourMask() : base() { this.Value = new List<bool>{}; }
        public HourMask(HourMask instance) { this.Value = new List<bool>(instance.Value); }
        public override IGH_Goo Duplicate() { return new HourMask(this); }

        public enum MaskType { Hourly, Daily, Diurnal, Invalid };

        public MaskType type
        {
            get
            {
                if (Value.Count == 8760) { return MaskType.Hourly; }
                if (Value.Count == 365) { return MaskType.Daily; }
                if (Value.Count == 24) { return MaskType.Diurnal; }
                return MaskType.Invalid;
            }
        }

        public void maskPerHour(bool[] hourlyFlags)
        {
            this.Value = new List<bool>(hourlyFlags);
        }

        public void maskByTime(DateTime da, DateTime db)
        {
            // produces a 24 value mask corresponding to hours of day to be applied to each day of the year
            DateTime d0 = new DateTime(Util.defaultYear, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddHours(da.Hour);
            DateTime d1 = new DateTime(Util.defaultYear, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddHours(db.Hour);
            
            this.Value = new List<bool> { };
            if (d1 > d0)
            {
                for (int h = 0; h < 24; h++) { if ((h >= d0.Hour) && (h < d1.Hour)) { this.Value.Add(true); } else { this.Value.Add(false); } }
            }
            else
            {
                for (int h = 0; h < 24; h++) { if ((h >= d1.Hour) && (h < d0.Hour)) { this.Value.Add(false); } else { this.Value.Add(true); } }
            }
        }

        public void maskByDate(DateTime da, DateTime db)
        {
            // produces a 365 value mask corresponding to days of year to be applied to each hour of every day
            DateTime d0 = new DateTime(Util.defaultYear, da.Month, da.Day, 0, 0, 0, DateTimeKind.Utc);
            DateTime d1 = new DateTime(Util.defaultYear, db.Month, db.Day, 0, 0, 0, DateTimeKind.Utc);
            this.Value = new List<bool> { };
            if (d1 > d0)
            {
                for (int d = 0; d < 365; d++) { if ((d >= d0.DayOfYear - 1) && (d <= d1.DayOfYear - 1)) { this.Value.Add(true); } else { this.Value.Add(false); } }
            }
            else
            {
                for (int d = 0; d < 365; d++) { if ((d > d1.DayOfYear - 1) && (d < d0.DayOfYear - 1)) { this.Value.Add(false); } else { this.Value.Add(true); } }
            }
        }

        public void maskByDayOfYear(int firstDay, int lastDay)
        {
            // produces a mask by first and last days (0-364)
            // produces a 365 value mask corresponding to days of year to be applied to each hour of every day
            // inclusive of first and last days
            this.Value = new List<bool> { };
            for (int d = 0; d < 365; d++) {
                if ((d >= firstDay) && (d <= lastDay)) { this.Value.Add(true); } else { this.Value.Add(false); } 
            }
        }

        public void maskByMonthOfYear(int month)
        {
            // produces a mask by selecting a month (0-11)
            // produces a 365 value mask corresponding to days of year to be applied to each hour of every day
            if (month < 11) { this.maskByDate(new DateTime(Util.defaultYear, month + 1, 1), new DateTime(Util.defaultYear, month + 2, 1).Subtract(new TimeSpan(1, 0, 0, 0))); }
            else { this.maskByDate(new DateTime(Util.defaultYear, month + 1, 1), new DateTime(Util.defaultYear + 1, 1, 1).Subtract(new TimeSpan(1, 0, 0, 0))); }
        }

        public void maskByMonthAndHour(int month, int hour)
        {
            // produces a mask by selecting a month (0-11) and a time of day (0-23)
            // produces a 8760 value mask corresponding to days of year to be applied to each hour of every day
            // only hours falling within the selected month and occuring on the selected hour of day will pass

            bool[] flags = new bool[8760];
            for (int h = 0; h < 8760; h++)
            {
                flags[h] = false;
                DateTime dt = Util.datetimeFromHourOfYear(h);
                if ((dt.Month-1 == month)&&(dt.Hour==hour)){flags[h]=true;}
            }
            this.maskPerHour(flags);
        }


        public void maskByWeekAndHour(int week, int hour) {
            // produces a mask by selecting a week (0-51) and a time of day (0-23)
            // produces a 8760 value mask corresponding to days of year to be applied to each hour of every day
            // only hours falling within the selected week and occuring on the selected hour of day will pass

            bool[] flags = new bool[8760];
            for (int h = 0; h < 8760; h++) { flags[h] = false; }
            for (int h = (week * 7 * 24)+hour; h < ((week+1)*7 * 24)+hour; h=h+24) {
                if (h<8760) flags[h] = true;
            }
            this.maskPerHour(flags);
        }

        public void fillMask(bool b)
        {
            this.Value = new List<bool> { };
            for (int h = 0; h < 24; h++) this.Value.Add(b);
        }

        public bool eval(DHr dhr)
        {
            switch (this.type)
            {//TODO: test hourly mask
                case MaskType.Hourly:
                    return this.Value[dhr.hr];
                case MaskType.Diurnal:
                    return this.Value[dhr.hr%24];
                case MaskType.Daily:
                    return this.Value[(int)(Math.Floor(((double)(dhr.hr))/24.0))];
                default:
                    return false;

            }
        }

        public bool eval(int hr)
        {
            DHr dhr = new DHr(hr);
            return this.eval(dhr);
        }


        #region // GH STUFF

        public override bool IsValid
        {
            get
            {
                if (this.type != MaskType.Invalid) { return true; }
                return false;
            }
        }
        public override object ScriptVariable(){return new HourMask(this); }
        public override string ToString() {
            string ret = "";
            ret+= string.Format("{0} values\n", Value.Count);
            foreach (bool b in this.Value) { ret += b+" , "; }
            return ret;
        }
        public override string TypeDescription{get { return "Represents an Hour Mask"; } }
        public override string TypeName{get { return "Hour Mask"; } }

        // This function is called when Grasshopper needs to convert other data 
        // into YearMask type.
        public override bool CastFrom(object source)
        {
            //Abort immediately on bogus data.
            if (source == null) { return false; }

            string str = null;
            if (GH_Convert.ToString(source, out str, GH_Conversion.Both))
            {
                str = str.ToUpperInvariant();

                //if (str.Contains("SUMMER")) { str = "JUN 20 TO SEP 21"; }
                //if (str.Contains("FALL")) { str = "SEP 22 TO DEC 20"; }
                //if (str.Contains("AUTUMN")) { str = "SEP 22 TO DEC 20"; }
                //if (str.Contains("WINTER")) { str = "DEC 21 TO MAR 19"; }
                //if (str.Contains("SPRING")) { str = "MAR 20 TO JUN 19"; }

                if (str.Contains(" TO "))
                {
                    string[] delimiters = new string[] {" TO "};
                    string[] sstr = str.Split(delimiters,StringSplitOptions.None); 
                    DateTime dt0 = Util.baseDatetime();
                    DateTime dt1 = Util.baseDatetime();
                    if ((DateTime.TryParse(sstr[0],out dt0))&&(DateTime.TryParse(sstr[1],out dt1))){

                        if ((dt0.Hour == 0) && (dt1.Hour == 0)) { maskByDate(dt0, dt1); }
                        if ((dt0.DayOfYear == DateTime.Now.DayOfYear) && (dt1.DayOfYear == DateTime.Now.DayOfYear)) { maskByTime(dt0,dt1); }

                        return true;
                    }
                }
            }


            return false;
        }


        #endregion


    }

    public class GHParam_HourMask : GH_Param<HourMask>
    {
        public GHParam_HourMask()
            : base(new GH_InstanceDescription("Hour Mask", "HMask", "Represents an hour mask", "DYear", "Primitive"))
        { }
        public override Grasshopper.Kernel.GH_Exposure Exposure { get { return GH_Exposure.primary; } }
        public override System.Guid ComponentGuid { get { return new Guid("{E23F6D87-0815-4DC1-B7B6-BC28C1648B72}"); } }
        protected override Bitmap Icon { get { return DYear.Properties.Resources.Icons_Param_YearMask; } }
    }

    public class HourMaskDisplay : GH_Param<HourMask>
    {
        public HourMaskDisplay()
            //Call the base constructor
            : base(new GH_InstanceDescription("Display Mask", "Mask", "Displays an hour mask", "DYear", "Visualize"))
        { }
        public override Grasshopper.Kernel.GH_Exposure Exposure { get { return GH_Exposure.primary; } }
        public override Guid ComponentGuid { get { return new Guid("{5F81F476-42EA-48F2-BB32-6788D62396D1}"); } }
        protected override Bitmap Icon { get { return DYear.Properties.Resources.Olgay; } }

        public HourMask maskToDraw()
        {
            if (VolatileData.IsEmpty) { return new HourMask(); }
            HourMask mask = this.m_data[0][0];
            return mask;
        }

        public override void CreateAttributes() { m_attributes = new HourMaskDisplay_Attributes(this); }

        public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            this.Attributes.Bounds = new RectangleF(this.Attributes.Pivot, new SizeF(365, 96));
        }
    }


    public class HourMaskDisplay_Attributes : GH_ResizableAttributes<HourMaskDisplay>
    {
        public HourMaskDisplay_Attributes(HourMaskDisplay owner) : base(owner) { }

        public int paddingTop = 15;
        public int padding = 4;

        protected override Size MinimumSize { get { return new Size(365, 96); }}
        protected override Size MaximumSize { get { return new Size(730, 240); } }
        protected override Padding SizingBorders { get { return new Padding(10); } }


        protected override void Render(GH_Canvas canvas, Graphics graphics, GH_CanvasChannel channel)
        {
            // Render all the wires that connect the Owner to all its Sources.
            if (channel == GH_CanvasChannel.Wires) { RenderIncomingWires(canvas.Painter, Owner.Sources, Owner.WireDisplay); return;  }
            
            // Render the parameter capsule and any additional text on top of it.
            if (channel == GH_CanvasChannel.Objects)
            {
                // Define the default palette.
                GH_Palette palette = GH_Palette.Transparent;
                // Adjust palette based on the Owner's worst case messaging level.
                switch (Owner.RuntimeMessageLevel)
                {
                    case GH_RuntimeMessageLevel.Warning:
                        palette = GH_Palette.Warning;
                        break;

                    case GH_RuntimeMessageLevel.Error:
                        palette = GH_Palette.Error;
                        break;
                }

                // Create a new Capsule without text or icon.
                GH_Capsule capsule = GH_Capsule.CreateCapsule(Bounds, palette);

                capsule.AddInputGrip(this.InputGrip.Y);
                capsule.AddOutputGrip(this.OutputGrip.Y);

                // Render the capsule using the current Selection, Locked and Hidden states.
                // hourmask parameters are always hidden since they cannot be drawn in the rhino viewport.
                capsule.Render(graphics, Selected, Owner.Locked, true);
                capsule.Dispose(); // Always dispose of a GH_Capsule when you're done with it.
                capsule = null;

                if (!Owner.VolatileData.IsEmpty)
                {
                    HourMask hmask = Owner.maskToDraw();
                    if (hmask.IsValid)
                    {
                        Color c = Color.FromArgb(0, 0, 0);

                        Bitmap bmp = new Bitmap(365*3,24*10);
                        int pixelWidth = 3;
                        int pixelHeight = 10;
                        using (Graphics gfx = Graphics.FromImage(bmp))
                        using (SolidBrush brush = new SolidBrush(c))
                        {
                            int i = 0;
                            for (int d = 0; d < 365; d++)
                            {
                                for (int h = 0; h < 24; h++)
                                {
                                    if (hmask.eval(i)) { gfx.FillRectangle(brush, pixelWidth * d, pixelHeight * h, pixelWidth, pixelHeight); }
                                    i++;
                                }
                            }
                        }
                        graphics.DrawImage(bmp, this.Bounds.X + padding, this.Bounds.Y + paddingTop, this.Bounds.Width - padding - padding, this.Bounds.Height - padding - paddingTop);

                    }
                }


                // Now it's time to draw the text on top of the capsule.
                // First we'll draw the Owner NickName using a standard font and a black brush.
                // We'll also align the NickName in the center of the Bounds.
                StringFormat format = new StringFormat();
                format.Alignment = StringAlignment.Center;
                format.LineAlignment = StringAlignment.Center;
                format.Trimming = StringTrimming.EllipsisCharacter;


                // Our entire capsule is 60 pixels high, and we'll draw 
                // three lines of text, each 20 pixels high.
                RectangleF textRectangle = Bounds;
                textRectangle.Height = 15;

                // Draw the NickName in a Standard Grasshopper font.
                graphics.DrawString(Owner.NickName, GH_FontServer.StandardBold, Brushes.Black, textRectangle, format);

                // Always dispose of any GDI+ object that implement IDisposable.
                format.Dispose();
            }
        }


    }



}
