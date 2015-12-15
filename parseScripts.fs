module StarWars.ParseScripts

open FSharp.Data
open System
open System.IO
open System.Text.RegularExpressions

let scriptUrls = 
    [ "Episode I: The Phantom Menace", "http://www.imsdb.com/scripts/Star-Wars-The-Phantom-Menace.html"
      "Episode II: Attack of the Clones", "http://www.imsdb.com/scripts/Star-Wars-Attack-of-the-Clones.html"
      "Episode III: Revenge of the Sith", "http://www.imsdb.com/scripts/Star-Wars-Revenge-of-the-Sith.html"
      "Episode IV: A New Hope", "http://www.imsdb.com/scripts/Star-Wars-A-New-Hope.html"
      "Episode V: The Empire Strikes Back", "http://www.imsdb.com/scripts/Star-Wars-The-Empire-Strikes-Back.html"
      "Episode VI: Return of the Jedi", "http://www.imsdb.com/scripts/Star-Wars-Return-of-the-Jedi.html"
    ]

// load film script from url
// script is either inside <td class="srctext"></td>
// or inside <pre></pre>
let getScript (url:string) =
    let extract_td (node:HtmlNode) =
        if node.Name() <> "td" then false
        else 
            match node.TryGetAttribute "class" with
            | None -> false
            | Some(t) -> t.Value() = "scrtext"
    let scriptPage = 
        HtmlDocument.Load(url).Descendants("pre")
    if Seq.isEmpty scriptPage then
        HtmlDocument.Load(url).Descendants(extract_td) |> Seq.head
    else scriptPage |> Seq.head

// split the script by scene
// each scene starts with either INT. or EXT. 
let rec splitByScene (script : string[]) scenes =
    let scenePattern = "<b>[ 0-9]*(INT.|EXT.)"
    let idx = 
        script 
        |> Seq.tryFindIndex (fun line -> Regex.Match(line, scenePattern).Success)
    match idx with
    | Some i ->
        let remainingScenes = script.[i+1 ..]
        let currentScene = script.[0..i-1]
        splitByScene remainingScenes (currentScene :: scenes)
    | None -> script :: scenes

// Extract names of characters that speak in scenes. 
// A) Extract names of characters in the format "[name]:"
let getFormat1Names text =
    let pattern = "[/A-Z0-9 -]+ *:"   // we need '-' for Obi-Wan etc
    let matches = Regex.Matches(text, pattern)
    let names = 
        seq { for m in matches -> m.Value }
        |> Seq.map (fun name -> name.Trim([|' '; ':'|]))
        |> Array.ofSeq
    names

// B) Extract names of characters in the format "<b> [name] </b>"
let getFormat2Names text =
    let pattern = "<b>[ ]*[/A-Z0-9 -]+[ ]*</b>"
    let m = Regex.Match(text, pattern)
    if m.Success then
        let name = m.Value.Replace("<b>","").Replace("</b>","").Trim()
        [| name |]
    else [||]

// Some characters have multiple names - map their names onto pre-defined values
// specified in 'aliases.csv'
[<Literal>]
let aliasFile = __SOURCE_DIRECTORY__ + "/data/aliases.csv"
type Aliases = CsvProvider<aliasFile>

/// Dictinary for translating character names between aliases
let aliasDict = 
    Aliases.Load(aliasFile).Rows 
    |> Seq.map (fun row -> row.Alias, row.Name)
    |> dict

/// Some characters have multiple names - map their names onto pre-defined values
let mapName name = if aliasDict.ContainsKey(name) then aliasDict.[name] else name

/// Extract character names from the given scene
let getCharacterNames (scene: string []) =
    let names1 = scene |> Seq.collect getFormat1Names 
    let names2 = scene |> Seq.collect getFormat2Names 
    Seq.append names1 names2
    |> Seq.map mapName
    |> Seq.distinct
    |> Array.ofSeq

///==============================================================================

/// Add colours to specific characters

let getCharacterColour name =
    match name with
    | "ANAKIN" -> "#ce3b59"
    | "DARTH VADER" -> "#000000"
    | "LUKE" -> "#3881e5"
    | "OBI-WAN" -> "#48D1CC"
    | "C-3PO" -> "#FFD700"
    | "R2-D2" -> "#bde0f6"
    | "CHEWBACCA" -> "#A0522D"
    | "HAN" -> "#ff9400"
    | "LEIA" -> "#DCDCDC"
    | "QUI-GON" -> "#4f4fb1"
    | "EMPEROR" -> "#191970"
    | "YODA" -> "#9ACD32"
    | "PADME" -> "#DDA0DD"
    | "JAR JAR" -> "#9a9a00"
    | _ -> "#808080"