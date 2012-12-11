using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using System.Drawing;

namespace DYear
{
    public class MDHr
    {
        internal Dictionary<string, float> m_vals;
        internal int m_hr; //hour of year
        public static int defaultYear = 2013;
        public Point3d pos;
        public Color color;

        public MDHr()
        {
            m_vals = new Dictionary<string, float>();
            m_hr = -1;
            pos = new Point3d();
            color = Color.FromArgb(0, 0, 0);
        }
        public MDHr(MDHr t)
        {
            this.m_hr = t.m_hr;
            this.m_vals = new Dictionary<string, float>(t.m_vals);
            pos = new Point3d(t.pos.X, t.pos.Y, t.pos.Z);
            color = Color.FromArgb(t.color.A, t.color.R, t.color.G, t.color.B);
        }
        public MDHr(int hr) : this() 
        {
            if ((hr >= 0) && (hr < 8760)) { m_hr = hr; } else { m_hr = -1; }
            this.m_vals = new Dictionary<string, float>();
            pos = new Point3d();
            color = Color.FromArgb(0, 0, 0);
        }
        public DateTime dt
        {
            get{return  new DateTime(MDHr.defaultYear, 1, 1, 0, 0, 0).AddHours(this.m_hr);}
            set {this.m_hr = (int)(((value.DayOfYear - 1) * 24) + value.Hour); }
        }

        public string[] keys {
            get { return new List<string>(m_vals.Keys).ToArray(); } 
        }

    }

    public class DHr : GH_Goo<MDHr> , IComparable<DHr>
    {
        public DHr() : base() { this.Value = new MDHr(); }
        public DHr(MDHr instance) { this.Value = instance; }
        public DHr(DHr instance) { this.Value = new MDHr(instance.Value);}
        public override IGH_Goo Duplicate() {return new DHr(this);}

        public DHr(int hr) { this.Value = new MDHr(hr); }

        #region // GETTERS AND SETTERS

        public int hr
        {
            get { return Value.m_hr; }
            set { Value.m_hr = value; }
        }
        public DateTime dt
        {
            get { return Value.dt; }
            set { Value.dt = value; }
        }
        public Point3d pos
        {
            get { return Value.pos; }
            set { Value.pos = value; }
        }
        public Color color
        {
            get { return Value.color; }
            set { Value.color = value; }
        }

        public float val(string key) {return Value.m_vals[key];}
        public void put(string key, float val) {Value.m_vals[key] = val;}
        public string[] keys
        {
            get { return Value.keys; }
        }
        #endregion 

        #region // GH STUFF

        public override bool IsValid
        {
            get
            {
                return Value.m_hr >= 0;
            }
        }
        public override object ScriptVariable()
        {
            return new DHr(this);
        }
        public override string ToString()
        {
            return string.Format("hour {0} [{1} keyed values]", Value.m_hr, Value.keys.Length);
        }
        public override string TypeDescription
        {
            get { return "Represents a Data Hour"; }
        }
        public override string TypeName
        {
            get { return "Data Hour"; }
        }
        #endregion 
    
        public int CompareTo(DHr other) {return this.hr.CompareTo(other.hr); }

    }

    public class GHParam_DHr : GH_Param<DHr>
{
        public int validHourCount;
        public List<string> commonKeys;
        public List<string> orphanedKeys;

        public GHParam_DHr()
            : base(new GH_InstanceDescription("Data Hour", "Dhr", "Represents a collection of Data Hours", "DYear", "Params"))
        {
            validHourCount = 0;
            commonKeys = new List<string>();
            orphanedKeys = new List<string>();
        }
  
        public override System.Guid ComponentGuid {get { return new Guid("{1573577D-7B23-46C4-803D-594ECE47BA10}"); }}
        protected override Bitmap Icon { get { return DYear.Properties.Resources.Icons_Param_Dhr; } }

        public override void CreateAttributes() { m_attributes = new GHParam_DHr_Attributes(this); }

        protected override void CollectVolatileData_FromSources()
        {
            base.CollectVolatileData_FromSources();
            updateLocals();
        }

