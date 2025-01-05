using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static Atom.Components.HierarchicalTree.HierarchicalTreeEntityHandlingComponent;

namespace Atom.Components.HierarchicalTree
{
    internal class HierarchicalTreeDashboard : MonoBehaviour
    {
        [Button]
        private void DisbandGraph()
        {
            var comps = FindObjectsByType<HierarchicalTreeEntityHandlingComponent>(FindObjectsSortMode.None);
            foreach (var entity in comps)
            {
                entity.ClearConnections();
            }
        }

        [Button]
        private void SetMode(GraphSortingRules graphSortingRule)
        {
            var comps = FindObjectsByType<HierarchicalTreeEntityHandlingComponent>(FindObjectsSortMode.None);
            foreach (var entity in comps)
            {
                entity.SortingRule = graphSortingRule;
            }
        }

        [Button]
        private void SetRequestSorting(bool state)
        {
            var comps = FindObjectsByType<HierarchicalTreeEntityHandlingComponent>(FindObjectsSortMode.None);
            foreach (var entity in comps)
            {
                entity.ChildrenRequestSorting = state;
            }
        }
    }
}
