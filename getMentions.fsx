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
    |> Array.map (fun s -> s.ToLower())

// Some names occur also as a part of other words - check for that as well
// problematic names: Han, Sola
let containsName (scene:string) (name:string) = 
   scene.Contains(name) && (
     Regex.IsMatch(scene,"[^a-z]+" + name + "[^a-z]+") ||
     Regex.IsMatch(scene,"^" + name + "[^a-z]+") ||
     Regex.IsMatch(scene,"[^a-z]+" + name + "$"))

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
        |> Seq.map (fun ((n1, n2), value) -> 
            JsonValue.Record [| "source", JsonValue.Number (decimal nodeIdxs.[n1]); 
                "target", JsonValue.Number (decimal nodeIdxs.[n2]);
                "value", JsonValue.Number (decimal value)|])
        |> Array.ofSeq
        |> JsonValue.Array
    (JsonValue.Record [| "nodes", jsonNodes  ; "links", jsonLinks |]).ToString()

let getMentionsNetwork includeRobots countThreshold episodeIdx = 
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

    let interactions = 
        scenes
        |> List.map (fun scene -> 
            let lscene = 
                scene |> Array.map (fun s -> s.ToLower()) // some names contain lower-case characters
            characters
            |> Array.map (fun name -> 
                lscene 
                |> Array.map (fun contents -> if containsName contents name then Some name else None )
                |> Array.choose id)
            |> Array.concat
            |> Array.choose (fun name -> 
                let newName = mapName (name.ToUpper())
                if includeRobots then Some newName
                elif newName = "R2-D2" || newName = "C-3PO" then None else Some newName)
            |> Seq.distinct
            |> Seq.toArray )
        |> List.filter (Array.isEmpty >> not)

    // Create weighted network
    let nodes = 
        interactions 
        |> Seq.collect id
        |> Seq.countBy id        
        |> Seq.filter (fun (name, count) -> count >= countThreshold)
    let nodeLookup = nodes |> Seq.map fst |> set

    let links = 
        interactions 
        |> List.collect (fun sceneNames -> 
            [ for i in 0..sceneNames.Length - 1 do 
                for j in i+1..sceneNames.Length - 1 do
                  let n1 = sceneNames.[i]
                  let n2 = sceneNames.[j]
                  if nodeLookup.Contains(n1) && nodeLookup.Contains(n2) then
                     yield min n1 n2, max n1 n2 ])
        |> Seq.countBy id
        |> Array.ofSeq

    nodes, links

let generateMentionsNetwork episodeIdx =     
    let nodes, links = getMentionsNetwork true 0 episodeIdx
    File.WriteAllText(__SOURCE_DIRECTORY__ + "/networks/starwars-episode-" + string (episodeIdx + 1) + "-mentions.json",
        getJsonNetwork nodes links)

for i in 0..5 do generateMentionsNetwork i

// =====================================================================
// Generate global network

let includeRobots = true
let countThreshold = 0
let linkThreshold = 0
let nodes, links = 
    [0..5] |> List.map (getMentionsNetwork includeRobots 0) |> List.unzip
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

File.WriteAllText(__SOURCE_DIRECTORY__ + "/networks/starwars-full-mentions.json",
    getJsonNetwork allNodes allLinks)

// ===============================================================
// Get timelines of mentions for each character

/// Get indices of scenes where individual characters appear (where they are mentioned).
let getSceneAppearances episodeIdx = 
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
    let totalScenes = scenes.Length

    scenes
    |> List.mapi (fun sceneIdx scene -> 
        let lscene = 
            scene |> Array.map (fun s -> s.ToLower()) // some names contain lower-case characters
        characters
        |> Array.map (fun name -> 
            lscene 
            |> Array.map (fun contents -> if containsName contents name then Some name else None )
            |> Array.choose id)
        |> Array.concat
        |> Array.map (fun name -> mapName (name.ToUpper()))
        |> Seq.distinct 
        |> Seq.map (fun name -> sceneIdx, name)
        |> List.ofSeq)
    |> List.collect id,
    totalScenes

let appearances = 
    [0 .. 5]
    |> List.map getSceneAppearances
    |> List.mapi (fun episodeIdx (sceneAppearances, total) ->
        sceneAppearances 
        |> List.map (fun (scene, name) -> 
            float episodeIdx + float scene / float total, name))
    |> List.collect id
    |> Seq.groupBy snd
    |> Seq.map (fun (name, inScenes) -> name, inScenes |> Seq.map fst |> Array.ofSeq)       
    |> Array.ofSeq  

// Save appearances as pseudo-csv file
let appearancesFilename = __SOURCE_DIRECTORY__ + "/data/charactersPerScene.csv"
let appearsString =
    appearances
    |> Array.map (fun (name, appears) -> 
        appears |> Array.map string |> Array.append [|name|] |> String.concat ",")
File.WriteAllLines(appearancesFilename, appearsString)
    