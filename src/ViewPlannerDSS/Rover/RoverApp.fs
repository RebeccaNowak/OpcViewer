﻿namespace ViewPlanner.Rover

open System
open Aardvark.Base

module RoverApp =
    open Aardvark.UI

    let panning (m:RoverModel) =
        let forward = m.camera.view.Forward
        let up = m.up //rotate around global up axis
        let panRotation = Rot3d(up, m.pan.delta.RadiansFromDegrees())
        let targetWithPan = panRotation.TransformDir(forward)
        let newView = CameraView.look m.position targetWithPan.Normalized up
        {m with camera =  {m.camera with view = newView} }


    
    let tilting (m:RoverModel) =
        let forward = m.camera.view.Forward
        let right = m.camera.view.Right
        let tiltRotation = Rot3d(right, m.tilt.delta.RadiansFromDegrees())
        let targetWithTilt = tiltRotation.TransformDir(forward).Normalized

        let newView = CameraView.look m.position targetWithTilt m.camera.view.Up
        {m with camera =  {m.camera with view = newView} }

   
    let initializeTilt (m:RoverModel) (value:float) = 
        {m with tilt = {m.tilt with previous = value; current = value}}
    
    let initializePan (m:RoverModel) (value:float) = 
        {m with pan = {m.pan with previous = value; current = value}}

    let setPan(m:RoverModel) (value:float) =
        let dt = m.pan.previous - value
        let prev = m.pan.current
        let curr = value
        {m with pan = {m.pan with delta = dt; previous = prev; current = curr}}


    let setTilt(m:RoverModel) (value:float) =
        let dt = m.tilt.previous - value
        let prev = m.tilt.current
        let curr = value
        {m with tilt = {m.tilt with delta = dt; previous = prev; current = curr}}
    

    let calcTheta (x:float) (y:float) =
       
       
        let v = Math.Atan(y/x)
        
        let absV = Math.Abs(v)
        let absVinDegree = absV * Constant.DegreesPerRadian
        let signX = Math.Sign(x)
        let signY = Math.Sign(y)

        match signX,signY with
            | 1,1 -> v
            | -1,1 -> (180.0 - absVinDegree)
            | -1, -1 -> (absVinDegree + 180.0)
            | 1,-1 -> (360.0 - absVinDegree)
            | _ -> v

       


    
    let rotateIntoCoordinateSystem (m:RoverModel) (vector:V3d) = 
        
        let pos = m.position
        let target = m.target
        let up = m.up

        let forward = (target - pos).Normalized
        let right = (forward.Cross(up)).Normalized
        let rotZ = Trafo3d.RotateInto(V3d.OOI,up)
        let rotY = Trafo3d.RotateInto(V3d.OIO,right)
        let rotX = Trafo3d.RotateInto(V3d.IOO,forward)

        //order z y x
        let rotatedByZ = rotZ.Forward.TransformPos vector
        let rotatedByY = rotY.Forward.TransformPos rotatedByZ
        let rotatedFinal = rotX.Forward.TransformPos rotatedByY

        rotatedFinal


    let calcThetaPhi (position:V3d) =
        
        let x = position.X
        let y = position.Y
        let z = position.Z
   
        //quadrant
        let theta = calcTheta x y 

        //let theta = atan2 y x//atan(y/x)
        let phi = acos(z)//atan2 z (sqrt((pown x 2)+(pown y 2)))

        V2d(theta,phi)



    //points: in world space
    let calcPanTiltValues (m:RoverModel) (points:List<V3d>) (cam:CameraView) =
        
        let rotTrafo = Trafo3d.RotateInto(V3d.OOI, m.up)
        //let rotTrafo = Trafo3d.RotateInto( m.up, V3d.OOI)
        //project points onto projection sphere
        let spherePos = m.projsphere.position

    
        //rotated points
        //let rotatedSpherePos = rotTrafo.Forward.TransformPos spherePos
        //let rotatedPoints = points  |> List.map (fun p -> rotTrafo.Forward.TransformPos p)
        //let shiftedPoints = rotatedPoints  |> List.map (fun p -> (p - rotatedSpherePos).Normalized)
        
        //normal points
        let testPoint = m.target

       
        //add target point for testing
        let l2 = [testPoint]
        let testList = List.append points l2
        let shiftedPoints = testList  |> List.map (fun p -> (p - spherePos).Normalized)
        //let rotatedPoints = shiftedPoints  |> List.map (fun p -> rotTrafo.Forward.TransformPos p)
        let rotatedPoints = shiftedPoints  |> List.map (fun p -> rotateIntoCoordinateSystem m p)

        let r = rotatedPoints.Item(rotatedPoints.Length - 1)
        let thetaOfPointonForwardVec = calcTheta r.X r.Y
        let setR = initializePan m thetaOfPointonForwardVec
        let setR2 = initializeTilt setR ((acos(r.Z))*Constant.DegreesPerRadian)

        //calculate theta and phi for coordinates
        //test for the first point in the list

        let listOfAngles = rotatedPoints |> List.map(fun pos -> calcThetaPhi pos)
        //debuging
        for p in listOfAngles do
            printfn "theta phi %A %A"  (p.X) (p.Y* Constant.DegreesPerRadian) 
        
        //let index = shiftedPoints.Length - 1 
        let first = rotatedPoints.Item (0)
        let second = rotatedPoints.Item(1)
        let projectionPoint1 = first + spherePos
        let projectionPoint2 = second + spherePos
        let x = first.X
        let y = first.Y
        let z = first.Z
        let theta = calcTheta x y
        let phi = acos(z) //atan2 z (sqrt((pown x 2)+(pown y 2))) 


        printfn "theta: %A" theta
        printfn "phi: %A" (phi * Constant.DegreesPerRadian)




        let panned = setPan setR2 (theta)
        let pannedRover = panning panned
        let tilted = setTilt pannedRover (phi* Constant.DegreesPerRadian)
        let newR = tilting tilted

        let projectionPoints = shiftedPoints |> List.map (fun p -> p + spherePos) |> PList.ofList
        {newR with projPoint1 = projectionPoint1; projPoint2 = projectionPoint2; projPoints = projectionPoints }
        //let tilted = setTilt pannedRover (phi* Constant.DegreesPerRadian)
        //tilting tilted
        //let rotTrafo = Trafo3d.Rotation(theta, phi, 0.0)
        //let viewM2 = CameraView.ofTrafo (m.camera.view.ViewTrafo * rotTrafo)

        

        //ATTEMPT:calculate bounding box
        //let pointsInViewSpace = points  |> List.map (fun p -> cam.ViewTrafo.Forward.TransformPos p) 

        ////get corners of box
        //let box = pointsInViewSpace |> Box3d

        //let xMin = box.Min.X
        //let xMax = box.Max.X
        //let yMin = box.Min.Y
        //let yMax = box.Max.Y
        //let zMin = box.Min.Z
        //let zMax = box.Max.Z
        
        ////8 corner points
        //let leftBottomFront = V3d(xMin, yMin, zMin)
        //let rightBottomFront = V3d(xMax, yMin, zMin)
        //let leftTopFront = V3d(xMin, yMax, zMin)
        //let rightTopFront = V3d(xMax, yMax, zMin)
        //let leftBottomBack = V3d(xMin, yMin, zMax)
        //let rightBottomBack = V3d(xMax, yMin, zMax)
        //let leftTopBack = V3d(xMin, yMax, zMax)
        //let rightTopBack = V3d(xMax, yMax, zMax)

        ////store them in list and transform every point back to world space
        //let cornerList = [leftBottomFront; rightBottomFront; leftTopFront; rightTopFront; leftBottomBack;rightBottomBack; leftTopBack; rightTopBack]
        //let cornersInWorldSpace = cornerList |> List.map(fun corner -> cam.ViewTrafo.Backward.TransformPos corner)
        //let cornersPList = cornersInWorldSpace |> PList.ofList

        //let LBF = cornersInWorldSpace.Item(0)
        //let RBF = cornersInWorldSpace.Item(1)
        //let LTF = cornersInWorldSpace.Item(2) //p1
        //let RTF = cornersInWorldSpace.Item(3) //p2
        //let LBB = cornersInWorldSpace.Item(4)
        //let RBB = cornersInWorldSpace.Item(5)
        //let LTB = cornersInWorldSpace.Item(6)
        //let RTB = cornersInWorldSpace.Item(7)

        //let p1 = leftTopFront
        //let p2 = rightTopFront

        //let camPos = m.camera.view.Location
        //let dir1 = LTB - camPos
        //let dir2 = RTB - camPos
        //let p1Norm = dir1.Normalized
        //let p2Norm = dir2.Normalized

        //let angleBetween = (acos(p1Norm.Dot(p2Norm))) * Constant.DegreesPerRadian

        //printfn "%A angle:" angleBetween

        //let p1ws = p1 |> cam.ViewTrafo.Backward.TransformPos 
        //let p2ws = p2 |> cam.ViewTrafo.Backward.TransformPos 

     
        //{m with camera =  {m.camera with view = cam}; cornerLBF = Some LBF; cornerLTF = Some LTF; 
        //    cornerRBF = Some RBF; cornerRTF = Some RTF; 
        //    cornerLBB = Some LBB; cornerRBB = Some RBB;
        //    cornerLTB = Some LTB; cornerRTB = Some RTB;
        //    corners = Some cornersPList }

        //{m with camera =  {m.camera with view = viewM2} }



    let checkROIFullyInside (m:RoverModel) (region:Option<plist<V3d>>) =
        
        //calculate center point of region
        match region with 
            | None -> m
            | Some region -> 

                let sum = region.Sum()
                let c = region |> PList.count
                let centerpoint = sum / (float c)

        //calculate new view matrix by tilting and panning to center point
                let viewM = m.camera.view.ViewTrafo
                let iProj = viewM.Forward.TransformPos centerpoint
                let tiltAngle = atan2 -iProj.Y -iProj.Z
                let panAngle = atan2 iProj.X -iProj.Z
                //let rotTrafo = Trafo3d.Rotation(tiltAngle, panAngle, 0.0)
                let rotTrafo = Trafo3d.Rotation(0.0, panAngle, 0.0)
                let viewM2 = CameraView.ofTrafo (m.camera.view.ViewTrafo * rotTrafo)

        //transform region points to projection space
                let projM = Frustum.projTrafo(m.frustum)
                let viewProj = viewM2.ViewTrafo * projM
                let transformedpoints = region |> PList.toList |> List.map (fun p -> viewProj.Forward.TransformPosProj p) 
        //let normPoints = transformedpoints |> List.map(fun p -> ((V2d(p.X, p.Y) + V2d.One) * 0.5))
        
        //check if all of the points have values between 0 and 1 
        //let allInside = List.forall (fun (point:V2d) -> (( point.X > 0.0 && point.X < 1.0) && ( point.Y > 0.0 && point.Y < 1.0)) ) normPoints
                let allInside = List.forall (fun (point:V3d) -> (( point.X > -1.0 && point.X  < 1.0) && ( point.Y > -1.0 && point.Y < 1.0)) ) transformedpoints

                let points = region |> PList.toList
       //if true then ROI fits in frustum
                match allInside with
                    | true -> {m with camera =  {m.camera with view = viewM2} }
                    | false -> calcPanTiltValues m points viewM2
            
    





    let moveFrustum (m:RoverModel) = //(region:plist<V3d>)=
       
        let v = checkROIFullyInside m m.reg
        v
       
                

        //let viewM = m.camera.view.ViewTrafo
        //let projM = Frustum.projTrafo(m.frustum)
        //let viewProj = viewM * projM

        ////transform points to projection space
        //let transformedpoints = region |> PList.toList |> List.map (fun p -> viewProj.Forward.TransformPosProj p)

        ////set up bounding box
        //let boxPoints = transformedpoints |> List.map(fun p -> ((V2d(p.X, p.Y) + V2d.One) * 0.5))
        //let bBox =  boxPoints |> Box2d //coords between 0 and 1
        //let size = bBox.Size
        //let leftBottomP = V3d(bBox.Min,1.0)
      
        ////transform point back to view space
        //let invP = projM.Backward.TransformPos leftBottomP

        ////Rotating of the camera
        ////let iProj = viewM.Forward.TransformPos bBox.Center
        //let iProj = invP
        //let tiltAngle = atan2 -iProj.Y -iProj.Z
        //let panAngle = atan2 iProj.X -iProj.Z 

        //let rotTrafo = Trafo3d.Rotation(tiltAngle, panAngle, 0.0)
        //let newView = CameraView.ofTrafo (m.camera.view.ViewTrafo * rotTrafo)

       
        //{m with camera =  {m.camera with view = newView} }

     
       
        
     
       
        
  
      

        




    let changeCam (rover:RoverModel) (camtype:Option<CameraType>)=
        
        match camtype with
            |Some Camera60 -> 
                let fr = Frustum.perspective 60.0 0.1 10.0 1.0
                {rover with frustum = fr; currentCamType = Some Camera60}
            
            |Some Camera30 ->
                let fr = Frustum.perspective 30.0 0.1 10.0 1.0
                {rover with frustum = fr; currentCamType = Some Camera30}
            
            |Some Camera15 ->
                let fr = Frustum.perspective 15.0 0.1 10.0 1.0
                {rover with frustum = fr; currentCamType = Some Camera15}
            
            |Some Stereo -> rover //TODO 

            |None -> rover





    let update (rover:RoverModel) (action:RoverAction) =
        
        match action with
            |ChangePosition newPos -> {rover with position = newPos} 

            |ChangePan p -> 
                let rover' = setPan rover p
                panning rover'

            |ChangeTilt t -> 
                let rover' = setTilt rover t
                tilting rover'
            
            |MoveToRegion  ->
                moveFrustum rover 
            
            |SwitchCamera cam ->
                changeCam rover  cam
                
              
    


