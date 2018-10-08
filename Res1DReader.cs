using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using DHI.Mike1D.Generic;
using DHI.Mike1D.ResultDataAccess;
using DHI.Services.TimeSeries;

namespace DHI.Res1DReader
{
    /// <summary>
    /// Class for extracting and resampling multiple res1D-file time series.
    /// Resamples all results if samplingPeriodMinutes > 0 in constructor.
    /// Uses first loaded res1d file to set start time.
    /// </summary>
    public class Res1DReader
    {
        public const string DefaultRes1DFileKey = "res1d"; // Used when loading a single res1d

        /// <summary>
        ///  The sampling reference sets the target resampling for all other res1d files.
        /// </summary>
        public string Res1DFileKeySamplingReference { get; }

        public TimeSpan SamplingPeriod { get; }
        public DateTime TargetStartTime { get; }
        public IList<DateTime> DateTimes { get; }
        protected bool DoResample { get; }

        private readonly Dictionary<string, ResultData> _res1DData = new Dictionary<string, ResultData>();
        private readonly Dictionary<string, DateTime[]> _res1DTimes = new Dictionary<string, DateTime[]>();

        public Res1DReader(string res1DFile, int samplingPeriodMinutes = 0)
            : this(new Dictionary<string, string> {{DefaultRes1DFileKey, res1DFile}}, samplingPeriodMinutes) { }

        /// <summary>
        /// Load multiple res1d files and set first res1d-file as sampling reference.
        /// </summary>
        /// <param name="res1DFiles"></param>
        /// <param name="samplingPeriodMinutes"></param>
        public Res1DReader(Dictionary<string, string> res1DFiles, int samplingPeriodMinutes = 0)
        {
            foreach (var res1DFile in res1DFiles)
            {
                _res1DData.Add(res1DFile.Key, LoadRes1DResultData(res1DFile.Value));
                _res1DTimes.Add(res1DFile.Key, _res1DData[res1DFile.Key].TimesList.ToArray()); // Save time stamps
            }

            Res1DFileKeySamplingReference = res1DFiles.Keys.First();
            DateTimes = GetDateTimes();

            SamplingPeriod = new TimeSpan(0, samplingPeriodMinutes, 0);
            DoResample = SamplingPeriod.TotalMinutes > 0;
            if (DoResample)
            {
                DateTimes = GetDateTimesResample(samplingPeriodMinutes, Res1DFileKeySamplingReference);
                TargetStartTime = DateTimes.First();
            }
        }

        public Res1DReader(IEnumerable<string> res1DFileKey, IEnumerable<string> res1DFilePath, int samplingPeriodMinutes = 0)
            : this(res1DFileKey.Zip(res1DFilePath, (k, v) => new {k, v}).ToDictionary(x => x.k, x => x.v), samplingPeriodMinutes) { }

        private static ResultData LoadRes1DResultData(string res1DFilePath)
        {
            IResultData resultData = new ResultData();
            resultData.Connection = Connection.Create(res1DFilePath);

            var resultDiagnostics = new Diagnostics(res1DFilePath);
            resultData.Load(resultDiagnostics);

            if (resultDiagnostics.ErrorCountRecursive > 0)
                throw new Exception("File could not be loaded: " + res1DFilePath);

            return (ResultData) resultData;
        }

        public Res1DData Get(Res1DQuery query) => Get(new[] {query});

        public Res1DData Get(params Res1DQuery[] queries) => Get(queries.ToList());

        public Res1DData Get(IList<Res1DQuery> queries)
        {
            var results = new Res1DData();
            foreach (var query in queries)
            {
                if (DoResample)
                {
                    foreach (var data in query.GetData(this))
                        results.Add(query + data.Key, Resample(query.Res1DFileKey, data.Value));
                }
                else
                    results.Add(query.GetData(this));
            }

            return results;
        }

        public float[] GetNode(string id, string quantity = "WaterLevel", string res1DFileKey = DefaultRes1DFileKey)
        {
            IRes1DNode node = _res1DData[res1DFileKey].Nodes.FirstOrDefault(n => n.Id.Equals(id))
                              ?? throw new Exception("Could not find node: " + id);

            IDataItem dataItem = GetDataItem(node, id, quantity, res1DFileKey);

            return dataItem.CreateTimeSeriesData(0);
        }

        public float[] GetReach(string id, string quantity = "Discharge", bool? atFromNode = null, string res1DFileKey = DefaultRes1DFileKey)
        {
            IRes1DReach reach = _res1DData[res1DFileKey].Reaches.FirstOrDefault(n => n.Name.Equals(id))
                                ?? _res1DData[res1DFileKey].Reaches.FirstOrDefault(n => n.Name.Equals("Weir:" + id))
                                ?? _res1DData[res1DFileKey].Reaches.FirstOrDefault(n => n.Name.Equals("Orifice:" + id))
                                ?? throw new Exception("Could not find reach: " + id);

            IDataItem dataItem = GetDataItem(reach, id, quantity, res1DFileKey);

            return GetReach(quantity, atFromNode, dataItem) ?? throw new Exception($"atFromNode must be defined for reach {id} {quantity}");
        }

