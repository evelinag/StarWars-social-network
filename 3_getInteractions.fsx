
#load "packages/FsLab/FsLab.fsx"

#load "parseScripts.fs"

open FSharp.Data
open System
open System.IO
open StarWars.ParseScripts
open System.Text.RegularExpressions

// Extract interactions between characters from the individual scripts
let characters = 
    let stdCharacters = File.ReadAllLines(__SOURCE_DIRECTORY__ + "/data/characters.csv") 
    let allNames =
        [| 0 .. scriptUrls.Length-1 |]
        |> Array.collect
            (fun ep -> 
                Seq.append aliasesForEpisodes.[ep].Keys aliasesForEpisodes.[ep].Values 
                |> Array.ofSeq)
    stdCharacters 
    |> Array.append allNames 
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
    let charactersByScene = getCharactersByScene url

    let namesInScenes = 
        charactersByScene
        |> Array.map (fun names -> 
            names |> Array.filter (fun name -> 
                        characters.Contains name && 
                        characterCheck episodeIdx name ))       

    // Create weighted network
    let nodes = 
        namesInScenes 
        |> Seq.collect id
        |> Seq.countBy id        
        |> Seq.filter (fun (name, count) -> count >= countThreshold)
    let nodeLookup = nodes |> Seq.map fst |> set

    let links = 
        namesInScenes 
        |> Array.collect (fun names -> 
            [| for i in 0..names.Length - 1 do 
                for j in i+1..names.Length - 1 do
                  let n1 = names.[i]
                  let n2 = names.[j]
                  if nodeLookup.Contains(n1) && nodeLookup.Contains(n2) then
                     yield min n1 n2, max n1 n2 |])
        |> Seq.countBy id
        |> Array.ofSeq

    nodes, links

let generateInteractionNetwork episodeIdx =     
    let nodes, links = getInteractionNetwork 0 episodeIdx
    File.WriteAllText(__SOURCE_DIRECTORY__ + "/networks/starwars-episode-" + string (episodeIdx + 1) + "-interactions.json",
        getJsonNetwork nodes links)

for i in 0..5 do generateInteractionNetwork i
generateInteractionNetwork 6

// =====================================================================
// Generate global network

let countThreshold = 0
let linkThreshold = 0
let nodes, links = 
    [0..6] |> List.map (getInteractionNetwork 0) |> List.unzip
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
// Adding BB-8 from Episode 7

[<Literal>]
let sampleNetwork = __SOURCE_DIRECTORY__ + "/networks/starwars-episode-1-mentions.json"
type Network = JsonProvider<sampleNetwork>

