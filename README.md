# The Star Wars social network

This repository contains the code I used to create my blog post on the 
[Star Wars social network](http://evelinag.com/blog/2015/12-15-star-wars-social-network/index.html).

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
   
The folder `networks` contains:

* `starwars-episode-N-interactions.json` contains the social network extracted from Episode N, where the links between characters are
defined by the times the characters speak within the same scene.
* `starwars-episode-N-mentions.json` contains the social network extracted from Episode N, where the links between characters are
defined by the times the characters are mentioned within the same scene.
* `starwars-episode-N-interactions-allCharacters.json` is the `interactions` network with R2-D2 and Chewbacca added in using 
data from `mentions` network.
* `starwars-full-...` contain the corresponding social networks for the whole set of 6 episodes.


The other files contain the code used to generate the data files and the plots.
* `interactions.html` and `episode-interactions.html` contain the D3.js code to visualize the networks.
* The rest of the code is in F# and uses [FsLab](http://fslab.org/)
* To run the code, use [paket reference manager](http://fsprojects.github.io/Paket/) to download all the dependencies
