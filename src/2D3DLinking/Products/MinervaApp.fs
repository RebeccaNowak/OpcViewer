﻿namespace PRo3D.Minerva

open System
open System.Net.Sockets

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Rendering.Text 
open Aardvark.Geometry
open Aardvark.SceneGraph
open Aardvark.UI
open Aardvark.UI.Primitives

open PRo3D.Base
open PRo3D.Minerva

open KdTreeHelper

module MinervaApp =  

    let instrumentText (instr : Instrument) =
        match instr with
        | Instrument.MAHLI          -> "MAHLI"
        | Instrument.FrontHazcam    -> "FrontHazcam"
        | Instrument.Mastcam        -> "Mastcam"
        | Instrument.APXS           -> "APXS"
        | Instrument.FrontHazcamR   -> "FrontHazcamR"
        | Instrument.FrontHazcamL   -> "FrontHazcamL"
        | Instrument.MastcamR       -> "MastcamR"    
        | Instrument.MastcamL       -> "MastcamL"
        | Instrument.ChemLib        -> "ChemLib"    
        | Instrument.ChemRmi        -> "ChemRmi"
        | Instrument.NotImplemented -> "not impl. yet"
        | _ -> instr |> sprintf "unknown instrument identifier %A" |> failwith
    
    let writeLinesToFile path (contents : list<string>) =
        System.IO.File.WriteAllLines(path, contents)
    
    let sendMessage2Visplore (address) (port) (message : string) =
        let client = new TcpClient(address, port);
        let data = System.Text.Encoding.ASCII.GetBytes(message)
        let stream = client.GetStream()
        stream.Write(data, 0, data.Length)
        
        printfn "Sending message: %A" message
            
        stream.Close()
        client.Close()        
        
    let constructFilterByInstrument (filter:PRo3D.Minerva.QueryModel) =
        let mutable cqlStringInst = []
        if filter.checkMAHLI then cqlStringInst <- (cqlStringInst @ ["MAHLI"])
        if filter.checkAPXS then cqlStringInst <- (cqlStringInst @ ["APXS"])
        if filter.checkFrontHazcamR then cqlStringInst <- (cqlStringInst @ ["FHAZ_RIGHT_B"])
        if filter.checkFrontHazcamL then cqlStringInst <- (cqlStringInst @ ["FHAZ_LEFT_B"])
        if filter.checkMastcamR then cqlStringInst <- (cqlStringInst @ ["MAST_RIGHT"])
        if filter.checkMastcamL then cqlStringInst <- (cqlStringInst @ ["MAST_LEFT"])
        if filter.checkChemLib then cqlStringInst <- (cqlStringInst @ ["CHEMCAM_LIBS"])
        if filter.checkChemRmi then cqlStringInst <- (cqlStringInst @ ["CHEMCAM_RMI"])
        let groupString = cqlStringInst |> String.concat("','")
        sprintf @"(instrumentId IN ('" + groupString + "'))"
    
    let constructFilterById (filter:QueryModel) (ids:list<string>)  = 
        let groupString = ids |> String.concat("','")
        //let cql1 = [("cql", @"(identifier IN ('" + groupString + "'))")] |> HMap.ofList
        let cqlStringSol = sprintf @"(planetDayNumber >= %f AND planetDayNumber <= %f)"  filter.minSol.value filter.maxSol.value
        let cqlStringInst = constructFilterByInstrument filter
        let cqlStringAll = sprintf @"(" + cqlStringSol + " AND " + cqlStringInst + ")"
        let cql = [("cql", cqlStringSol)] |> HMap.ofList
        (@"https://minerva.eox.at/opensearch/collections/all/json/", cql)   
              
    let shuffleR (r : Random) xs = xs |> List.sortBy (fun _ -> r.Next())
    
    let everyNth n elements =
        elements
            |> List.mapi (fun i e -> if i % n = n - 1 then Some(e) else None)
            |> List.choose id

    let private updateSolLabels (features:plist<Feature>) (position : V3d) = 
        if features |> PList.isEmpty then
            HMap.empty
        else
            let features = features |> PList.toList

            let minimum = features|> List.map(fun x -> x.sol) |> List.min
            let maximum = features|> List.map(fun x -> x.sol) |> List.max
            let numberOfLabels = 10
            let nth = max 1 (Range1i(minimum, maximum).Size / 10)
            
            features
            |> List.map(fun x -> x.sol |> string, x.geometry.positions.Head) 
            //|> List.sortBy(fun (_,p) -> V3d.DistanceSquared(position, p))
            |> HMap.ofList //kill duplicates
            |> HMap.toList
            //|> shuffleR (Random())
            //|> ... sortby bla
            |> everyNth nth
            |> List.take' numberOfLabels
            |> HMap.ofList
    
    let private updateSgFeatures (features:plist<Feature>) =
      
        let array = features |> PList.toArray
        
        let names     = array |> Array.map(fun f -> f.id)            
        let positions = array |> Array.map(fun f -> f.geometry.positions.Head)            
        let colors    = array |> Array.map(fun f -> f.instrument |> MinervaModel.instrumentColor )
        
        let trafo =
            match positions |> Array.tryHead with
            | Some p -> Trafo3d.Translation p
            | _ -> Trafo3d.Identity
                         
        {
            names = names
            positions = positions
            colors = colors
            trafo = trafo
        }
    
    let private updateSelectedSgFeature (features:plist<Feature>) (selected:hset<string>) =
        features
        |> PList.filter( fun x -> HSet.contains x.id selected)
        |> updateSgFeatures
       
    let private setSelection (newSelection: hset<string>) (model: MinervaModel) =
        let selectedSgs = updateSelectedSgFeature model.session.filteredFeatures newSelection
        let session = { model.session with selection = { model.session.selection with selectedProducts = newSelection}}
        { model with session = session; selectedSgFeatures = selectedSgs}

    let overwriteSelection (selectionIds: list<string>) (model:MinervaModel) =
        let newSelection  = selectionIds |> HSet.ofList
        setSelection newSelection

    let updateSelectionToggle (names:list<string>) (model:MinervaModel) =
        let newSelection = 
            names
            |> List.fold(fun set name -> 
                match set |> HSet.contains name with
                | true ->  set |> HSet.remove name
                | false -> set |> HSet.add name) model.session.selection.selectedProducts

        model |> setSelection newSelection 
    
    let updateFeaturesForRendering (model:MinervaModel) =
        Log.startTimed "[Minerva] building sgs"
        let solLabels  = updateSolLabels  model.session.filteredFeatures model.session.queryFilter.filterLocation //view frustum culling AND distance culling
        let sgFeatures = updateSgFeatures model.session.filteredFeatures
        Log.line "[Minerva] showing %d labels and %d products" solLabels.Count sgFeatures.positions.Length
        Log.stop()
        { model with solLabels = solLabels; sgFeatures = sgFeatures }
    
    let queryClosestPoint model hit =         
        let viewProj = hit.event.evtView * hit.event.evtProj
        let viewPort = V2d hit.event.evtViewport
        let size = 5.0 * 2.0 / viewPort
        let centerNDC = 
            let t = V2d hit.event.evtPixel / viewPort
            V2d(2.0 * t.X - 1.0, 1.0 - 2.0 * t.Y)
        
        let ellipse = Ellipse2d(centerNDC, V2d.IO * size, V2d.OI * size)

        match model.session.selection.kdTree with
        | null -> Seq.empty
        | _ -> 
            let closestPoints = 
                KdTreeQuery.FindPoints(
                    model.session.selection.kdTree, 
                    model.kdTreeBounds, 
                    model.session.selection.flatPos, 
                    viewProj, 
                    ellipse
                )
            
            closestPoints
    
    let updateProducts data (model : MinervaModel) =
        Log.line "[Minerva] found %d entries" data.features.Count   
        let flatList = 
            data.features 
            |> PList.map(fun x -> x.geometry.positions |> List.head, x.id) 
            |> PList.toArray
        
        if flatList |> Array.isEmpty then
            model
        else
            let input = flatList |> Array.map fst
            let flatId = flatList |> Array.map snd
            let kdTree = PointKdTreeExtensions.CreateKdTree(input, Metric.Euclidean, 1e-5)
            let kdTreeBounds = Box3d(input)

            let session = 
                {
                    model.session with
                        selection = 
                            { 
                                model.session.selection with
                                    kdTree = kdTree
                                    flatPos = input
                                    flatID = flatId
                            }                      
                        filteredFeatures = data.features
                }
            { 
              model with
                data = data                
                kdTreeBounds = kdTreeBounds                 
                session = session
            }

    let loadProducts dumpFile cacheFile model =
        Log.startTimed "[Minerva] Fetching full dataset from data file"
        let data = MinervaModel.loadDumpCSV dumpFile cacheFile
        Log.stop()     
        
        updateProducts data model

    let loadTifs (model: MinervaModel) =
        Log.startTimed "[Minerva] Fetching all TIFs from data file"
        let credentials = "minerva:tai8Ies7" 
        model.data.features
        |> PList.map(fun f ->
            Files.loadTifAndConvert credentials f.id
        ) |> ignore
        Log.stop()
        model

    // 1087 -> Some(Files.loadTifAndConvert credentials f.id) 
    let loadTifs1087 (model: MinervaModel) =
        let credentials = "minerva:tai8Ies7" 
        let features1087 = 
            model.data.features 
            |> PList.filter(fun (x :Feature) -> x.sol = 1087)

        let numOfFeatures = features1087.Count
        Log.startTimed "[Minerva] Fetching %d TIFs from data file" numOfFeatures

        features1087
        |> PList.toList
        |> List.iteri(fun i feature -> 
            Report.Progress(float i / float numOfFeatures)
            Files.loadTifAndConvert credentials feature.id)
        
        Log.stop()
        model
                
    let update (view:CameraView) frustum (model : MinervaModel) (msg : MinervaAction) : MinervaModel =
        match msg with     
        | PerformQueries -> failwith "[Minerva] not implemented"
          //try
          //  //let data = model.queries |> MinervaGeoJSON.loadMultiple        
          //  Log.startTimed "[Minerva] Fetching full dataset from Server"
          //  let data = idTestList |> (getIdQuerySite model.queryFilter) |> fun (a,b) -> MinervaGeoJSON.loadPaged a b
          //  //let features = data.features |> PList.sortBy(fun x -> x.sol)    
          //  Log.stop()
    
          //  let queryM = QueryApp.updateFeaturesForRendering model.queryFilter data.features
          //  { model with data = data; queryFilter = queryM }
    
          //with e ->
          //  Log.error "%A" e.Message
          //  model
        | SelectByIds selectionIds -> // Set SelectByIds
            let newSelection  = selectionIds |> HSet.ofList
            model |> setSelection newSelection
        | MultiSelectByClipBox box ->
            let viewProjTrafo = view.ViewTrafo * Frustum.projTrafo frustum
            
            let featureArray = 
                model.session.filteredFeatures
                |> PList.map (fun x -> struct (x.id |> string, x.geometry.positions.Head))
                |> PList.toArray

            let newSelection = 
                let worstCase = System.Collections.Generic.List(featureArray.Length)
                for i in 0..featureArray.Length-1 do
                    let struct(id, pos) = featureArray.[i]
                    if box.Contains (viewProjTrafo.Forward.TransformPosProj pos) then
                        worstCase.Add id
                worstCase 
                |> HSet.ofSeq

            if newSelection.IsEmptyOrNull() then
                //Log.line "Selection-Rect is empty"
                model
            else 
                //Log.line "Found %i Features within selection rect" newSelection.Count 
                model |> setSelection newSelection
        | FilterByIds idList -> //failwith "[Minerva] not implemented"
                    
            Log.line "[Minerva] filtering data to set of %d" idList.Length
            
            let filterSet = idList |> HSet.ofList
            let filtered = model.data.features |> PList.filter(fun x -> x.id |> filterSet.Contains)
            let session = { model.session with filteredFeatures = filtered }

            { model with session = session } |> updateFeaturesForRendering    
        | FlyToProduct _ -> model //handled in higher level app
        
        // TODO....refactor openTif and loadTif
        | OpenTif id -> 
            Files.loadTif id |> ignore
            model
        | LoadTifs access ->
            loadTifs model
        
        | LoadProducts (dumpFile, cacheFile) -> 
            model |> loadProducts dumpFile cacheFile
        | QueryMessage msg -> 
            let filters = QueryApp.update model.session.queryFilter msg        
            let filtered = QueryApp.applyFilterQueries model.data.features filters
            let session = { model.session with queryFilter = filters; filteredFeatures = filtered }

            { model with session = session } |> updateFeaturesForRendering
        | ApplyFilters ->
            let filtered = QueryApp.applyFilterQueries model.data.features model.session.queryFilter
            let session = { model.session with filteredFeatures = filtered } 
            { model with session = session } |> updateFeaturesForRendering
        | ClearFilter ->            
            let session = { model.session with filteredFeatures = model.data.features; queryFilter = QueryModel.initial }
            { model with session = session } |> updateFeaturesForRendering
        | SingleSelectProduct name ->
            let session = { model.session with selection = { model.session.selection with singleSelectProduct = Some name }}
            { model with session = session }
        | ClearSelection ->
            let session = { 
                model.session with 
                    selection = { 
                        model.session.selection with 
                            singleSelectProduct = None; selectedProducts = HSet.empty
                    }
            }
            { model with session = session; selectedSgFeatures = updateSgFeatures PList.empty }
        | AddProductToSelection name ->
            let m' = updateSelectionToggle [name] model
            let session = { m'.session with selection = { m'.session.selection with singleSelectProduct = Some name } }
            { m' with session = session }
        | PickProducts hit -> 
            let closestPoints = queryClosestPoint model hit
            match closestPoints with
            | emptySeq when Seq.isEmpty emptySeq -> model
            | seq -> 
                let index = seq |> Seq.map (fun (depth, pos, index) -> index) |> Seq.head
                let closestID = model.session.selection.flatID.[index]
                updateSelectionToggle [closestID] model                             
        | HoverProducts hit ->
            //Report.BeginTimed("hover-update") |> ignore
            
            let closestPoints = queryClosestPoint model hit
    
            let updateModel = 
                match closestPoints with
                | emptySeq when Seq.isEmpty emptySeq -> 
                    match model.hoveredProduct with
                    | None -> model
                    | Some _ -> { model with hoveredProduct = None}
                | seq -> 
                    let depth, pos, index = seq |> Seq.head
                    let id = model.session.selection.flatID.[index]
                    { model with hoveredProduct = Some { id = id; pos = pos} }
            //Report.EndTimed() |> ignore
    
            updateModel

        | HoverProduct o ->
            { model with hoveredProduct = o }
   
        | SetPointSize s ->
            let size = Numeric.update model.session.featureProperties.pointSize s

            let session = { model.session with featureProperties = { model.session.featureProperties with pointSize = size } }
            { model with session = session }
        | SetTextSize s ->
            let size = Numeric.update model.session.featureProperties.textSize s
            let session = { model.session with featureProperties = { model.session.featureProperties with textSize = size }}   
            { model with session = session }

        | _ -> 
            Log.error "Action %A not in reduced minerva version." msg
            model
    
    let viewFeaturesGui (model : MMinervaModel) =
       
        let viewFeatures (instr : Instrument) (features : list<Feature>) = 
        
          let selectionColor (model : MMinervaModel) (feature : Feature) =
            model.session.selection.selectedProducts
              |> ASet.map(fun x -> x = feature.id)
              |> ASet.contains true
              |> Mod.map (function x -> if x then C4b.VRVisGreen else C4b.White)
        
          let ac = sprintf "color: %s" (Html.ofC4b C4b.White)
        
          features |> List.map(fun f -> 
            let headerAttributes =
                amap {
                    //yield onClick (fun _ -> SingleSelectProduct f.id)
                    yield  onClick(fun _ -> AddProductToSelection f.id)
                } |> AttributeMap.ofAMap
        
            //let iconAttr = 
            //  [
            //    clazz "ui map pin inverted middle aligned icon"; 
            //    style (sprintf "color: %s" (f.instrument |> Model.instrumentColor |> Html.ofC4b))
            //    onClick(fun _ -> AddProductToSelection f.id)
            //    //onClick(fun _ -> FlyToProduct f.geometry.positions.Head)
            //  ]  
              
            let iconAttributes =
              amap {                  
                yield clazz "ui map pin inverted middle aligned icon"
                //yield  onClick(fun _ -> AddProductToSelection f.id)
        
                yield style (sprintf "color: %s" (Html.ofC4b (f.instrument |> MinervaModel.instrumentColor)))
              } |> AttributeMap.ofAMap
        
            let headerAttr : AttributeMap<_> = [clazz "ui header small"] |> AttributeMap.ofList
                                                    
            div [clazz "ui inverted item"][
              Incremental.i iconAttributes AList.empty //i iconAttributes []
              div [clazz "ui content"] [
                Incremental.div (AttributeMap.ofList [style ac]) (
                  alist {
                    let! hc = selectionColor model f
                    let c = hc |> Html.ofC4b |> sprintf "color: %s"
                    yield div[clazz "header"; style c][
                          Incremental.div (headerAttributes) (AList.single (instr |> instrumentText |> text))
                    ]
                    yield div [clazz "ui description"] [
                            f.sol |> sprintf "Sol: %A" |> text
                            i [clazz "binoculars icon"; onClick (fun _ -> FlyToProduct f.geometry.positions.Head)][]
                            i [clazz "download icon"; onClick (fun _ -> OpenTif f.id)][]
                            ]
                    //yield i [clazz "binoculars icon"; onClick (fun _ -> FlyToProduct f.geometry.positions.Head)][] //|> UI.wrapToolTip "FlyTo"       
                             
                  } )
                ]            
            ])
        
        let accordion text' icon active content' =
           let title = if active then "title active inverted" else "title inverted"
           let content = if active then "content active" else "content"     
                                 
           onBoot "$('#__ID__').accordion();" (
               div [clazz "ui inverted segment"] [
                   div [clazz "ui inverted accordion fluid"] [
                       div [clazz title; style "background-color: #282828"] [
                               i [clazz ("dropdown icon")][]
                               text text'                                
                               div[style "float:right"][i [clazz (icon + " icon")][]]     
                       ]
                       div [clazz content;  style "overflow-y : auto; "] content' //max-height: 35%
                   ]
               ]
           )
         
        let propertiesGui =
            require Html.semui ( 
                Html.table [ 
                    Html.row "point size:" [Numeric.view' [NumericInputType.Slider] model.session.featureProperties.pointSize |> UI.map (fun x -> SetPointSize x)] 
                    Html.row "text size:" [Numeric.view' [NumericInputType.InputBox] model.session.featureProperties.textSize |> UI.map (fun x -> SetTextSize x)] 
                ]        
            )     
            
        let viewFeatureProperties = 
            model.session.selection.singleSelectProduct
            |> Mod.map( fun selected ->
                match selected with
                | Some id ->
                    let feat = 
                        model.session.filteredFeatures
                        |> AList.toList
                        |> List.find(fun x -> x.id = id)
        
                    require Html.semui (
                        Html.table [   
                            Html.row "Instrument:"    [Incremental.text (feat.instrument |> instrumentText |> Mod.constant)] 
                            Html.row "Sol:"           [Incremental.text (feat.sol.ToString() |> Mod.constant)]   
                            Html.row "FlyTo:"         [button [clazz "ui button tiny"; onClick (fun _ -> FlyToProduct feat.geometry.positions.Head )][]]
                            Html.row "Get tif:"       [button [clazz "ui button tiny"; onClick (fun _ -> OpenTif feat.id )][]]
                            ]
                        )
                | None ->  div[style "font-style:italic"][ text "no product selected" ]
            )

        let featuresGroupedByInstrument features =
            adaptive {
                let! features = features |> AList.toMod
                return features |> PList.toList |> List.groupBy(fun x -> x.instrument)
            }

        let groupedFeatures = model.session.filteredFeatures |> featuresGroupedByInstrument
                                      
        let listOfFeatures =
            alist {           
                let! groupedFeatures = groupedFeatures
                
                let! pos = model.session.queryFilter.filterLocation        
                for (instr, group) in groupedFeatures do
                
                    let header = sprintf "%s (%d)" (instr |> instrumentText) group.Length
                    
                    let g = 
                      group 
                        |> List.sortBy(fun x -> V3d.DistanceSquared(pos, x.geometry.coordinates.Head)) 
                        |> List.take'(20)
                    
                    yield div [clazz "ui inverted item"][
                        yield accordion header "Content" false [                    
                          div [clazz "ui list"] (viewFeatures instr g)
                        ]
                    ]
            }
        
        div [clazz "ui inverted segment"] [
            div [clazz "ui buttons"] [
                button [clazz "ui button small"; onClick (fun _ -> ConnectVisplore)][text "Connect"]
                button [clazz "ui button small"; onClick (fun _ -> SendSelection)][text "Send Selection"]
                button [clazz "ui button small"; onClick (fun _ -> SendScreenSpaceCoordinates)][text "Snapshot"]
                //button [
              //  clazz "ui button"; 
              //  onEvent "onGetRenderId" [] (fun args -> Reset)
              //  clientEvent "onclick" "aardvark.processEvent(__ID__,'onGetRenderId', document.getElementsByClassName('mainrendercontrol')[0].id)"
              //] [text "SCREAM SHOT"]
            ]
            
            accordion "Query App" "Content" false [
                div [clazz "ui buttons"] [         
                    //button [clazz "ui button"; onClick (fun _ -> Save)][text "Save"]
              //button [clazz "ui button"; onClick (fun _ -> Load)][text "Load"]
                    button [clazz "ui button"; onClick (fun _ -> ApplyFilters)][text "Apply Filter"]
                    button [clazz "ui button"; onClick (fun _ -> ClearFilter)][text "Clear Filter"]
                    button [clazz "ui button"; onClick (fun _ -> ClearSelection)][text "Clear Selection"]         
                ]
                
                // QUERYAPP included
                QueryApp.viewQueryFilters groupedFeatures model.session.queryFilter |> UI.map QueryMessage
                propertiesGui          
            ]
            
            Incremental.div 
                ([clazz "ui very compact stackable inverted relaxed divided list"] |> AttributeMap.ofList) 
                listOfFeatures //AList.empty  
                        
            accordion "Properties" "Content" false [ 
                Incremental.div AttributeMap.empty (viewFeatureProperties |> AList.ofModSingle)
            ]                                   
                       
         ]
    
    let viewWrapped pos (model : MMinervaModel) =
        require Html.semui (
            body [style "width: 100%; height:100%; background: #252525; overflow-x: hidden; overflow-y: scroll"] [
                div [clazz "ui inverted segment"] [viewFeaturesGui model]
            ])
    
    // SG
    let viewPortLabels (model : MMinervaModel) (view:IMod<CameraView>) (frustum:IMod<Frustum>) : ISg<MinervaAction> = 
        
        let viewProjTrafo = Mod.map2 (fun (v:CameraView) f -> v.ViewTrafo * Frustum.projTrafo f) view frustum
        let near = frustum |> Mod.map (fun x -> x.near)

        let textHelper2 text pos =
            Drawing.text view near 
                  (Mod.constant 60.0) 
                  pos 
                  (Trafo3d.Translation(pos)) 
                  (Mod.constant text) 
                  (model.session.featureProperties.textSize.value)

        //let textHelper text pos =
        //    Sg.textWithConfig {TextConfig.Default with renderStyle = RenderStyle.Billboard} (Mod.constant(text)) 
        //    |> Sg.noEvents
        //    |> Sg.trafo (Mod.constant (Trafo3d.Translation pos))

        let featureArray = 
            model.session.filteredFeatures
            |> AList.map (fun x -> struct (x.sol |> string, x.geometry.positions.Head))
            |> AList.toMod
            |> Mod.map (fun x -> x |> PList.toArray)

        let box = Box3d(V3d(-1.0, -1.0, 0.0),V3d(1.0,1.0,1.0))

        let visibleFeatures = 
            featureArray 
            |> Mod.map2 (fun (viewProj:Trafo3d) array -> 

                //Log.startTimed "filterStart"

                let worstCase = System.Collections.Generic.List(array.Length)
                
                for i in 0..array.Length-1 do
                    let struct(id, pos) = array.[i]
                    if box.Contains (viewProj.Forward.TransformPosProj pos) then
                        worstCase.Add struct (id, pos)

                //Log.stop()

                worstCase

            ) viewProjTrafo

        let maxCount = 5.0

        let topFeatures =
            visibleFeatures
            |> Mod.map (fun x -> 
                let count = float x.Count
                if count < maxCount then
                    x.ToArray()
                else 
                    let stepSize = count / maxCount
                    [|
                        for i in 0.0..stepSize..count-1.0 do
                            yield x.[i.Ceiling() |> int]
                    |])

        let sg =
            topFeatures 
            |> ASet.bind (fun x -> x |> Array.map (fun struct (text, pos) -> textHelper2 text pos) |> ASet.ofArray)
            |> Sg.set

        sg
    
    let getSolBillboards (model : MMinervaModel) (view:IMod<CameraView>) (near:IMod<float>) : ISg<MinervaAction> =        
        model.solLabels
            |> AMap.map(fun txt pos ->
               Drawing.text view near 
                  (Mod.constant 60.0) 
                  pos 
                  (Trafo3d.Translation(pos)) 
                  (Mod.constant txt) 
                  (model.session.featureProperties.textSize.value)
            ) 
            |> AMap.toASet  
            |> ASet.map(fun x -> snd x)            
            |> Sg.set
            
    let viewFilterLocation (model : MMinervaModel) =
        let height = 5.0
        let coneTrafo = 
            model.session.queryFilter.filterLocation 
            |> Mod.map(fun x -> 
                Trafo3d.RotateInto(V3d.ZAxis, -x.Normalized) * Trafo3d.Translation (x + (x.Normalized * height)))
            //lineLength |>
            //  Mod.map(fun s -> Trafo3d.RotateInto(V3d.ZAxis, dip) * Trafo3d.Translation(center' + dip.Normalized * s))

        Drawing.coneISg (C4b.VRVisGreen |> Mod.constant) (0.5 |> Mod.constant) (height |> Mod.constant) coneTrafo
            
    let viewFeaturesSg (model : MMinervaModel) =
        let pointSize = model.session.featureProperties.pointSize.value
        
        Sg.ofList [
            Drawing.featureMousePick model.kdTreeBounds
            Drawing.drawFeaturePoints model.sgFeatures pointSize
            Drawing.drawSelectedFeaturePoints model.selectedSgFeatures pointSize
            Drawing.drawHoveredFeaturePoint model.hoveredProduct pointSize model.sgFeatures.trafo
        ]
    
    let threads (m : MinervaModel) = m.vplMessages
   
    let start()  =
      
      App.start {
          unpersist = Unpersist.instance
          threads   = fun m -> m.vplMessages
          view      = viewWrapped (Mod.constant V3d.Zero) //localhost
          update    = update (CameraView.lookAt V3d.Zero V3d.One V3d.OOI) (Frustum.perspective 90.0 0.001 100000.0 1.0)
          initial   = MinervaModel.initial
      }