open System
open System.IO

#load "packages/FsLab/FsLab.fsx"
open FSharp.Data
open RDotNet
open RProvider
open RProvider.ggplot2
open RProvider.datasets
open Deedle

#load "paket-files/evelinag/ffplot/ggplot.fs"
open ggplot

#load "parseScripts.fs"
open StarWars.ParseScripts

// Visualize character mentions using violin plot from ggplot2

let appearancesFilename = __SOURCE_DIRECTORY__ + "/data/charactersPerScene.csv"

let charactersToVisualize = 
  [| "HAN" ;
     "LEIA"
     "LUKE"
     "CHEWBACCA";
     "DARTH VADER"
     "C-3PO"
     "ANAKIN"
     "R2-D2"
     "PADME"
     "EMPEROR"
     "YODA"
     "OBI-WAN"
     |] 
let selectedCharacters = set charactersToVisualize

type Appearance = {Name: string; Time: float}

// Extract the character mentions
let characterAppearances =
    File.ReadAllLines(appearancesFilename)
    |> Array.map (fun line -> line.Split([|','|]))
    |> Array.filter (fun line -> selectedCharacters.Contains(line.[0]) )
    |> Array.collect (fun line -> 
        let name = line.[0]
        line.[1..] |> Array.map float |> Array.map (fun x -> {Name = name; Time = x}))

// Get colours for each character
let colours = 
  R.eval(R.parse(text=
    "c(" + ( charactersToVisualize 
             |> Seq.map (fun name -> name, getCharacterColour name)
             |> Seq.map (fun (name, colour) -> "\"" + name + "\" = \"" + colour + "\"")
             |> String.concat "," )
    + ")" ))

// Create an R data frame and specify ordering of Name factors 
let df = Frame.ofRecords(characterAppearances)
R.assign("df", R.as_data_frame(df))
R.eval(R.parse(text=("df$Name <- factor(df$Name, levels = c(\"" + (String.concat "\",\"" charactersToVisualize) + "\"))")))
R.eval(R.parse(text="library(ggplot2)"))

// Massive ggplot!!!
R.eval(R.parse(text="ggplot(df, aes(x=Name,y=Time,fill=Name,colour=Name))")) 
++ R.geom__hline(namedParams["yintercept", box [0..6]; "size", box 2; "colour", box "#e2e2e2"])
++ R.geom__violin(
    namedParams[
        "adjust", box 0.05;
        ])
++ R.coord__flip()
++ R.scale__fill__manual(namedParams["values", colours])
++ R.scale__colour__manual(namedParams["values", colours])
++ R.theme__bw()
++ R.theme( 
    namedParams[
        "legend.position", box "none"
        "panel.border", R.element__blank() |> box
        "panel.grid.minor", R.element__blank() |> box
        "axis.ticks", R.element__blank() |> box 
        "panel.grid.major.y", R.element__line(namedParams[ "size", box 1; "color", box "#e2e2e2"] ) |> box 
        "panel.grid.major.x", R.element__blank() |> box 
        "axis.title.x", R.element__blank() |> box
        "axis.title.y", R.element__blank() |> box
        "axis.text", R.element__text(namedParams["size", 16]) |> box
        ])
++ R.scale__y__continuous(
    namedParams [
        "breaks", box [0.5; 1.5; 2.5; 3.5; 4.5; 5.5]
        "labels", box ["Episode I"; "Episode II"; "Episode III"; "Episode IV"; "Episode V"; "Episode VI" ]])

