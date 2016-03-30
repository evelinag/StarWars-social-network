open System
open System.IO

#load "packages/FsLab/FsLab.fsx"
open FSharp.Data
open RDotNet
open RProvider
open RProvider.ggplot2
open RProvider.datasets
open RProvider.igraph

// =========================================
// Compute graph statistics

let [<Literal>] linkFile =
    __SOURCE_DIRECTORY__ + "/networks/starwars-episode-1-interactions.json"
type Network = JsonProvider<linkFile>

let file = __SOURCE_DIRECTORY__ + "/networks/starwars-episode-7-interactions-allCharacters.json"
//let file = __SOURCE_DIRECTORY__ + "/networks/starwars-full-interactions-allCharacters.json"
let nodes = 
    Network.Load(file).Nodes 
    |> Seq.map (fun node -> node.Name) |> Array.ofSeq
let nodeLookup = nodes |> Array.mapi (fun i name -> i, name) |> dict
let links = Network.Load(file).Links

let mergeAnakin = true
let graph =
    let edges = 
        links
        |> Array.map (fun link ->
            let n1 = nodeLookup.[link.Source]
            let n2 = nodeLookup.[link.Target]
            if mergeAnakin then 
                if n1 = "ANAKIN" then 
                    if "DARTH VADER" < n2 then [| "DARTH VADER"; n2 |] else [| n2; "DARTH VADER" |]
                elif n2 = "ANAKIN" then 
                    if "DARTH VADER" < n1 then [| "DARTH VADER"; n1 |] else [| n1; "DARTH VADER" |]
                else
                    [| n1; n2 |]
            else
                  [| n1; n2 |] )
        |> Array.distinct   // discard with duplicated edges
        |> Array.concat
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

let top_betweenness k = 
    Array.zip names centralityValues
    |> Array.sortByDescending snd
    |> Array.take k

let top_degree k = 
    Array.zip names' degreeValues
    |> Array.sortByDescending snd
    |> Array.take k

let printMarkdownTable measureName top5 = 
    printfn "|\t| Name | %s |" measureName
    printfn "|---|-----|-----|"
    top5 |> Array.iteri (fun i (name, (value : float)) ->  
        if measureName = "Degree" then printfn "| %d. | %s | %d |" (i+1) name (int value)
        else printfn "%d. | %s | %.1f |" (i+1) name value)

printMarkdownTable "Degree" (top_degree 5)
printMarkdownTable "Betweenness" (top_betweenness 5)

// Look at shortest path between two nodes
R.shortest__paths(namedParams["graph", box graph; "from", box "GREEDO"; "to", box "LEIA"])

//=====================================================================
// Compare graph density between the episodes

let densities, transitivity, nodeCounts = 
    [| for episodeIdx in 1..7 ->
        let file = __SOURCE_DIRECTORY__ + "/networks/starwars-episode-" + string episodeIdx + "-interactions-allCharacters.json"
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

        let clust : float = R.transitivity(graph, "undirected").GetValue()
        let density : float = R.graph_density(graph).GetValue()

        density, clust, nodes.Length 
    |]
    |> Array.unzip3

open XPlot.GoogleCharts

// Plot the number of characters in each episode
let options =
    Options(
        title = "Number of characters",
        hAxis = Axis(
            title = "Number of characters", 
            viewWindowMode = "explicit", 
            viewWindow = ViewWindow(min = 0, max = 40)),
        colors = [|"#3bc4c4"|]
    )

nodeCounts
|> Array.mapi (fun i c -> "Episode " + string (i+1), c)
|> Chart.Bar
|> Chart.WithOptions(options)

// Plot the clustering coefficient of each episode
let options2 =
    Options(
        title = "Clustering coefficient (transitivity)",
        hAxis = Axis(
            title = "Clustering coefficient")
    )

transitivity
|> Array.mapi (fun i c -> "Episode " + string (i+1), c)
|> Chart.Bar
|> Chart.WithOptions(options2)

// Plot the density of each network
let options3 =
    Options(
        title = "Network density",
        hAxis = Axis(title = "Density (%)", 
            viewWindowMode = "explicit", 
            viewWindow = ViewWindow(min = 5, max = 18)),
        colors = [|"#3bc4c4"|]
    )

densities
|> Array.mapi (fun i c -> "Episode " + string (i+1), c * 100.0 )
|> Chart.Bar
|> Chart.WithOptions(options3)
