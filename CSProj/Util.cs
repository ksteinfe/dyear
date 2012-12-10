using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DYear
{
    public static class Util
    {
        public static float dbrh_to_ah(float db, float rh)
        {
            // absolute humidity from relative humidity and dry bulb
            // from Vaisala Humidity Conversion Formulas, 2012
            // A = C · Pw/T (g/m3)
            // constants are approximations for temperatures between -20 and 50 dC at sea level

            // AH = Absolute Humidity (g/m3)
            // DBT = Dry Bulb Temperature (C)
            // RH = Relative Humidity (%)

            return (float)(2.16679 * (6.1162 * (Math.Pow(10, ((7.5892 * db) / (db + 240.71))) * rh) * 100 / (273.16 + db)));
        }

    }
}
