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




        // config
        string[] flightIndicatorsLcdNames = { };     
        string flightIndicatorsControllerName = ""; // TODO use it

        // end of config
        List<IMyTextPanel> flightIndicatorsLcdDisplay = new List<IMyTextPanel>();
        IMyShipController flightIndicatorsShipController = null;
        double flightIndicatorsShipControllerCurrentSpeed = 0;
        Vector3D flightIndicatorsShipControllerAbsoluteNorthVec;
        double flightIndicatorsPitch;
        double flightIndicatorsRoll;
        double flightIndicatorsYaw;
        double flightIndicatorsElevation = 0;

        const double flightIndicatorsRad2deg = 180 / Math.PI;
        const double flightIndicatorsDeg2rad = Math.PI / 180;

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
        }

        public void Main(string argument, UpdateType updateSource)
        {
            if (!TryInit())
            {
                return;
            }
            Compute();
            Display();
        }

        private bool TryInit()
        {
            if(flightIndicatorsLcdDisplay.Count==0)
            {
                if (flightIndicatorsLcdNames.Length == 0)
                {
                    flightIndicatorsLcdDisplay.Add(FindFirstLcd());
                } else
                {
                    flightIndicatorsLcdDisplay.AddList(FindLcds(flightIndicatorsLcdNames));
                }
                
                if (flightIndicatorsLcdDisplay.Count == 0)
                {
                    Echo("Cound not find any LCD");
                    return false;
                }
            }
            
            if(flightIndicatorsShipController == null)
            {
                List<IMyShipController> shipControllers = new List<IMyShipController>();
                GridTerminalSystem.GetBlocksOfType<IMyShipController>(shipControllers);

                if (shipControllers.Count != 0)
                {
                    flightIndicatorsShipController = shipControllers[0];
                } else
                {
                    Echo("No controller found");
                    return false;
                }                

                // compute absoluteNorthVec
                Vector3D shipForwardVec = flightIndicatorsShipController.WorldMatrix.Forward;
                Vector3D gravityVec = flightIndicatorsShipController.GetNaturalGravity();
                Vector3D planetRelativeLeftVec = shipForwardVec.Cross(gravityVec);
                flightIndicatorsShipControllerAbsoluteNorthVec = planetRelativeLeftVec;
            }
            
            return true;
        }

        private void Display()
        {
            StringBuilder stringBuilder = new StringBuilder();
            WriteOutput(stringBuilder, "Speed {0} m/s", Math.Round(flightIndicatorsShipControllerCurrentSpeed, 2));
            WriteOutput(stringBuilder, "Pitch {0}°", Math.Round(flightIndicatorsPitch, 2));
            WriteOutput(stringBuilder, "Roll {0}°", Math.Round(flightIndicatorsRoll, 2));
            WriteOutput(stringBuilder, "Yaw {0}°", Math.Round(flightIndicatorsYaw, 2));
            WriteOutput(stringBuilder, "Elevation {0} m", Math.Round(flightIndicatorsElevation, 0));
            LcdDisplayMessage(stringBuilder.ToString(), flightIndicatorsLcdDisplay);
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
            flightIndicatorsRoll = VectorAngleBetween(shipLeftVec, planetRelativeLeftVec) * flightIndicatorsRad2deg * Math.Sign(shipLeftVec.Dot(gravityVec));
            if (flightIndicatorsRoll > 90 || flightIndicatorsRoll < -90)
            {
                flightIndicatorsRoll = 180 - flightIndicatorsRoll; //accounts for upsidedown 
            }
            // Pitch
            flightIndicatorsPitch = VectorAngleBetween(shipForwardVec, gravityVec) * flightIndicatorsRad2deg; //angle from nose direction to gravity 
            flightIndicatorsPitch -= 90; //as 90 degrees is level with ground 
            // Yaw
            //get east vector  
            Vector3D relativeEastVec = gravityVec.Cross(flightIndicatorsShipControllerAbsoluteNorthVec);

            //get relative north vector  
            Vector3D relativeNorthVec = relativeEastVec.Cross(gravityVec);
            Vector3D forwardProjectUp = VectorProjection(shipForwardVec, gravityVec);
            Vector3D forwardProjPlaneVec = shipForwardVec - forwardProjectUp;

            //find angle from abs north to projected forward vector measured clockwise  
            flightIndicatorsYaw = VectorAngleBetween(forwardProjPlaneVec, relativeNorthVec) * flightIndicatorsRad2deg;
            if (shipForwardVec.Dot(relativeEastVec) < 0)
            {
                flightIndicatorsYaw = 360 - flightIndicatorsYaw; //because of how the angle is measured  
            }

            if(!flightIndicatorsShipController.TryGetPlanetElevation(MyPlanetElevation.Surface, out flightIndicatorsElevation))
            {
                flightIndicatorsElevation = -1; //error, no gravity field is detected earlier, so it's another kind of problem
            }
                                    
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

        void WriteOutput(StringBuilder output, string fmt, params object[] args)
        {
            output.Append(string.Format(fmt, args));
            output.Append('\n');
        }



        //
        // LCD library code
        // IMyTextPanel FindFirstLcd()
        // List<IMyTextPanel> FindLcds(string[] lcdGoupsAndNames)
        // void InitDisplays(List<IMyTextPanel> myTextPanels)
        // void InitDisplay(IMyTextPanel myTextPanel)

        Color defaultFontColor = new Color(150, 30, 50);

        private void LcdDisplayMessage(string message, List<IMyTextPanel> myTextPanels, bool append = false)
        {
            foreach (IMyTextPanel myTextPanel in myTextPanels)
            {
                myTextPanel.WritePublicText(message, append);
            }
        }

        // return null if no lcd
        private IMyTextPanel FindFirstLcd()
        {
            List<IMyTextPanel> temporaryLcdList = new List<IMyTextPanel>();
            GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(temporaryLcdList);
            if (temporaryLcdList.Count > 0)
            {
                return temporaryLcdList[0];
            }
            return null;
        }

        // return all lcd in groups + all lcd by names
        private List<IMyTextPanel> FindLcds(string[] lcdGoupsAndNames)
        {
            List<IMyTextPanel> lcds = new List<IMyTextPanel>();
            List<IMyTextPanel> temporaryLcdList = new List<IMyTextPanel>();

            List<IMyTextPanel> allLcdList = new List<IMyTextPanel>();
            GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(allLcdList);
            // get groups
            for (int i = 0; i < lcdGoupsAndNames.Length; i++)
            {
                IMyBlockGroup lcdGroup = GridTerminalSystem.GetBlockGroupWithName(lcdGoupsAndNames[i]);
                if (lcdGroup != null)
                {
                    temporaryLcdList.Clear();
                    lcdGroup.GetBlocksOfType<IMyTextPanel>(temporaryLcdList);
                    if (temporaryLcdList.Count == 0)
                    {
                        Echo("Warning : group " + lcdGoupsAndNames[i] + " has no LCD.");
                    }
                    lcds.AddList(temporaryLcdList);
                }
                else
                {
                    bool found = false;
                    foreach (IMyTextPanel myTextPanel in allLcdList)
                    {
                        if (myTextPanel.CustomName == lcdGoupsAndNames[i])
                        {
                            lcds.Add(myTextPanel);
                            found = true;
                            break;
                        }

                    }
                    if (!found)
                    {
                        Echo("Warning : LCD named " + lcdGoupsAndNames[i] + " not found (and is not a group).");
                    }
                }
            }
            InitDisplays(lcds);
            return lcds;
        }


        private void InitDisplays(List<IMyTextPanel> myTextPanels)
        {
            InitDisplays(myTextPanels, defaultFontColor);
        }

        private void InitDisplay(IMyTextPanel myTextPanel)
        {
            InitDisplay(myTextPanel, defaultFontColor);
        }

        private void InitDisplays(List<IMyTextPanel> myTextPanels, Color color)
        {
            foreach (IMyTextPanel myTextPanel in myTextPanels)
            {
                InitDisplay(myTextPanel, color);
            }
        }

        private void InitDisplay(IMyTextPanel myTextPanel, Color color)
        {
            myTextPanel.FontColor = color;
            myTextPanel.FontSize = (Single)2;
            myTextPanel.ApplyAction("OnOff_On");
        }

        //
        // END LCD LIBRARY CODE
        //





    }

}
