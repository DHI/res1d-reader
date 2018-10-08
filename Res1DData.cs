using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace DHI.Res1DReader
{

    /// <summary>
    /// Res1DData contains the actual time series values for the chosen Res1DQuery
    /// Dictionary<string, float[]>.
    /// </summary>
    public class Res1DData : Dictionary<string, float[]>
    {
        public IList<string> Ids => Keys.ToList();
        public float[] Single => Values.SingleOrDefault();

        public Res1DData() { }

        public Res1DData(IEnumerable<string> ids, IEnumerable<float[]> values)
        {
            Add(ids, values);
        }

        public void Add(Res1DData res1DData) => Add(res1DData.Keys, res1DData.Values);

        public void Add(IEnumerable<string> ids, IEnumerable<float[]> values)
        {
            foreach (var timeSeries in ids.Zip(values, (id, value) => new { Id = id, Values = value }))
                this[timeSeries.Id] = timeSeries.Values.ToArray();
        }

        /// <summary>
        /// Export data to text file.
        /// </summary>
        /// <param name="dateTimes"></param>
        /// <param name="filePath"></param>
        /// <param name="delimiter"></param>
        public void Export(IEnumerable<DateTime> dateTimes, string filePath, string delimiter = ",")
        {
            var file = new StringBuilder();
            string line = "Time";
            foreach (var h in Ids)
                line += delimiter + h;

            file.AppendLine(line);

            for (int i = 0; i < dateTimes.Count(); i++)
            {
                line = dateTimes.ElementAt(i).ToString(CultureInfo.InvariantCulture);

                foreach (var data in this)
                    line += delimiter + data.Value.ElementAt(i).ToString(CultureInfo.InvariantCulture);

                file.AppendLine(line);
            }

            File.WriteAllText(filePath, file.ToString());
            file.Clear();
        }

        /// <summary>
        /// Sum all time series values in Res1DData.
        /// </summary>
        /// <returns></returns>
        public float[] Sum()
        {
            return Sum(this.Select(ts => ts.Value).ToList());
        }

        public static float[] Sum(List<float[]> a)
        {
            float[] sum = a[0];
            for (var i = 1; i < a.Count; i++)
                sum = Sum(sum, a[i]);

            return sum;
        }

        public static float[] SumCheckNull(List<float[]> arraysToSum)
        {
            arraysToSum = arraysToSum.Where(l => l != null).ToList();

            if (arraysToSum.Count == 0)
                return new float[0];

            float[] sum = arraysToSum[0];
            for (var i = 1; i < arraysToSum.Count; i++)
                sum = Sum(sum, arraysToSum[i]);

            return sum;
        }

        public static float[] Sum(float[] a, float[] b)
        {
            if (a == null)
            {
                if (b.Length != 1) return b;
                var bb = new float[b.Length];
                for (int i = 0; i < b.Length; i++)
                    bb[i] = b[0];

                return bb;
            }

            if (b == null)
                return a;

            if (b.Length == 1)
            {
                for (int i = 0; i < a.Length; i++)
                    a[i] += b[0];

                return a;
            }

            for (int i = 0; i < a.Length; i++)
                a[i] += b[i];

            return a;
        }

        public static double[] Sum(double[] a, double[] b)
        {
            if (a == null)
            {
                if (b.Length != 1) return b;
                var bb = new double[b.Length];
                for (int i = 0; i < b.Length; i++)
                    bb[i] = b[0];

                return bb;
            }

            if (b == null)
                return a;

            if (b.Length == 1)
            {
                for (int i = 0; i < a.Length; i++)
                    a[i] += b[0];

                return a;
            }

            for (int i = 0; i < a.Length; i++)
                a[i] += b[i];

            return a;
        }

        public static double[] Subtract(double[] a, double[] b)
        {
            b = Array.ConvertAll(b, e => -e);
            return Sum(a, b);
        }

        public static Dictionary<string, double[]> ToDouble(Dictionary<string, float[]> data)
        {
            var dd = new Dictionary<string, double[]>();
            foreach (var d in data)
                dd.Add(d.Key, d.Value.Select(v => (double)v).ToArray());

            return dd;
        }
    }
}