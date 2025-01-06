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
        private void GetInfos()
        {
            var min_rank = int.MaxValue;
            var max_rank = int.MinValue;
            var average_connections = 0f;
            var parents = 0;
            var comps = FindObjectsByType<HierarchicalTreeEntityHandlingComponent>(FindObjectsSortMode.None);
            foreach (var entity in comps)
            {
                min_rank = Math.Min(min_rank, entity.currentRank);
                max_rank = Math.Max(max_rank, entity.currentRank);
                average_connections += entity.children.Count;
                if(entity.children.Count > 0 )
                    parents++;
            }

            average_connections /= parents;

            Debug.Log($"Min rank : {min_rank}, Max_rank : {max_rank}, average children cout : {average_connections}");
        }

        [Button]
        private void DisbandGraph()
        {
            var comps = FindObjectsByType<HierarchicalTreeEntityHandlingComponent>(FindObjectsSortMode.None);
            foreach (var entity in comps)
            {
                entity.ResetTreeData();
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
    }
}
