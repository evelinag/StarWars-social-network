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
      "Episode VII: The Force Awakens", "http://www.imsdb.com/scripts/Star-Wars-The-Force-Awakens.html"
    ]

// load film script from url
// script is either inside <td class="srctext"></td>
// or inside <pre></pre>
let getScriptElement (url:string) =
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

// Active pattern to parse the contents of the script
let (|SceneTitle|Name|Word|) (text:string) =
    let scenePattern = "[ 0-9]*(INT.|EXT.)[ A-Z0-9]"
    let namePattern = "^[/A-Z0-9]+[-]*[/A-Z0-9 ]*[-]*[/A-Z0-9 ]+$"
    if Regex.Match(text, scenePattern).Success then
        SceneTitle text
    elif Regex.Match(text, namePattern).Success then
        Name text
    else Word

let (|MultipleNames|_|) (text:string) =
    let namePattern = "[/A-Z0-9 -]+ *:"
    let results = Regex.Matches(text,namePattern)    
    if results.Count > 0 then
        let names = 
            [ for r in results -> r.Value.Trim(": ".ToCharArray())]
        Some names
    else 
        None

/// Recursively parse the script, extract the character names for each scene
let rec parseScenes sceneAcc characterAcc (items: string list) =
   match items with
   | item::rest ->
       match item with
       | SceneTitle title -> 
            // add the finished scene to the scene accumulator
            let fullScene = List.rev characterAcc
            parseScenes (fullScene::sceneAcc) [] rest
       | Name name -> 
            // add character's name to the character accumulator
            parseScenes sceneAcc (name::characterAcc) rest
       | Word -> // do nothing
            parseScenes sceneAcc characterAcc rest
   | [] -> List.rev sceneAcc        

/// Alternative function for parsing the script file

let getAlternativeNames text =
    match text with
    | MultipleNames names -> names
    | _ -> []

let rec splitAlternativeScript sceneAcc (sceneTitles: string list) (text:string) =
    match sceneTitles with
    | scene :: rest ->
        let idx = text.IndexOf(scene)
        let currentScene = text.[0..idx-1] 
        let characters = 
            match currentScene with 
            | MultipleNames names -> names
            | _ -> []
        splitAlternativeScript (characters::sceneAcc) rest text.[idx + scene.Length ..]
    | [] -> 
        // return the list of scenes, split into individual words
        List.rev sceneAcc 

let isSceneTitle (text:string) = text.Contains("INT.") || text.Contains("EXT.")

// let episode = getScriptElement htmlScripts.[2]  // <- problematic episodes: 0
// let episode = getScriptElement htmlScripts.[6]
// Parse the scripts
let getCharactersByScene episodeUrl = 
    let episodeHtml = getScriptElement episodeUrl
    // extract all script elements in bold
    // this works for MOST episodes
    let bItems = 
        episodeHtml.Elements("b") 
        |> List.map (fun x -> x.InnerText().Trim())

    let charactersByScene = 
        parseScenes [] [] bItems
        |> List.filter (fun characters -> not characters.IsEmpty)
        // this still contains some other stuff -> filter by list of characters

    let nSpeaking = List.concat charactersByScene |> List.length

    let characters = 
        // check if the characters were extracted correctly
        if (float nSpeaking)/(float bItems.Length) >= 0.2 then
            charactersByScene
        else
           // bItems contains just scene breaks - use them to split the screenplay
           // and extract the character names
           let text = episodeHtml.ToString()
           let sceneTitles = bItems |> List.filter isSceneTitle
           splitAlternativeScript [] sceneTitles text 
    characters
    |> List.map  (List.distinct >> Array.ofList)
    |> Array.ofList

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

// Some characters have multiple names - map their names onto pre-defined values
// specified in 'aliases.csv'
[<Literal>]
let aliasFile = __SOURCE_DIRECTORY__ + "/data/aliases.csv"
type Aliases = CsvProvider<aliasFile>

open Microsoft.FSharp.Reflection

/// Dictinary for translating character names between aliases
let aliasDict episodeIdx = 
    Aliases.Load(aliasFile).Rows 
    |> Seq.choose (fun row -> 
        // extract contents of a tuple as an array of obj values
        if (FSharpValue.GetTupleFields(row).[episodeIdx + 2]) :?> bool
        then Some (row.Alias, row.Name)
        else None)
    |> dict

let aliasesForEpisodes = Array.init scriptUrls.Length aliasDict

/// Some characters have multiple names - map their names onto pre-defined values
let mapName episodeIdx name = 
    if aliasesForEpisodes.[episodeIdx].ContainsKey(name) then 
        aliasesForEpisodes.[episodeIdx].[name] 
    else name

/// Script for Episode 7 contains a log of expressive terms that are not characters
let filterClutterTerms names =
    let expressions = ["S POV"; "CONTINUED"; "CUT TO"; "WIDE SHOT"; "HIM"] |> set
    names |> Array.filter (fun name -> not(expressions.Contains name))

/// Filter characters with names that also appear as general words in other episodes
let characterCheck episodeIdx name =
    match episodeIdx, name with
    | 6, "mace" -> false    // MACE = MACE WINDU vs MACE = MACE wielding stormtrooper
    | _ -> true


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
    | "REY" -> "#ffe0af"
    | "KYLO REN" -> "#000000"
    | "SNOKE" -> "#191970"
    | "FINN" -> "#07b19f"
    | "POE" -> "#a15bea"
    | "BB-8" -> "#eb5d00"
    | _ -> "#808080"