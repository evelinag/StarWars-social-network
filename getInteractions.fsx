
#load "packages/FsLab/FsLab.fsx"

#load "parseScripts.fs"

open FSharp.Data
open System
open System.IO
open StarWars.ParseScripts
open System.Text.RegularExpressions

// Extract interactions between characters from the individual scripts
let characters = 
    File.ReadAllLines(__SOURCE_DIRECTORY__ + "/data/characters.csv") 
    |> Array.append (Seq.append aliasDict.Keys aliasDict.Values |> Array.ofSeq)
    |> set

/// Create JSON network
let getJsonNetwork nodes links =
    let jsonNodes = 
        nodes
        |> Seq.map (fun (name, count) -> 
            JsonValue.Record 
                [| "name", JsonValue.String name; 
                   "value", JsonValue.Number (decimal (count+1))
                   "colour", JsonValue.String (getCharacterColour name)|] )
        |> Array.ofSeq
        |> JsonValue.Array
    // translate links from names to idxs
    let nodeIdxs = Seq.mapi (fun i (name, count) -> name, i) nodes |> dict
    let jsonLinks = 
        links
        |> Seq.choose (fun ((n1, n2), value) -> 
            if nodeIdxs.ContainsKey(n1) && nodeIdxs.ContainsKey(n2) then
              JsonValue.Record [| 
                "source", JsonValue.Number (decimal nodeIdxs.[n1]); 
                "target", JsonValue.Number (decimal nodeIdxs.[n2]);
                "value", JsonValue.Number (decimal value)|] |> Some
            else None)
        |> Array.ofSeq
        |> JsonValue.Array
    (JsonValue.Record [| "nodes", jsonNodes  ; "links", jsonLinks |]).ToString()

let getInteractionNetwork countThreshold episodeIdx = 
    let episode, url = scriptUrls.[episodeIdx]
    let script = getScript url
    let scriptParts = script.Elements()

    let mainScript = 
        scriptParts
        |> Seq.map (fun element -> element.ToString())
        |> Seq.toArray

    // Now every element of the list is a single scene
    let scenes = 
        splitByScene mainScript [] |> List.rev

    let namesInScenes = 
        scenes 
        |> List.map getCharacterNames
        |> List.map (fun names -> names |> Array.filter (fun n -> characters.Contains n))       

    // Create weighted network
    let nodes = 
        namesInScenes 
        |> Seq.collect id
        |> Seq.countBy id        
        |> Seq.filter (fun (name, count) -> count >= countThreshold)
    let nodeLookup = nodes |> Seq.map fst |> set

    let links = 
        namesInScenes 
        |> List.collect (fun names -> 
            [ for i in 0..names.Length - 1 do 
                for j in i+1..names.Length - 1 do
                  let n1 = names.[i]
                  let n2 = names.[j]
                  if nodeLookup.Contains(n1) && nodeLookup.Contains(n2) then
                     yield min n1 n2, max n1 n2 ])
        |> Seq.countBy id
        |> Array.ofSeq

    nodes, links

let generateInteractionNetwork episodeIdx =     
    let nodes, links = getInteractionNetwork 0 episodeIdx
    File.WriteAllText(__SOURCE_DIRECTORY__ + "/networks/starwars-episode-" + string (episodeIdx + 1) + "-interactions.json",
        getJsonNetwork nodes links)

for i in 0..5 do generateInteractionNetwork i

// =====================================================================
// Generate global network

let countThreshold = 0
let linkThreshold = 0
let nodes, links = 
    [0..5] |> List.map (getInteractionNetwork 0) |> List.unzip
let summarizeEpisodes data =
    data 
    |> Seq.collect id 
    |> Seq.groupBy fst
    |> Seq.map (fun (name, episodeCounts) -> name, episodeCounts |> Seq.sumBy snd)

let allNodes = 
    summarizeEpisodes nodes 
    |> Seq.filter (fun (name, count) -> count >= countThreshold)
let nodeLookup = allNodes |> Seq.map fst |> set

let allLinks = 
    summarizeEpisodes links
    |> Seq.filter (fun ((n1, n2), _) -> nodeLookup.Contains(n1) && nodeLookup.Contains(n2))
    |> Seq.filter (fun ((n1, n2), count) -> count > linkThreshold)

File.WriteAllText(__SOURCE_DIRECTORY__ + "/networks/starwars-full-interactions.json",
    getJsonNetwork allNodes allLinks)

// =====================================================================
// Add R2-D2 and Chewbacca manually from the network of mentions
[<Literal>]
let sampleNetwork = __SOURCE_DIRECTORY__ + "/networks/starwars-episode-1-mentions.json"
type Network = JsonProvider<sampleNetwork>

let missingCharacters = [|"R2-D2"; "CHEWBACCA"|]
let similarCharacters = [|"C-3PO"; "HAN"|]

