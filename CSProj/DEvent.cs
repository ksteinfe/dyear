using System;
using System.Collections.Generic;
using System.Text;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;


/*
namespace DYear
{
    public class DEvent
    {
        private static int defaultYear = 2010;
        private static TimeSpan defaultSpan = new TimeSpan(1, 0, 0);
        private static DateTime defaultStart = new DateTime(defaultYear, 1, 1, 0, 0, 0);

        private Dictionary<string, float> m_vals;
        private DateTime m_start;
        private TimeSpan m_span;

        public DEvent()
        {
            m_vals = new Dictionary<string, float>();
            m_span = defaultSpan;
            m_start = defaultStart;
        }
        public DEvent(DEvent t)
        {
            this.m_vals = t.m_vals;
            this.m_span = t.m_span;
            this.m_start = t.m_start;
        }

        #region //DateTime Getters and Setters

        public DateTime startDatetime {
            get{return this.m_start; }
            set{ this.m_start = new DateTime(DEvent.defaultYear, value.Month, value.Day, value.Hour, value.Minute, 0);}
        }

        public DateTime endDatetime {
            get{ return this.m_start + this.m_span; }
        }

        public TimeSpan spanDatetime
        {
            get { return this.m_span; }
            set { if (value.TotalMinutes >= 1) { this.m_span = value; } }
        }

        public double startHour
        {
            get { return dateTimeToDecimalHours(this.m_start); }
            set { if ((value > 0) && (value < 8760)) { this.m_start = decimalHoursToDateTime(value); } }
        }
        public double endHour
        {
            get { return dateTimeToDecimalHours(this.endDatetime); }
        }
        public double spanHour
        {
            get { return this.m_span.TotalHours; }
            set { if (value > 0) {
                long ticks = (long)(TimeSpan.TicksPerHour * value);
                this.m_span = new TimeSpan();
            } }
        }

        public static double dateTimeToDecimalHours(DateTime dt)
        {
            double hour = ((dt.DayOfYear - 1) * 24)+dt.Hour;
            double min = dt.Minute;
            return hour + (min*60);
        }
        public static DateTime decimalHoursToDateTime(double dh)
        {
            DateTime dt = new DateTime(defaultYear, 1, 1, 0, 0, 0);
            return dt.AddHours(dh);
        }

        #endregion

        public float val(string key)
        {
            return m_vals[key];
        }
        public void put(string key, float val)
        {
            m_vals[key] = val;
        }
        public string[] keys {
            get { return new List<string>(m_vals.Keys).ToArray(); } 
        }

    }

    public class GH_DEvent : GH_Goo<DEvent>
    {
        public GH_DEvent() : base() { this.Value = new DEvent();}
        public GH_DEvent(DEvent instance) { this.Value = instance; }
        public GH_DEvent(GH_DEvent instance) { this.Value = instance.Value;}
        public override IGH_Goo Duplicate() {return new GH_DEvent(this);}

        public override bool IsValid
        {
            get { 
                //TODO: test if start time is valid as well
                return true; 
            }
        }

        // Return the TickDict Object to script interfaces
        public override object ScriptVariable()
        {
            return new GH_DEvent(this);
        }
        public override string ToString()
        {
            return string.Format("{0} [{1} keyed values]", Value.startHour, Value.keys.Length);
        }
        public override string TypeDescription
        {
            get { return "Represents a Data Event - a defined span of time within an idealized year and a key-value dictionary of values associated with it."; }
        }
        public override string TypeName
        {
            get { return "DEvent"; }
        }
    }

    public class GHParam_DEvent : GH_Param<GH_DEvent>
{

        public GHParam_DEvent() : base(new GH_InstanceDescription("Data Event", "DEvent", "Represents a collection of Data Events\n (a 'data event' is a defined span of time within an idealized year and a key-value dictionary of values associated with it)", "Params", "Primitive")) { }
  
        public override System.Guid ComponentGuid
        {
            get { return new Guid("{1573577D-7B23-46C4-803D-594ECE47BA10}"); }
        }

 
}





}
*/
