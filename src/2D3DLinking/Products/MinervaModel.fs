﻿namespace PRo3D.Minerva

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.UI

open FSharp.Data
open Aardvark.Geometry

open PRo3D.Base

#nowarn "0686"

type FeatureId = FeatureId of string

type Typus = 
  | FeatureCollection = 0
  | Feature           = 1
  | Polygon           = 2
  | Point             = 3

type MAHLI_Properties =
  {
    id        : FeatureId
    beginTime : DateTime
    endTime   : DateTime
  }

type FrontHazcam_Properties =
  {
    id        : FeatureId
    beginTime : DateTime
    endTime   : DateTime
  }

type Mastcam_Properties =
  {
    id        : FeatureId
    beginTime : DateTime
    endTime   : DateTime
  }

type ChemCam_Properties =
  {
    id        : FeatureId    
  }

type APXS_Properties =
  {
    id        : FeatureId
  }

type Properties =
    | MAHLI       of MAHLI_Properties
    | FrontHazcam of FrontHazcam_Properties
    | Mastcam     of Mastcam_Properties
    | APXS        of APXS_Properties
    | ChemCam     of ChemCam_Properties
    member this.id =
        match this with
        | MAHLI       k -> k.id
        | FrontHazcam k -> k.id
        | Mastcam     k -> k.id
        | APXS        k -> k.id
        | ChemCam     k -> k.id

type Instrument = 
    | MAHLI          =  0
    | FrontHazcam    =  1
    | Mastcam        =  2
    | APXS           =  3
    | FrontHazcamR   =  4
    | FrontHazcamL   =  5
    | MastcamR       =  6
    | MastcamL       =  7
    | ChemLib        =  8
    | ChemRmi        =  9
    | NotImplemented = 10

type Geometry = 
    {
        typus       : Typus
        coordinates : list<V3d>
        positions   : list<V3d>
    }

type Feature =
    { 
        id          : string
        instrument  : Instrument
        typus       : Typus
        properties  : Properties
        boundingBox : Box2d
        geometry    : Geometry
        sol         : int
        dimensions  : V2i
    }

type RootProperties = 
    {
        totalCount   : int
        startIndex   : int 
        itemsPerPage : int    
        published    : DateTime
    }

[<DomainType>]
type FeatureCollection = 
    {
        version     : int
        name        : string
        typus       : Typus
        boundingBox : Box2d    
        features    : plist<Feature>
    }
with
    static member current = 0
    static member initial =
        { 
            version     = FeatureCollection.current
            name        = "initial"
            boundingBox = Box2d.Invalid
            typus       = Typus.Feature
            features    = PList.empty
        }

type QueryAction =
    | SetMinSol         of Numeric.Action
    | SetMaxSol         of Numeric.Action
    | SetDistance       of Numeric.Action
    | SetFilterLocation of V3d
    | CheckMAHLI
    | CheckFrontHazcam
    | CheckMastcam
    | CheckAPXS
    | CheckFrontHazcamR
    | CheckFrontHazcamL
    | CheckMastcamR
    | CheckMastcamL
    | CheckChemLib
    | CheckChemRmi
  //| UseQueriesForDataFile

type MinervaAction =
    | LoadProducts                  of string * string
    | Save
    | Load
    | ApplyFilters
    | ClearFilter
    | PerformQueries  
    | ClearSelection
    | SendSelection
    | SendScreenSpaceCoordinates
    | FilterByIds                   of list<string>
    | SelectByIds                   of list<string>
    | MultiSelectByClipBox          of Box3d
    | ConnectVisplore
    | FlyToProduct                  of V3d
    | QueryMessage                  of QueryAction
    | SetPointSize                  of Numeric.Action
    | SetTextSize                   of Numeric.Action
    | SingleSelectProduct           of string
    | AddProductToSelection         of string
    | PickProducts                  of SceneHit
    | HoverProducts                 of SceneHit
    | OpenTif                       of string
    | LoadTifs                      of string
  //| ChangeInstrumentColor of ColorPicker.Action * Instrument

[<DomainType>]
type SgFeatures = {
    names       : string[]
    positions   : V3d[]
    colors      : C4b[]
    trafo       : Trafo3d
}

//TODO Lf model with hmap<instrumentType, color>
[<DomainType>]
type InstrumentColor = {
    mahli        : C4b    
    frontHazcam  : C4b 
    mastcam      : C4b  
    apxs         : C4b  
    frontHazcamR : C4b 
    frontHazcamL : C4b 
    mastcamR     : C4b 
    mastcamL     : C4b 
    chemLib      : C4b 
    chemRmi      : C4b  
    color        : ColorInput
}

