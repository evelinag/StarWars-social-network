#load "packages/FsLab/FsLab.fsx"

#load "parseScripts.fs"

open FSharp.Data
open System
open System.IO
open StarWars.ParseScripts

// ===========================================================================
// Extract character names from the scripts

let allNames =
  scriptUrls
  |> List.map (fun (episode, url) ->
    let script = getScript url
    let scriptParts = script.Elements()

    let mainScript = 
        scriptParts
        |> Seq.map (fun element -> element.ToString())
        |> Seq.toArray

    // Now every element of the list is a single scene
    let scenes = splitByScene mainScript [] 

    // Extract names appearing in each scene
    scenes |> List.map getCharacterNames |> Array.concat )
  |> Array.concat
  |> Seq.countBy id
  |> Seq.filter (snd >> (<) 1)  // filter out characters that speak in only one scene

for (name, count) in allNames do printfn "%s - %d" name count

// Now follows a manual step - filter out names that are not actual names of characters
// such as "PILOT" or "GUARD"

// Print the selected character names
let characters = File.ReadAllLines(__SOURCE_DIRECTORY__ + "/data/characters.csv")
characters |> Array.sort |> Array.iter (printfn "%s")

