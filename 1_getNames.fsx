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
  |> List.mapi (fun episodeIdx (episode, url) ->
      getCharactersByScene url
      |> Array.concat)
  |> Array.concat
  |> filterClutterTerms
  |> Array.countBy id
  |> Seq.filter (snd >> (<) 1)  // filter out characters that speak in only one scene

for (name, count) in allNames do printfn "%s - %d" name count

// Now follows a manual step - filter out names that are not actual names of characters
// such as "PILOT" or "GUARD"

// Print the selected character names
let characters = File.ReadAllLines(__SOURCE_DIRECTORY__ + "/data/characters.csv")
characters |> Array.sort |> Array.iter (printfn "%s")