        private static float[] GetReach(string quantity, bool? atFromNode, IDataItem dataItem)
        {
            if (atFromNode.HasValue)
            {
                var elementIndex = (bool) atFromNode ? 0 : dataItem.NumberOfElements - 1;
                return dataItem.CreateTimeSeriesData(elementIndex);
            }
            else if (quantity.Contains("Volume")) // Sum all HD points ToDo: handle depending on quantity
            {
                float[] sum = dataItem.CreateTimeSeriesData(0);
                for (var i = 1; i < dataItem.NumberOfElements; i++)
                    sum = Res1DData.Sum(sum, dataItem.CreateTimeSeriesData(i));

                return sum;
            }

            return null;
        }

        public float[] GetCatchment(string id, string quantity = "TotalRunOff", string res1DFileKey = DefaultRes1DFileKey)
        {
            //if (quantity.Equals("CatchmentDischarge"))
            //    id += "CatchmentDischarge";

            IRes1DCatchment catchment = _res1DData[res1DFileKey].Catchments.FirstOrDefault(n => n.Id.Equals(id))
                                        ?? throw new Exception("Could not find catchment: " + id);

            IDataItem dataItem = GetDataItem(catchment, id, quantity, res1DFileKey);

            return dataItem.CreateTimeSeriesData(0);
        }

        public IDataItem GetDataItem(IRes1DDataSet dataSet, string id, string quantity, string res1DFileKey)
        {
            return dataSet.DataItems.FirstOrDefault(di => di.Quantity.Equals(ParseQuantity(quantity))) // Assume only one dataItem per quantity
                   ?? throw new Exception("No data for chosen quantity: " + quantity + " for " + id);
        }

        private static Quantity ParseQuantity(string quantity)
        {
            try
            {
                quantity = Regex.Replace(quantity, " ", "");
                var q = (PredefinedQuantity) Enum.Parse(typeof(PredefinedQuantity), quantity);
                return Quantity.Create(q);
            }
            catch
            {
                throw new Exception("Invalid or unsupported quantity: " + quantity);
            }
        }


        public void GetStartEndDateTimes(out DateTime startTime, out DateTime endTime)
        {
            startTime = _res1DData[DefaultRes1DFileKey].StartTime;
            endTime = _res1DData[DefaultRes1DFileKey].EndTime;
        }

        public DateTime[] GetDateTimes(string res1DFileKey = DefaultRes1DFileKey)
        {
            return _res1DData[res1DFileKey].TimesList.ToArray();
        }

        public DateTime[] GetDateTimesResample(int samplingPeriodMinutes, string res1DFileKey = DefaultRes1DFileKey)
        {
            DateTime from = _res1DData[res1DFileKey].StartTime;
            DateTime to = _res1DData[res1DFileKey].EndTime;
            var timeStep = new TimeSpan(0, samplingPeriodMinutes, 0);

            return DateTimeRange(from, to, timeStep).ToArray();
        }

        public static IEnumerable<DateTime> DateTimeRange(DateTime from, DateTime to, TimeSpan timeStep)
        {
            for (DateTime t = from; t <= to; t = t.Add(timeStep))
                yield return t;
        }


        /// <summary>
        ///  Resamples time series data (time, values)
        /// </summary>
        /// <param name="time"></param>
        /// <param name="values"></param>
        /// <param name="targetStartTime"></param>
        /// <param name="targetSamplePeriod"></param>
        /// <returns></returns>
        public static float[] Resample(DateTime[] time, float[] values, DateTime targetStartTime, TimeSpan targetSamplePeriod)
        {
            var tsData = CreateTimeSeriesData(time, values);

            // Insert starting time if offset from target resampling
            if (time.First() > targetStartTime)
                tsData.Insert(targetStartTime, values.First());
            else if (time.First() < targetStartTime)
            {
                var startValue = tsData.GetInterpolated(targetStartTime, TimeSeriesDataType.Instantaneous).Value;
                tsData.Insert(targetStartTime, startValue);
            }

            return GetValues(tsData.Resample(targetSamplePeriod));
        }

        public float[] Resample(string res1DFileKey, float[] values)
        {
            return Resample(_res1DTimes[res1DFileKey], values, TargetStartTime, SamplingPeriod);
        }

        private static ITimeSeriesData<double> CreateTimeSeriesData(DateTime[] time, float[] values)
        {
            ITimeSeriesData<double> data = new TimeSeriesData<double>();
            for (var i = 0; i < time.Length; i++)
            {
                data.DateTimes.Add(time[i]);
                data.Values.Add(values[i]);
            }

            return data;
        }

        private static float[] GetValues(ITimeSeriesData<double> tsData)
        {
            return tsData.Values.Select(v => (float) (v ?? 0)).ToArray();
        }

        public string[] GetDateTimesResampleString(int minutes, string res1DFileKey = DefaultRes1DFileKey)
        {
            var tr = GetDateTimesResample(minutes, res1DFileKey);
            return ToDateTimesString(tr);
        }

        public static string[] ToDateTimesString(DateTime[] time)
        {
            var english = CultureInfo.GetCultureInfo("en-GB");
            var dt = new string[time.Length];
            for (int t = 0; t < time.Length; t++)
                dt[t] = time[t].ToString(english);

            return dt;
        }

        public static IEnumerable<double> ToDouble(IEnumerable<float> values)
        {
            return values.Select(value => (double) value);
        }
    }
}