let addMissing missingCharacters similarCharacters (episodeIdx : int option) = 
    let mentionsNetwork, interactionNetwork = 
        match episodeIdx with
        | Some(i) ->
          Network.Load(
            __SOURCE_DIRECTORY__ + "/networks/starwars-episode-" + string i + "-mentions.json"),
          Network.Load(
            __SOURCE_DIRECTORY__ + "/networks/starwars-episode-" + string i + "-interactions.json")
        | None -> 
          Network.Load(__SOURCE_DIRECTORY__ + "/networks/starwars-full-mentions.json"),
          Network.Load(__SOURCE_DIRECTORY__ + "/networks/starwars-full-interactions.json")

    let getNameMentions name = mentionsNetwork.Nodes |> Seq.find (fun x -> x.Name = name)
    let getNameInteractions name = interactionNetwork.Nodes |> Seq.find (fun x -> x.Name = name)

    let mentionsLookup = mentionsNetwork.Nodes |> Seq.mapi (fun i x -> x.Name, i) |> dict
    let mentionsLookup' = mentionsNetwork.Nodes |> Seq.mapi (fun i x -> i, x.Name) |> dict
    let getMentionsLinks name = 
        mentionsNetwork.Links 
        |> Seq.filter (fun x -> x.Source = mentionsLookup.[name] || x.Target = mentionsLookup.[name])
        |> Seq.map (fun x -> (mentionsLookup'.[x.Source], mentionsLookup'.[x.Target]), x.Value)

    let interactionsLookup = interactionNetwork.Nodes |> Seq.mapi (fun i x -> x.Name, i) |> dict
    let interactionsLookup' = interactionNetwork.Nodes |> Seq.mapi (fun i x -> i, x.Name) |> dict
    let getInteractionsLinks name = 
        interactionNetwork.Links 
        |> Seq.filter (fun x -> x.Source = interactionsLookup.[name] || x.Target = interactionsLookup.[name])
        |> Seq.map (fun x -> (interactionsLookup'.[x.Source], interactionsLookup'.[x.Target]), x.Value)

    let missing, similar = 
        Array.zip missingCharacters similarCharacters
        |> Array.filter (fun (n1, n2) -> mentionsLookup.ContainsKey(n1) && mentionsLookup.ContainsKey(n2))
        |> Array.unzip
    let missingNodes, missingLinks = 
        missing 
        |> Array.filter (fun name -> mentionsLookup.ContainsKey(name))
        |> Array.map (fun name -> 
            getNameMentions name, getMentionsLinks name |> Seq.toArray) 
        |> Array.unzip
    let similarNodes, similarLinks = 
        similar  
        |> Array.map (fun name -> 
            getNameMentions name, getMentionsLinks name |> Seq.toArray) 
        |> Array.unzip
    let interactionNodes, interactionLinks = 
        similar 
        |> Array.map (fun name -> 
            getNameInteractions name, getInteractionsLinks name |> Seq.toArray)
        |> Array.unzip

    // Scale size of node in the interactions network
    let newNodes = 
        Array.zip3 missingNodes similarNodes interactionNodes 
        |> Array.map (fun (missing, similar, similar') -> 
            let weight = (float missing.Value)/(float similar.Value) * (float similar'.Value)
            missing.Name, weight |> round |> int)
    
    // Scale links in the interaction network
    let newLinks =
        Array.zip3 missingLinks similarLinks interactionLinks
        |> Array.collect (fun (missing, similar, similar') ->
            let scaling = 
                [ for ((n1, n2), w) in similar -> 
                    let _,w' = 
                        defaultArg 
                          (similar' |> Array.tryFind (fun ((n1',n2'),w') -> n1' = n1 && n2' = n2))
                          ((n1, n2), 0) 
                    (float w')/(float w) ]
                |> List.filter ((<) 0.0)
                |> List.average
            missing 
            |> Array.map (fun ((n1, n2),w) -> 
                (n1, n2), (float w)*scaling |> floor |> int)
            |> Array.filter (fun (_,w) -> w > 0))    

    let updatedNodes = 
        interactionNetwork.Nodes 
        |> Seq.map (fun n -> n.Name, n.Value)
        |> Seq.append newNodes
    let updatedLinks =
        interactionNetwork.Links
        |> Seq.map (fun l -> (interactionsLookup'.[l.Source], interactionsLookup'.[l.Target]), l.Value)
        |> Seq.append newLinks
    updatedNodes, updatedLinks

let fillInteractionNetwork episodeIdx =     
    let nodes, links = addMissing missingCharacters similarCharacters (Some episodeIdx)
    File.WriteAllText(__SOURCE_DIRECTORY__ + "/networks/starwars-episode-" + string (episodeIdx) + "-interactions-allCharacters.json",
        getJsonNetwork nodes links)

for i in 1..6 do 
    printfn "%d" i
    fillInteractionNetwork i

let fullNodes, fullLinks = addMissing missingCharacters similarCharacters None
File.WriteAllText(__SOURCE_DIRECTORY__ + "/networks/starwars-full-interactions-allCharacters.json",
    getJsonNetwork fullNodes fullLinks)