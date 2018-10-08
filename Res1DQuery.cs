using System.Collections.Generic;
using System.Linq;

namespace DHI.Res1DReader
{
    /// <summary>
    /// Create query for specific quantity in res1d file.
    /// </summary>
    public abstract class Res1DQuery
    {
        public string Quantity, Res1DFileKey, Type, Name;
        public List<string> Ids { get; }

        protected Res1DQuery(string res1DFileKey, IEnumerable<string> ids, string quantity, string type)
        {
            Ids = ids.ToList();
            Quantity = quantity;
            Res1DFileKey = res1DFileKey;
            Type = type;
        }

        protected Res1DQuery(string res1DFileKey, string id, string quantity, string type) : this(res1DFileKey, new[] { id }, quantity, type) { }

        public abstract Res1DData GetData(Res1DReader res1D);

        public override string ToString()
        {
            return Res1DFileKey + Type + Quantity;
        }
    }

    public class Res1DQueryReach : Res1DQuery
    {
        public bool? AtFromNode;

        public Res1DQueryReach(string res1DFileKey, IEnumerable<string> ids, string quantity, bool? atFromNode = null) 
            : base(res1DFileKey, ids, quantity, "Reach")
        {
            AtFromNode = atFromNode;
        }

        public Res1DQueryReach(IEnumerable<string> ids, string quantity, bool? atFromNode = null) 
            : this("res1d", ids, quantity, atFromNode) { }


        public override Res1DData GetData(Res1DReader res1D)
        {
            return new Res1DData(Ids, Ids.Select(id => res1D.GetReach(id, Quantity, AtFromNode, Res1DFileKey)));
        }

        public override string ToString()
        {
            return Res1DFileKey + Type + Quantity + AtFromNode;
        }
    }

    public class Res1DQueryNode : Res1DQuery
    {
        public Res1DQueryNode(string res1DFileKey, IEnumerable<string> ids, string quantity) : base(res1DFileKey, ids, quantity, "Node") { }
        public Res1DQueryNode(IEnumerable<string> ids, string quantity) : this("res1d", ids, quantity) { }

        public override Res1DData GetData(Res1DReader res1D)
        {
            return new Res1DData(Ids, Ids.Select(id => res1D.GetNode(id, Quantity, Res1DFileKey)));
        }
    }

    public class Res1DQueryCatchment : Res1DQuery
    {
        public Res1DQueryCatchment(string res1DFileKey, IEnumerable<string> ids, string quantity) : base(res1DFileKey, ids, quantity, "Catchment") { }
        public Res1DQueryCatchment(IEnumerable<string> ids, string quantity) : this("res1d", ids, quantity) { }

        public override Res1DData GetData(Res1DReader res1D)
        {
            return new Res1DData(Ids, Ids.Select(id => res1D.GetCatchment(id, Quantity, Res1DFileKey)));
        }
    }
}