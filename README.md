# The Star Wars social network

This repository contains the code I used to create my blog post on the 
[Star Wars social network](http://evelinag.com/blog/2015/12-15-star-wars-social-network/index.html).

*Update*: I added the 7th episode Star Wars: The Force Awakens and included more network analysis [on my blog](http://evelinag.com/blog/2016/01-25-social-network-force-awakens/index.html).

The folder `data` contains:

- `characters.csv`: extracted list of named characters that appeared in the screenplays. 
- `aliases.csv`: csv file with alternative names for some of the characters
- `charactersPerScene.csv`: each line contains name of a character followed by the relative 
   times when the character is mentioned in the screenplay. I used this data to generate character timelines. 
   The values were computed as 
      
       episode number + scene number/number of scenes in episode
   
   Values [0,1] correspond to mentions in Episode I, [1,2] to Episode II etc. 
   
   [Note that this is not a valid csv file because each line contains
   a different number of columns]
   
## Networks   
The folder `networks` contains:

* `starwars-episode-N-interactions.json` contains the social network extracted from Episode N, where the links between characters are
defined by the times the characters speak within the same scene.
* `starwars-episode-N-mentions.json` contains the social network extracted from Episode N, where the links between characters are
defined by the times the characters are mentioned within the same scene.
* `starwars-episode-N-interactions-allCharacters.json` is the `interactions` network with R2-D2 and Chewbacca added in using 
data from `mentions` network.
* `starwars-full-...` contain the corresponding social networks for the whole set of 6 episodes.

### Description of networks
The json files representing the networks contain the following information:

#### Nodes
The nodes contain the following fields:
- name: Name of the character
- value: Number of scenes the character appeared in
- colour: Colour in the visualization

#### Links
Links represent connections between characters. The link information corresponds to: 
- source: zero-based index of the character that is one end of the link, the order of nodes is the order in which they are listed in the “nodes” element
- target: zero-based index of the character that is the the other end of the link. 
- value: Number of scenes where the “source character” and “target character” of the link appeared together.
Please not that the network is *undirected*. Which character represents the source and the target is arbitrary, they correspond only to two ends of the link.


The other files contain the code used to generate the data files and the plots.
* `interactions.html` and `episode-interactions.html` contain the D3.js code to visualize the networks.
* The rest of the code is in F# and uses [FsLab](http://fslab.org/)
* To run the code, use [paket reference manager](http://fsprojects.github.io/Paket/) to download all the dependencies


## Using the social network data

For details please refer to the associated repository [evelinag/star-wars-network-data](https://github.com/evelinag/star-wars-network-data) containing only the dataset.

### Citing the dataset

If you use the dataset in your work, please use the following citation:

Gabasova, E. (2016). Star Wars social network. DOI: https://doi.org/10.5281/zenodo.1411479.

    @misc{gabasova_star_wars_2016,
      author  = {Evelina Gabasova},
      title   = {{Star Wars social network}},
      year    = 2016,
      url     = {https://doi.org/10.5281/zenodo.1411479},
      doi     = {10.5281/zenodo.1411479}
     }
