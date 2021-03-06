﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;

namespace DYear {
    public static class Util {
        public static int defaultYear = 1999;

        public static float dbrh_to_ah(float db, float rh) {
            // absolute humidity from relative humidity and dry bulb
            // from Vaisala Humidity Conversion Formulas, 2012
            // A = C · Pw/T (g/m3)
            // constants are approximations for temperatures between -20 and 50 dC at sea level

            // AH = Absolute Humidity (g/m3)
            // DBT = Dry Bulb Temperature (C)
            // RH = Relative Humidity (%)

            return (float)(2.16679 * (6.1162 * (Math.Pow(10, ((7.5892 * db) / (db + 240.71))) * rh) * 100 / (273.16 + db)));
        }


        public static int hourOfYearFromDatetime(DateTime dt) {
            if (dt.Year == defaultYear + 1) { return 8759; }
            if (dt.Year == defaultYear) { return (dt.DayOfYear - 1) * 24 + dt.Hour - 1; }
            return -999;
        }

        public static DateTime datetimeFromHourOfYear(int hour) {
            return new DateTime(defaultYear, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddHours(hour);
        }

        public static DateTime baseDatetime() {
            return new DateTime(defaultYear, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        }



        public static Color InterpolateColor(Color c0, Color c1, double t, bool errorcheck = true) {
            int r = (int)(((c1.R - c0.R) * t) + c0.R);
            int g = (int)(((c1.G - c0.G) * t) + c0.G);
            int b = (int)(((c1.B - c0.B) * t) + c0.B);
            if (errorcheck) {
                if (r > 255) r = 255; if (g > 255) g = 255; if (b > 255) b = 255;
                if (r < 0) r = 0; if (g < 0) g = 0; if (b < 0) b = 0;
            }
            Color c = Color.FromArgb(r, g, b);
            return c;
        }

        
    }




    namespace Statistics {
        //http://www.remondo.net/calculate-mean-median-mode-averages-csharp/

        public static class Average {
            public static float Mean(this IEnumerable<float> list) {
                return list.Average(); // :-)
            }

            public static float Median(this IEnumerable<float> list) {
                List<float> orderedList = list
                    .OrderBy(numbers => numbers)
                    .ToList();

                int listSize = orderedList.Count;
                float result;

                if (listSize % 2 == 0) // even
            {
                    int midIndex = listSize / 2;
                    result = ((orderedList.ElementAt(midIndex - 1) +
                               orderedList.ElementAt(midIndex)) / 2);
                } else // odd
            {
                    float element = (float)listSize / 2;
                    element = (float)Math.Round(element, MidpointRounding.AwayFromZero);

                    result = orderedList.ElementAt((int)(element - 1));
                }

                return result;
            }

            public static IEnumerable<float> Modes(this IEnumerable<float> list) {
                var modesList = list
                    .GroupBy(values => values)
                    .Select(valueCluster =>
                            new {
                                Value = valueCluster.Key,
                                Occurrence = valueCluster.Count(),
                            })
                    .ToList();

                int maxOccurrence = modesList
                    .Max(g => g.Occurrence);

                return modesList
                    .Where(x => x.Occurrence == maxOccurrence && maxOccurrence > 1) // Thanks Rui!
                    .Select(x => x.Value);
            }


            public static float Quartile(this IEnumerable<float> listin, float quartile) {

                List<float> list = new List<float>(listin);
                list.Sort();
                float result;
                float index = quartile * (list.Count() + 1);// Get roughly the index
                float remainder = index % 1; // Get the remainder of that index value if exists
                index = (float)(Math.Floor(index) - 1); // Get the integer value of that index

                if (remainder.Equals(0)) {
                    // we have an integer value, no interpolation needed
                    result = list.ElementAt((int)index);
                } else {
                    // we need to interpolate
                    float value = list.ElementAt((int)index);
                    float interpolationValue = value
                        .Interpolate(list.ElementAt((int)(index + 1)), remainder);

                    result = value + interpolationValue;
                }

                return result;
            }

            private static float Interpolate(this float a, float b, float remainder) {
                return (b - a) * remainder;
            }
        }

    }



}