let getInteractionsForMissing allMissingCharacters allSimilarCharacters (episodeIdx: int option) =
    // load interactions and mentions
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

    let mentionsLookup = mentionsNetwork.Nodes |> Seq.mapi (fun i x -> x.Name, i) |> dict
    let mentionsLookup' = mentionsNetwork.Nodes |> Seq.mapi (fun i x -> i, x.Name) |> dict
    let getMentionsLinks name = 
        mentionsNetwork.Links 
        |> Array.filter (fun x -> 
            mentionsLookup.ContainsKey name && 
            (x.Source = mentionsLookup.[name] || x.Target = mentionsLookup.[name]))
        |> Array.map (fun x -> (mentionsLookup'.[x.Source], mentionsLookup'.[x.Target]), x.Value)

    // filter out characters that do not appear in the episode
    let missingCharacters, similarCharacters =  
        Array.zip allMissingCharacters allSimilarCharacters
        |> Array.filter (fun (ch, others) -> mentionsLookup.ContainsKey ch)
        |> Array.unzip

    // interactions: translate indices to names and vice versa
    let interactionsLookup = interactionNetwork.Nodes |> Seq.mapi (fun i x -> x.Name, i) |> dict
    let interactionsLookup' = interactionNetwork.Nodes |> Seq.mapi (fun i x -> i, x.Name) |> dict
    let getInteractionsLinks name = 
        interactionNetwork.Links 
        |> Seq.filter (fun x -> 
            interactionsLookup.ContainsKey name && 
             (x.Source = interactionsLookup.[name] || x.Target = interactionsLookup.[name]))
        |> Seq.map (fun x -> (interactionsLookup'.[x.Source], interactionsLookup'.[x.Target]), x.Value)

    // helper functions to get nodes for interactions and mentions for specific characters
    let getNameMentions name = mentionsNetwork.Nodes |> Seq.tryFind (fun x -> x.Name = name)
    let getNameInteractions name = interactionNetwork.Nodes |> Seq.tryFind (fun x -> x.Name = name)

    // compute link weights for each existing
    let linkWeights =
        interactionNetwork.Nodes
        |> Array.map (fun node -> node.Name) 
        |> Array.map (fun node -> 
            let mentionsLinks = 
                getMentionsLinks node
                |> Array.map (fun ((n,n'),w) -> if n = node then n',w else n,w)
                |> dict
            let interactionsLinks = getInteractionsLinks node 
            let weights = 
                [| for ((n1, n2),w) in interactionsLinks ->
                    let name' = if n1 = node then n2 else n1
                    let w' = if mentionsLinks.ContainsKey(name') then mentionsLinks.[name'] else 0
                    if w' > 0 then
                       name', (float w) / (float w') 
                    else 
                       name', (float w) |]
            if weights.Length > 0 then
                node, weights |> Array.averageBy snd
            else
                // character doesn't interact with any other character
                node, 0.0 )
        |> dict

    // take all mensions links from missing characters & scale them using the link weights
    // average link weights for links between missing characters
    let newNodes, newLinks, interactionRates =
        Array.zip missingCharacters similarCharacters
        |> Array.map (fun (missingCharacter, similar) -> 
            let similarLinks = similar |> Seq.map getInteractionsLinks |> Seq.concat |> dict
            let newLinks = 
                getMentionsLinks missingCharacter
                |> Array.map (fun ((n1, n2), count) -> 
                    // compute link weight for the 'missing' <--> 'other' link
                    let otherName = if n1 = missingCharacter then n2 else n1
                    let newWeight = 
                        if linkWeights.ContainsKey otherName then 
                            linkWeights.[otherName] * (float count) |> floor |> int 
                        else 0
                    (missingCharacter, otherName), newWeight)
                |> Array.filter (fun ((missing, other), w) ->
                    // to avoid spurious links, check if similar characters are linked with the 'other' node
                    let isSimilarLink = 
                        similar
                        |> Array.fold (fun isSim ch -> 
                            isSim || similarLinks.ContainsKey(ch, other) 
                            || similarLinks.ContainsKey(other,ch)
                            ) false
                    let isLinked = Array.contains other similar
                    isSimilarLink || isLinked )
                |> Array.filter (snd >> (<) 0)
            // add nodes for the missing characters
            let weight = 
                let similarWeight = 
                    similar 
                    |> Array.map (fun node -> 
                        let interactionWeight = 
                            match (getNameInteractions node) with
                            | Some n -> n.Value |> float
                            | None -> 0.0
                        let mentionsWeight = 
                            match (getNameMentions node) with 
                            | Some n -> n.Value |> float
                            | None -> 0.0
                        if mentionsWeight > 0.0 then 
                           interactionWeight/mentionsWeight
                        else 0.0)
                    |> Array.average    
                match getNameMentions missingCharacter with
                | Some n -> similarWeight * (float n.Value)
                | None -> 0.0
            let newNode = missingCharacter, weight |> floor |> int
            
            // Compute average interaction rate for the character from links
            let avgInteractionRate = 
                let mentions = getMentionsLinks missingCharacter
                let weightedLinks = 
                    newLinks
                    |> Array.collect (fun ((n1, n2), interactionW) ->
                        mentions |> Array.filter (fun ((n1', n2'), mentionW) -> n1' = n2 || n2' = n2)
                        |> Array.map (fun (_, mentionW) -> (float interactionW)/(float mentionW)))
                if weightedLinks.Length > 0 then 
                    Array.average weightedLinks
                else 0.0
            newNode, newLinks, avgInteractionRate)
        |> Array.unzip3

    // also add links between the non-speaking characters
    let nonspeakingLinks = 
        [| for ch1 in 0..missingCharacters.Length-1 do
            for ch2 in ch1+1..missingCharacters.Length-1 ->
                let mentionsW = 
                    let filteredMentions = 
                        getMentionsLinks missingCharacters.[ch1] 
                        |> Array.filter (fun ((n1, n2),_) -> n1 = missingCharacters.[ch2] || n2 = missingCharacters.[ch2])
                    if filteredMentions.Length > 0 then
                        filteredMentions |> Array.exactlyOne |> snd |> float
                    else 0.0
                let newWeight = 
                    List.average [
                        mentionsW * interactionRates.[ch1]
                        mentionsW * interactionRates.[ch2] ]
                    |> round |> int
                if missingCharacters.[ch1] < missingCharacters.[ch2] then
                    (missingCharacters.[ch1], missingCharacters.[ch2]), newWeight
                else
                    (missingCharacters.[ch2], missingCharacters.[ch1]), newWeight
                |]

    // update all nodes and all links
    let updatedNodes = 
        interactionNetwork.Nodes 
        |> Array.map (fun n -> n.Name, n.Value)
        |> Array.append newNodes
        |> Array.groupBy fst
        |> Array.map (fun (name, ws) -> name, ws |> Array.sumBy snd)
        |> Array.filter (snd >> (<) 0)
    let updatedLinks =
        interactionNetwork.Links
        |> Array.map (fun l -> (interactionsLookup'.[l.Source], interactionsLookup'.[l.Target]), l.Value)
        |> Array.append (Array.concat newLinks |> Array.map (fun ((n1, n2),w) -> if n1 < n2 then (n1,n2),w else (n2,n1),w))
        |> Array.append nonspeakingLinks
        |> Array.groupBy fst
        |> Array.map (fun ((n1, n2), ws) -> (n1, n2), ws |> Array.sumBy snd)
        |> Array.filter (snd >> (<) 0)

    updatedNodes, updatedLinks

let addSilentEncounters episodeIdx (nodes : (string*int)[] ,links) =
    match episodeIdx with
    | Some(7) -> 
        Array.append [| "LUKE", 1 |] nodes,
        Array.append links [| ("LEIA", "REY"), 1; ("LUKE", "REY"), 1|]
    | None -> nodes, Array.append links [| ("LEIA", "REY"), 1; ("LUKE", "REY"), 1|]
    | _ -> nodes, links


let allMissingCharacters = [|"R2-D2"; "CHEWBACCA"; "BB-8"|]
let allSimilarCharacters = [| [|"C-3PO"|]; [|"HAN"|]; [| "REY"; "FINN"; "POE" |] |]

let fillInteractionNetwork episodeIdx =     
    let nodes, links = 
        getInteractionsForMissing allMissingCharacters allSimilarCharacters (Some episodeIdx)
        |> addSilentEncounters (Some episodeIdx)
    File.WriteAllText(__SOURCE_DIRECTORY__ + "/networks/starwars-episode-" + string (episodeIdx) + "-interactions-allCharacters.json",
        getJsonNetwork nodes links)

for i in 1..7 do 
    printfn "%d" i
    fillInteractionNetwork i

let fullNodes, fullLinks = 
    getInteractionsForMissing allMissingCharacters allSimilarCharacters None
    |> addSilentEncounters None
File.WriteAllText(__SOURCE_DIRECTORY__ + "/networks/starwars-full-interactions-allCharacters.json",
    getJsonNetwork fullNodes fullLinks)

let mergeDarthVader (nodes, links) =
    let mergedNodes = 
       ("DARTH VADER",
        nodes |> Array.filter (fun (name, _) -> name = "ANAKIN" || name = "DARTH VADER")
        |> Array.sumBy snd)
    let mergedLinks = 
        links |> Array.filter (fun ((n1, n2), w) -> n1 = "ANAKIN" || n1 = "DARTH VADER" || n2 = "ANAKIN" || n2 = "DARTH VADER")
        |> Array.map (fun ((n1, n2), w) -> if n1 = "ANAKIN" || n1 = "DARTH VADER" then n2, w else n1, w)
        |> Array.groupBy fst
        |> Array.choose (fun (name, ws) -> 
            if name = "DARTH VADER" then None else
            let w = Array.sumBy snd ws
            if name < "DARTH VADER" then 
                Some ((name, "DARTH VADER"), w)
            else 
                Some (("DARTH VADER", name), w))
    let nodes' = 
        nodes 
        |> Array.filter (fun (name, _) -> name <> "ANAKIN" && name <> "DARTH VADER")
        |> Array.append [|mergedNodes|]
    let links' = 
        links 
        |> Array.filter (fun ((n1, n2), w) -> n1 <> "ANAKIN" && n1 <> "DARTH VADER" && n2 <> "ANAKIN" && n2 <> "DARTH VADER")
        |> Array.append mergedLinks
    nodes', links'        

// Additionally merge Anakin and Darth Vader
let fullNodes', fullLinks' = 
    getInteractionsForMissing allMissingCharacters allSimilarCharacters None
    |> addSilentEncounters None
    |> mergeDarthVader 
File.WriteAllText(__SOURCE_DIRECTORY__ + "/networks/starwars-full-interactions-allCharacters-merged.json",
    getJsonNetwork fullNodes' fullLinks')