        private void updateLocals()
        {
            int n = 0;
            validHourCount = 0;
            List<string> allKeys = new List<string>();
            foreach (DHr hr in this.m_data) {
                if (hr.IsValid)
                {
                    foreach (string key in hr.keys) { if (!allKeys.Contains(key)) { allKeys.Add(key); } }
                    validHourCount++;
                }
                else
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Invalid Dhr found at index" + n);
                }
                n++;
            }
            commonKeys = new List<string>();
            orphanedKeys = new List<string>();
            foreach (string key in allKeys)
            {
                bool isCommon = true;
                foreach (DHr hr in this.m_data) { 
                    if(!hr.Value.m_vals.ContainsKey(key)){isCommon=false; break;} 
                }
                if (isCommon) { commonKeys.Add(key); } else { 
                    orphanedKeys.Add(key);
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "An hour in this collection contains the unique key '"  + key + "'.");
                }
            }
        }

        public override void AppendAdditionalMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalMenuItems(menu);

            
            Menu_AppendSeparator(menu);
            Menu_AppendItem(menu, "-- KEYS -- (click below to copy to clipboard)");
            Menu_AppendSeparator(menu);

            foreach (string key in commonKeys) { Menu_AppendItem(menu, key, Menu_KeyItemClicked); }
        }

        private void Menu_KeyItemClicked(Object sender, EventArgs e)
        {
            System.Windows.Forms.ToolStripMenuItem ti = (System.Windows.Forms.ToolStripMenuItem)(sender);
            System.Windows.Forms.Clipboard.SetText(ti.Text);
        }

    }

    public class GHParam_DHr_Attributes : GH_Attributes<GHParam_DHr>
    {
        public GHParam_DHr_Attributes(GHParam_DHr owner) : base(owner) { }

        protected override void Layout()
        {
            // Compute the width of the NickName of the owner (plus some extra padding), 
            // then make sure we have at least 100 pixels.
            int width = GH_FontServer.StringWidth(Owner.NickName, GH_FontServer.Standard);
            width = Math.Max(width + 10, 100);

            // The height of our object is always 100
            int height = 50;

            // Assign the width and height to the Bounds property.
            // Also, make sure the Bounds are anchored to the Pivot
            Bounds = new RectangleF(Pivot, new SizeF(width, height));
        }
        public override void ExpireLayout()     
        {    
            base.ExpireLayout();
            // Destroy any data you have that becomes 
            // invalid when the layout expires. 
        }

        protected override void Render(GH_Canvas canvas, Graphics graphics, GH_CanvasChannel channel){
          // Render all the wires that connect the Owner to all its Sources.
          if (channel == GH_CanvasChannel.Wires)
          {
            RenderIncomingWires(canvas.Painter, Owner.Sources, Owner.WireDisplay);
            return;
          }
          // Render the parameter capsule and any additional text on top of it.
          if (channel == GH_CanvasChannel.Objects)
          {
            // Define the default palette.
            GH_Palette palette = GH_Palette.Normal;
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
            RectangleF textRectangle = Bounds;
            textRectangle.Height = 15;

            // Draw the NickName in a Standard Grasshopper font.
            graphics.DrawString(Owner.NickName, GH_FontServer.StandardBold, Brushes.Black, textRectangle, format);


            // Now we need to draw the median and mean information.
            // Adjust the formatting and the layout rectangle.
            format.Alignment = StringAlignment.Near;
            textRectangle.Inflate(-5, 0);

            textRectangle.Y += 20;
            graphics.DrawString(String.Format("{0} Hours", Owner.validHourCount), GH_FontServer.Standard, Brushes.Black, textRectangle, format);

            textRectangle.Y += 15;
            graphics.DrawString(String.Format("{0} Keys", Owner.commonKeys.Count), GH_FontServer.Standard, Brushes.Black, textRectangle, format);


            // Always dispose of any GDI+ object that implement IDisposable.
            format.Dispose();
          }
        }


    }






}
