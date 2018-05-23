using System;

/*
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI;

Sandbox.Common.dll
Sandbox.Game.dll
VRage.Game.dll
VRage.Library.dll
VRage.Math.dll


////
Sandbox.Game.dll
Sandbox.Common.dll

SpaceEngineers.Game.dll

Vrage.Library.dll
Vrage.Math.dll
Vrage.Game.dll

*/
using System;
using System.Collections.Generic;
using VRageMath;
using VRage.Game;
using VRage.Library;
using System.Text;
using Sandbox.ModAPI.Interfaces;
using Sandbox.ModAPI.Ingame;
using Sandbox.Common;
using Sandbox.Game;
using VRage.Collections;
using VRage.Game.ModAPI.Ingame;
//using SpaceEngineers.Game.ModAPI.Ingame;

namespace RollPitchYaw
{
    public abstract class Program : Sandbox.ModAPI.IMyGridProgram
    {
        public Sandbox.ModAPI.Ingame.IMyGridTerminalSystem GridTerminalSystem { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public Sandbox.ModAPI.Ingame.IMyProgrammableBlock Me { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public TimeSpan ElapsedTime { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string Storage { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public IMyGridProgramRuntimeInfo Runtime { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public Action<string> Echo { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public bool HasMainMethod => throw new NotImplementedException();
        public bool HasSaveMethod => throw new NotImplementedException();
        public void Main(string argument)
        {
            throw new NotImplementedException();
        }

        public void Save()
        {
            throw new NotImplementedException();
        }

        /*
        public void Main(string argument, UpdateType updateSource)
        {
            throw new NotImplementedException();
        }
        */




        
        string flightIndicatorsLcdName = "";
        bool flightIndicatorsShouldAutoFindLcd = true; // TODO use it
        string flightIndicatorsControllerName = ""; // TODO use it
        bool flightIndicatorsShouldAutoFindController = true; // TODO use it

        IMyShipController flightIndicatorsShipController = null;
        IMyTextPanel flightIndicatorsLcdDisplay = null;
        double flightIndicatorsShipControllerCurrentSpeed = 0;
        Vector3D flightIndicatorsShipControllerAbsoluteNorthVec;
        double flightIndicatorsPitch;
        double flightIndicatorsRoll;
        double flightIndicatorsYaw;
        double flightIndicatorsElevation = 0;

        const double rad2deg = 180 / Math.PI;
        const double deg2rad = Math.PI / 180;

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
            Init();
        }

        private void Init()
        {
            List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(blocks);
            if (blocks.Count == 0)
            {
                throw new Exception("No LCD Screen");
            }
            flightIndicatorsLcdDisplay = (IMyTextPanel)blocks[1];
            flightIndicatorsLcdDisplay.SetValue("FontColor", new Color(150, 30, 50));
            flightIndicatorsLcdDisplay.SetValue<Single>("FontSize", (Single)2);
            flightIndicatorsLcdDisplay.ApplyAction("OnOff_On");

            List<IMyShipController> shipControllers = new List<IMyShipController>();
            GridTerminalSystem.GetBlocksOfType<IMyShipController>(shipControllers);

            flightIndicatorsShipController = GetControlledShipController(shipControllers);
            if (flightIndicatorsShipController == null && shipControllers.Count != 0)
            {
                flightIndicatorsShipController = shipControllers[0];
            }
            if (flightIndicatorsShipController == null)
            {
                throw new Exception("No user detected in any control seats");
            }

            // compute absoluteNorthVec
            Vector3D shipForwardVec = flightIndicatorsShipController.WorldMatrix.Forward;
            Vector3D gravityVec = flightIndicatorsShipController.GetNaturalGravity();
            Vector3D planetRelativeLeftVec = shipForwardVec.Cross(gravityVec);
            flightIndicatorsShipControllerAbsoluteNorthVec = planetRelativeLeftVec;
        }


        public void Main(string argument, UpdateType updateSource)
        {
            Compute();
            flightIndicatorsLcdDisplay.WritePublicText("Speed " + flightIndicatorsShipControllerCurrentSpeed.ToString("N2") + " m/s" + "\n");
            flightIndicatorsLcdDisplay.WritePublicText("Pitch " + flightIndicatorsPitch.ToString("N2") + "°\n", true);
            flightIndicatorsLcdDisplay.WritePublicText("Roll " + flightIndicatorsRoll.ToString("N2") + "°\n", true);
            flightIndicatorsLcdDisplay.WritePublicText("Yaw " + flightIndicatorsYaw.ToString("N2") + "°\n", true);
            flightIndicatorsLcdDisplay.WritePublicText("Elevation " + flightIndicatorsElevation.ToString("N0") + " m\n", true);
        }


        private void Compute()
        {
            // speed
            var velocityVec = flightIndicatorsShipController.GetShipVelocities().LinearVelocity;
            //CurrentSpeed = velocityVec.Length(); //raw speed of ship 
            flightIndicatorsShipControllerCurrentSpeed = flightIndicatorsShipController.GetShipSpeed();

            // roll pitch yaw
            Vector3D shipForwardVec = flightIndicatorsShipController.WorldMatrix.Forward;
            Vector3D shipLeftVec = flightIndicatorsShipController.WorldMatrix.Left;
            Vector3D shipDownVec = flightIndicatorsShipController.WorldMatrix.Down;
            Vector3D gravityVec = flightIndicatorsShipController.GetNaturalGravity();
            Vector3D planetRelativeLeftVec = shipForwardVec.Cross(gravityVec);

            // il est possible de prendre planetRelativeLeftVec comme vector nord absolu à l'init de programme
            //Vector3D absoluteNorthVec = new Vector3D(0, -1, 0); // new Vector3D(0.342063708833718, -0.704407897782847, -0.621934025954579); if not planet worlds

            if (gravityVec.LengthSquared() == 0)
            {
                Echo("No natural gravity field detected");
                flightIndicatorsPitch = 0;
                flightIndicatorsRoll = 0;
                flightIndicatorsYaw = 0;
                flightIndicatorsElevation = 0;
                return;
            }
            // Roll
            flightIndicatorsRoll = VectorAngleBetween(shipLeftVec, planetRelativeLeftVec) * rad2deg * Math.Sign(shipLeftVec.Dot(gravityVec));
            if (flightIndicatorsRoll > 90 || flightIndicatorsRoll < -90)
            {
                flightIndicatorsRoll = 180 - flightIndicatorsRoll; //accounts for upsidedown 
            }
            // Pitch
            flightIndicatorsPitch = VectorAngleBetween(shipForwardVec, gravityVec) * rad2deg; //angle from nose direction to gravity 
            flightIndicatorsPitch -= 90; //as 90 degrees is level with ground 
            // Yaw
            //get east vector  
            Vector3D relativeEastVec = gravityVec.Cross(flightIndicatorsShipControllerAbsoluteNorthVec);

            //get relative north vector  
            Vector3D relativeNorthVec = relativeEastVec.Cross(gravityVec);
            Vector3D forwardProjectUp = VectorProjection(shipForwardVec, gravityVec);
            Vector3D forwardProjPlaneVec = shipForwardVec - forwardProjectUp;

            //find angle from abs north to projected forward vector measured clockwise  
            flightIndicatorsYaw = VectorAngleBetween(forwardProjPlaneVec, relativeNorthVec) * rad2deg;
            if (shipForwardVec.Dot(relativeEastVec) < 0)
            {
                flightIndicatorsYaw = 360 - flightIndicatorsYaw; //because of how the angle is measured  
            }

            if(!flightIndicatorsShipController.TryGetPlanetElevation(MyPlanetElevation.Surface, out flightIndicatorsElevation))
            {
                flightIndicatorsElevation = -1; //error, no gravity field is detected earlier, so it's another kind of problem
            }
                                    
        }

        IMyShipController GetControlledShipController(List<IMyShipController> SCs)
        {
            foreach (IMyShipController thisController in SCs)
            {
                if (thisController.IsUnderControl && thisController.CanControlShip)
                    return thisController;
            }

            return null;
        }

        double VectorAngleBetween(Vector3D a, Vector3D b) //returns radians 
        {
            if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
                return 0;
            else
                return Math.Acos(MathHelper.Clamp(a.Dot(b) / Math.Sqrt(a.LengthSquared() * b.LengthSquared()), -1, 1));
        }

        Vector3D VectorProjection(Vector3D a, Vector3D b)
        {
            if (Vector3D.IsZero(b))
                return Vector3D.Zero;

            return a.Dot(b) / b.LengthSquared() * b;
        }







    }

}