[<DomainType>]
type FeatureProperties = {
    pointSize   : NumericInput
    textSize    : NumericInput
    //instrumentColor : InstrumentColor
}

module QueryModelInitial =
    let minSol = 
        {
            value = 1050.0
            min =  0.0
            max = 10000.0
            step = 1.0
            format = "{0:0}"
        }
    
    let maxSol = 
        {
            value = 1100.0
            min =  0.0
            max = 10000.0
            step = 1.0
            format = "{0:0}"
        }
    
    let distance = 
        {
            value = 10000000.0
            min =  0.0
            max = 10000000.0
            step = 100.0
            format = "{0:0}"
        }

[<DomainType>]
type QueryModel = {
    version              : int
    minSol               : NumericInput
    maxSol               : NumericInput
    
    distance             : NumericInput
    filterLocation       : V3d
    
    //TODO LF ... model with hset or hmap
    checkMAHLI           : bool
    checkFrontHazcam     : bool
    checkMastcam         : bool
    checkAPXS            : bool
    checkFrontHazcamR    : bool
    checkFrontHazcamL    : bool
    checkMastcamR        : bool
    checkMastcamL        : bool
    checkChemLib         : bool
    checkChemRmi         : bool        
}
with 
    static member current = 0    
    static member initial =
        {
            version               = QueryModel.current

            minSol                = QueryModelInitial.minSol
            maxSol                = QueryModelInitial.maxSol
           
            distance              = QueryModelInitial.distance
            filterLocation        = V3d.Zero
            
            checkMAHLI            = true
            checkFrontHazcam      = true
            checkMastcam          = true
            checkAPXS             = true
            checkFrontHazcamR     = true
            checkFrontHazcamL     = true
            checkMastcamR         = true
            checkMastcamL         = true
            checkChemLib          = true
            checkChemRmi          = true              
        }

[<DomainType>]
type SelectionModel = {
    version              : int
    selectedProducts     : hset<string> 
    singleSelectProduct  : option<string>
    selectionMinDist     : float

    [<NonIncremental>]
    kdTree               : PointKdTreeD<V3d[],V3d>
    [<NonIncremental>]
    flatPos              : array<V3d>
    [<NonIncremental>]
    flatID               : array<string>
}
with 
    static member current = 0
    static member initial =
        {
            version = SelectionModel.current
            selectedProducts     = hset.Empty
            singleSelectProduct  = None
            kdTree               = Unchecked.defaultof<_>
            flatPos              = Array.empty
            flatID               = Array.empty
            selectionMinDist     = 0.05
        }

//type Selection = {
//    selectedProducts        : hset<string> 
//    singleSelectProduct     : option<string>
//}

open System.IO

