open System
open System.IO

#load "packages/FsLab/FsLab.fsx"
open FSharp.Data
open RDotNet
open RProvider
open RProvider.ggplot2
open RProvider.datasets

// =========================================
// Compute graph statistics

open RProvider.igraph

let [<Literal>] linkFile =
    __SOURCE_DIRECTORY__ + "/networks/starwars-episode-1-interactions.json"
type Network = JsonProvider<linkFile>

//let file = __SOURCE_DIRECTORY__ + "/networks/starwars-episode-6-interactions-allCharacters.json"
let file = __SOURCE_DIRECTORY__ + "/networks/starwars-full-interactions-allCharacters.json"
let nodes = 
    Network.Load(file).Nodes 
    |> Seq.map (fun node -> node.Name) |> Array.ofSeq
let nodeLookup = nodes |> Array.mapi (fun i name -> i, name) |> dict
let links = Network.Load(file).Links

let mergeAnakin = true
let graph =
    let edges = 
        links
        |> Array.collect (fun link ->
            let n1 = nodeLookup.[link.Source]
            let n2 = nodeLookup.[link.Target]
            if mergeAnakin then 
                if n1 = "ANAKIN" then [| "DARTH VADER"; n2 |]
                elif n2 = "ANAKIN" then [| n1; "DARTH VADER" |]
                else
                 [| n1 ; n2 |]
            else
                 [| n1 ; n2 |] )
    namedParams["edges", box edges; "dir", box "undirected"]
    |> R.graph

// Compute betweenness centrality    
// Betweenness = ratio of number of shortest paths from all vertices to all
//               others that pass through that node
let centrality = R.betweenness(graph)
let names = R.names(centrality).AsCharacter().ToArray()
let centralityValues = centrality.AsNumeric().ToArray()

// Compute degree centrality
// Degree centrality = number of other nodes connected to the node 
let degreeCentrality = R.degree(graph)
let names' = R.names(degreeCentrality).AsCharacter().ToArray()
let degreeValues = degreeCentrality.AsNumeric().ToArray() 

let top5_betweenness = 
    Array.zip names centralityValues
    |> Array.sortBy (fun (_,n) -> -n)
    |> Seq.take 5

let top5_degree = 
    Array.zip names' degreeValues
    |> Array.sortBy (fun (_,n) -> -n)
    |> Seq.take 5

let printMarkdownTable measureName top5 = 
    printfn "|\t| Name | %s |" measureName
    printfn "|---|-----|-----|"
    top5 |> Seq.iteri (fun i (name, (value : float)) ->  
        if measureName = "Degree" then printfn "| %d. | %s | %d |" (i+1) name (int value)
        else printfn "%d. | %s | %.1f |" (i+1) name value)

printMarkdownTable "Degree" top5_degree
printMarkdownTable "Betweenness" top5_betweenness

// Look at shortest path between two nodes
R.shortest__paths(namedParams["graph", box graph; "from", box "GREEDO"; "to", box "LEIA"])

