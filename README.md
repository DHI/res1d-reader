# res1d-reader
Reads and resamples multiple Mike1D result files (.res1d)

Example code

```python
# Example res1d file from https://github.com/DHI/mike1d-sdk-examples/tree/master/data/Results
Res1DFilePath = r"C:/GitHub/mike1d-sdk-examples/data/Results/vida96-3.res1d";    

res1D = Res1DReader(Res1DFilePath);
reachName1 = "VIDAA-NED";
reachName2 = "VIDAA-MEL";

time = res1D.DateTimes;

reachData1 = res1D.GetReach(reachName1, quantity = "Discharge", atFromNode = True);

nodeName1 = "'LINDSKOV', 1";
nodeData1 = res1D.GetNode(nodeName1, quantity = "WaterLevel");

# Get same quantity from multiple reaches -- use query and Get()
reachNames = [reachName1, reachName2];
query = Res1DQueryReach(reachNames, "WaterLevel", atFromNode = True);
reachData2 = res1D.Get(query)
```
