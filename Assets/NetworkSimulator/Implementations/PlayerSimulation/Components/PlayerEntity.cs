using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AI;

namespace Atom.PlayerSimulation
{
    /// <summary>
    /// Basically a simple navmesh agent that will randomly roam on the map and which will reflect a simulated Node.
    /// The goal of the system is to keep syncrhonising playerEntities that are close in the game world.
    /// </summary>
    public class PlayerEntity : MonoBehaviour
    {
        /// <summary>
        /// The nodeEntity that owns this simulated player
        /// </summary>
        [SerializeField] private NodeEntity _ownerNodeEntity;
        private NavMeshAgent _agent;

        public void StartRoaming()
        {
            _agent = GetComponent<NavMeshAgent>();
            StartCoroutine(RoamRoutine());
        }

        IEnumerator RoamRoutine()
        {
            var next_pos = Vector3.zero;
            while (true)
            {
                while(true)
                {
                    next_pos = new Vector3(UnityEngine.Random.Range(-100, 100), UnityEngine.Random.Range(-100, 100), UnityEngine.Random.Range(-100, 100));
                    if (NavMesh.SamplePosition(next_pos, out var navMeshHit, 10, 0))
                    {
                        next_pos = navMeshHit.position;
                        break;
                    }
                    yield return null;
                }
                
                _agent.SetDestination(next_pos);
                _agent.isStopped = false;

                while((_agent.transform.position - next_pos).magnitude > 1)
                {
                    yield return null;
                }

                _agent.isStopped = true;

            }
        }
    }
}
