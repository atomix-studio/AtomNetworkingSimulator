AtomNetworkingSimulator started from a gossip protocol simulation. 
This small project is a research and educationnal project about graph theory and decentralized networks.

_Basic decentralized network connections visualization_
![screenshot](Screenshots/graph_150nodes.png)

- The first goal was to be able to visualize and debug decentralized/async computations in a graph network of nodes, like gossip communications, broadcasts, voting/consensus, and so on..
- The second goal was to implement a breadth search decentralized algorithm to compute an optimal n-tree from a root node that allows fast spread of message with minimal message count over the network
- The third goal was to test this protocol to allow simulated 'players' in a 3D scene to be connected to peers related to their distance in the virtual world. It would allow a multiplayer open-world game to be fully decentralized, with no main server instance.


_Computed Tree example (N = 3)_
_The tree nodes are connected by the cycle distance of the received broadcasts made over gossip protocol

![screenshot](Screenshots/decentralized_bfs.png)
