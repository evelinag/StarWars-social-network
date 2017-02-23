# Star Wars social networks

Contents of the files are the following:

* `starwars-episode-N-interactions.json` contains the social network extracted from Episode N, where the links between characters are
defined by the times the characters speak within the same scene.

* `starwars-episode-N-mentions.json` contains the social network extracted from Episode N, where the links between characters are
defined by the times the characters are mentioned within the same scene.

* `starwars-episode-N-interactions-allCharacters.json` is the `interactions` network with R2-D2 and Chewbacca added in using 
data from `mentions` network.

* `starwars-full-...` contain the corresponding social networks for the whole set of 6 episodes.

## Description of networks
The json files representing the networks contain the following information:

### Nodes
The nodes contain the following fields:
- name: Name of the character
- value: Number of scenes the character appeared in
- colour: Colour in the visualization

### Links
Links represent connections between characters. The link information corresponds to: 
- source: zero-based index of the character that is one end of the link, the order of nodes is the order in which they are listed in the “nodes” element
- target: zero-based index of the character that is the the other end of the link. 
- value: Number of scenes where the “source character” and “target character” of the link appeared together.
Please not that the network is *undirected*. Which character represents the source and the target is arbitrary, they correspond only to two ends of the link.

