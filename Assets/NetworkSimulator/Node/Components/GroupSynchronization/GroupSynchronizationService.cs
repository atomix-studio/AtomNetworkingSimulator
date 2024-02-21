using Atom.ComponentSystem;
using System;

namespace Atom.GroupManagement
{
    public static class GroupSynchronizationServiceEventHandler
    {

    }

    [Serializable]
    /// <summary>
    /// TODO DECOUPLIGN GROUP MANAGEMENT FROM NODE TO HERE
    /// </summary>
    public class GroupSynchronizationService : INodeComponent
    {
        public NodeEntity context { get ; set; }

        public void OnInitialize()
        {
        }

        public void OnNodeComponentInitialize()
        {
        }

        public void OnNodeComponentUpdate()
        {
        }

        // the group is the tcp stream synchronized part of the network
        // the players have to be in the same group to see and interact with each other

        // groups are maintained with different set of rules that we will test here

        // if a node is not anymore in the avalaible peers (PartialView), or if a connected node peer score decreases too much comparate to other peers
        // the connection should be closed if there is a better option avalaible
        // the node will run a request-to-replace protocol in this situation

        /// group searching will be capable to handle the fact that PartialView peers are not avalaible and to go search further via deeper and deeper broadcast 
        /// and also to change is local peers to more avalaible ones if they are already busy with group connections


        // exclusive group = all members are connected with each other ? exclusive group joining is possible only after a full group vote initiated by the group member which received the grouping request from an external node
        // partial group = node is connected with only one member of the group > message should then be broadcasted but overall, all members should be connected only with members of the same group, identified by an id that is propagated along the group lifecycle
        // streamed groups = node is connected with other nodes, unregarding with which nodes its connections are connected with

        // a group can be created when a node doesn't find any group to join. From this moment, the group will exist as an identifier propagated to all new joiners until it will eventually fade when nodes disconnect
        // a group can remain active even if the creator is disconnected from the network
        // grouping rules logic are written in group synchronisation core module abstract class 


    }
}