module MinervaModel =
    module Initial =
        let pointSize = 
            {
                value = 5.0
                min = 0.5
                max = 15.0
                step = 0.1
                format = "{0:0.00}"
            }
        
        let textSize = 
            {
                value = 10.0
                min = 0.001
                max = 30.0
                step = 0.001
                format = "{0:0.000}"
            }  
        
        let featureProperties = 
            {
                pointSize = pointSize
                textSize = textSize
                //instrumentColor = instrumentC
            }
        
        let sgFeatures =
            {
                names     = Array.empty
                positions = Array.empty
                colors    = Array.empty
                trafo     = Trafo3d.Identity
            }
        
        let selectedSgFeatures =
            {
                names     = Array.empty
                positions = Array.empty
                colors    = Array.empty
                trafo     = Trafo3d.Identity
            }                       

        let sites = [
            //@"https://minerva.eox.at/opensearch/collections/MAHLI/json/"
            //@"https://minerva.eox.at/opensearch/collections/FrontHazcam-Right/json/"
            //@"https://minerva.eox.at/opensearch/collections/FrontHazcam-Left/json/"
            //@"https://minerva.eox.at/opensearch/collections/Mastcam-Right/json/"    
            //@"https://minerva.eox.at/opensearch/collections/Mastcam-Left/json/"
            //@"https://minerva.eox.at/opensearch/collections/APXS/json/"
            @"https://minerva.eox.at/opensearch/collections/all/json/"
        ]

    let toInstrument (id : string) = 
        match id.ToLowerInvariant() with
        | "mahli"        -> Instrument.MAHLI
        | "apxs"         -> Instrument.APXS
        | "fhaz_left_b"  -> Instrument.FrontHazcamL    
        | "fhaz_right_b" -> Instrument.FrontHazcamR    
        | "mast_left"    -> Instrument.MastcamL
        | "mast_right"   -> Instrument.MastcamR
        | "chemcam_libs" -> Instrument.ChemLib
        | "chemcam_rmi"  -> Instrument.ChemRmi      
        | _ -> id |> sprintf "unknown instrument %A" |> failwith
    
    let instrumentColor (instr : Instrument) =
        match instr with 
        | Instrument.MAHLI          -> C4b(27,158,119)
        | Instrument.FrontHazcam    -> C4b(255,255,255)
        | Instrument.Mastcam        -> C4b(255,255,255)
        | Instrument.APXS           -> C4b(230,171,2)
        | Instrument.FrontHazcamR   -> C4b(31,120,180)
        | Instrument.FrontHazcamL   -> C4b(166,206,227)
        | Instrument.MastcamR       -> C4b(227,26,28)
        | Instrument.MastcamL       -> C4b(251,154,153)
        | Instrument.ChemRmi        -> C4b(173,221,142)
        | Instrument.ChemLib        -> C4b(49,163,84)
        | Instrument.NotImplemented -> C4b(0,0,0)
        | _ -> failwith "unknown instrument"

    let getProperties (ins : Instrument) (insId:string) (row:CsvRow) : Properties = 
        match ins with
            | Instrument.MAHLI ->   
                {
                MAHLI_Properties.id = insId |> FeatureId
                beginTime =  DateTime.Parse(row.GetColumn "{Timestamp}Start_time")
                endTime =  DateTime.Parse(row.GetColumn "{Timestamp}Stop_time")
                } |> Properties.MAHLI
            | Instrument.FrontHazcam | Instrument.FrontHazcamL | Instrument.FrontHazcamR ->
                {
                FrontHazcam_Properties.id = insId |> FeatureId
                beginTime =  DateTime.Parse(row.GetColumn "{Timestamp}Start_time")
                endTime =  DateTime.Parse(row.GetColumn "{Timestamp}Stop_time")
                } |> Properties.FrontHazcam
            | Instrument.Mastcam | Instrument.MastcamL | Instrument.MastcamR ->
                {
                Mastcam_Properties.id = insId |> FeatureId
                beginTime =  DateTime.Parse(row.GetColumn "{Timestamp}Start_time")
                endTime =  DateTime.Parse(row.GetColumn "{Timestamp}Stop_time")
                } |> Properties.Mastcam
            | Instrument.APXS ->
              {
                APXS_Properties.id = insId |> FeatureId
              } |> Properties.APXS       
            | Instrument.ChemLib ->
              {
                ChemCam_Properties.id = insId |> FeatureId           
              } |> Properties.ChemCam       
            | Instrument.ChemRmi ->
              {
                ChemCam_Properties.id = insId |> FeatureId            
              } |> Properties.ChemCam       
            | Instrument.NotImplemented ->
              {
                APXS_Properties.id = insId |> FeatureId
              } |> Properties.APXS               
            | _ -> failwith "encountered invalid instrument from parsing"

    let intOrDefault (def : int) (name: string) =
        match name with | "" -> def | _ -> name.AsInteger()            

    let getFeature (row:CsvRow) : option<Feature> =

        let id' = row.GetColumn "{Key}Product_id"
        match id' with
         | "" -> None
         | _-> 
            let inst = row.GetColumn "{Category}Instrument_id"
            let instrument = inst |> toInstrument
            let sol' = (row.GetColumn "{Value}{Sol}Planet_day_number").AsInteger()

            let omega = (row.GetColumn "{Angle}Omega").AsFloat()
            let phi = (row.GetColumn "{Angle}Phi").AsFloat()
            let kappa = (row.GetColumn "{Angle}Kappa").AsFloat()

            let x = (row.GetColumn "{CartX}X").AsFloat()
            let y = (row.GetColumn "{CartY}Y").AsFloat()
            let z = (row.GetColumn "{CartZ}Z").AsFloat()


            let w = (row.GetColumn "{Value}Image_width") |> intOrDefault 0            
            let h = (row.GetColumn "{Value}Image_height") |> intOrDefault 0            

            let instName = row.GetColumn "{Category}Instrument_name"

            let props = getProperties instrument inst row

            let geo = 
                {
                    typus = Typus.Point
                    coordinates = V3d(omega, phi, kappa) |> List.singleton
                    positions = V3d(x, y, z) |> List.singleton
                }

            let feature = 
                {
                  id          = id'
                  instrument  = instrument
                  typus       = Typus.Feature 
                  boundingBox = Box2d.Invalid//feature?bbox |> parseBoundingBox
                  properties  = props
                  geometry    = geo
                  sol         = sol'
                  dimensions  = V2i(w, h)
                } 
            Some feature   

    let loadDumpCSV dumpFile cacheFile =
        let cachePath = cacheFile
        let path = dumpFile
        Log.startTimed "[Minerva] Loading products"
        let featureCollection =
            match (File.Exists path, File.Exists cachePath) with
             | (true, false) -> 
                let allData = CsvFile.Load(path).Cache()

                let features = 
                    allData.Rows
                        |> Seq.toList
                        |> List.mapi(fun i x -> 
                            Log.line "[Minerva] %d" i
                            getFeature x )      
                        |> List.choose id
                {
                  version     = FeatureCollection.current
                  name        = "dump"
                  typus       = Typus.FeatureCollection    
                  boundingBox = Box2d.Invalid
                  features    = features |> PList.ofList
                } |> Serialization.save cachePath
             | (_, true) -> Serialization.loadAs cachePath
             | _ -> 
                Log.error "[Minerva] sth. went wrong with dump.csv"
                FeatureCollection.initial
        Log.stop()
        featureCollection
   
