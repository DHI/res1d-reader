using Xunit;

namespace Res1DReader.Test
{
    public class Res1DReaderTest
    {
        /// <summary>
        /// Example res1d file from
        /// https://github.com/DHI/mike1d-sdk-examples/tree/master/data/Results
        /// </summary>
        public const string Res1DFilePath = @"..\..\..\..\mike1d-sdk-examples\data\Results\vida96-3.res1d";


        [Fact]
        public void Test1()
        {
            var res1D = new Res1DReader(Res1DFilePath);
            var reachName1 = "VIDAA-NED";
            var reachName2 = "VIDAA-MEL";

            var time = res1D.DateTimes;

            var reachData1 = res1D.GetReach(reachName1, quantity: "Discharge", atFromNode: true);

            var nodeName1 = "'LINDSKOV', 1";
            var nodeData1 = res1D.GetNode(nodeName1, quantity: "WaterLevel");

            // Get same quantity from multiple reaches -- use query and Get()
            var reachNames = new []{reachName1, reachName2};
            var query = new Res1DQueryReach(reachNames, "WaterLevel", atFromNode: true);
            var reachData2 = res1D.Get(query);
        }

    }
}
