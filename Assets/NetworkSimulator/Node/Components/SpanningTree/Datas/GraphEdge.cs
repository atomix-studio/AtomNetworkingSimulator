using Sirenix.OdinInspector;
using System;

namespace Atom.Components.GraphNetwork
{
    /// <summary>
    /// Representation of a connection with an outer node in the graph
    /// When a connection is accepted by a node, both sides creates an instance of edge with the adress of the other node
    /// All graph edges concatened at the network level represents the MST at the end of the procedure
    /// </summary>
    [Serializable, HideLabel]
    public class GraphEdge
    {
        [HorizontalGroup("GraphEdge")] public long EdgeId;
        [HorizontalGroup("GraphEdge")] public string EdgeAdress;

        public GraphEdge(long edgeId, string edgeAdress)
        {
            EdgeId = edgeId;
            EdgeAdress = edgeAdress;
        }
    }
}