[<DomainType>]
type Session =
    {
        version            : int
        queryFilter        : QueryModel
        featureProperties  : FeatureProperties
        selection          : SelectionModel

        queries            : list<string>
        filteredFeatures   : plist<Feature> //TODO TO make to ids
        dataFilePath       : string
    }
with
    static member current = 0
    static member initial = 
        {
            version           = Session.current
            queryFilter       = QueryModel.initial
            queries           = List.empty
            filteredFeatures  = PList.empty
            selection         = SelectionModel.initial
            featureProperties = MinervaModel.Initial.featureProperties        
            dataFilePath      = ""
        }

type SelectedProduct =
    {
        id:  string
        pos: V3d
    }

[<DomainType>]
type MinervaModel = 
    {        
        session            : Session        
        data               : FeatureCollection

        [<NonIncremental>]
        vplMessages : ThreadPool<MinervaAction>
        
        kdTreeBounds         : Box3d
        hoveredProduct       : Option<SelectedProduct>
        solLabels            : hmap<string, V3d>
        sgFeatures           : SgFeatures
        selectedSgFeatures   : SgFeatures
        picking              : bool        
    }                                    
with 
    static member initial =


        {
            session = Session.initial
            data = FeatureCollection.initial
            vplMessages = ThreadPool.Empty

            kdTreeBounds = Box3d.Invalid
            hoveredProduct = None
            solLabels = HMap.empty
            sgFeatures = MinervaModel.Initial.sgFeatures
            selectedSgFeatures = MinervaModel.Initial.selectedSgFeatures
            picking = false
        }

[<StructuredFormatDisplay("{AsString}"); Struct>]
type Len(meter : float) =
  member x.Angstrom       = meter * 10000000000.0
  member x.Nanometer      = meter * 1000000000.0
  member x.Micrometer     = meter * 1000000.0
  member x.Millimeter     = meter * 1000.0
  member x.Centimeter     = meter * 100.0
  member x.Meter          = meter
  member x.Kilometer      = meter / 1000.0
  member x.Astronomic     = meter / 149597870700.0
  member x.Lightyear      = meter / 9460730472580800.0
  member x.Parsec         = meter / 30856775777948584.0

  member private x.AsString = x.ToString()

  override x.ToString() =
      if x.Parsec       > 0.5 then sprintf "%.3fpc" x.Parsec
      elif x.Lightyear  > 0.5 then sprintf "%.3fly" x.Lightyear
      elif x.Astronomic > 0.5 then sprintf "%.3fau" x.Astronomic
      elif x.Kilometer  > 0.5 then sprintf "%.3fkm" x.Kilometer
      elif x.Meter      > 1.0 then sprintf "%.2fm"  x.Meter
      elif x.Centimeter > 1.0 then sprintf "%.2fcm" x.Centimeter
      elif x.Millimeter > 1.0 then sprintf "%.0fmm" x.Millimeter
      elif x.Micrometer > 1.0 then sprintf "%.0fµm" x.Micrometer
      elif x.Nanometer  > 1.0 then sprintf "%.0fnm" x.Nanometer
      elif meter        > 0.0 then sprintf "%.0f"   x.Angstrom
      else "0"    
