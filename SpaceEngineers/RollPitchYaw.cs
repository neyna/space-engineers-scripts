﻿#region pre_script
/*
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
using SpaceEngineers.Game.ModAPI;

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

        public Func<IMyIntergridCommunicationSystem> IGC_ContextGetter { set => throw new NotImplementedException(); }

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

        #endregion pre_script

        // TODO
        // cockpit must have its bottom towards gravity, add parameter to tell which direction the cockpit is facing (TOP, BOTTOM, HORIZONTAL)


        // config
        string[] flightIndicatorsLcdNames = {""};
        string flightIndicatorsControllerName = "";
        const bool stalizableYaw = false; // do you want to stablize yaw to 0°
        const bool isPlanetWorld = true; // this should be true for every easy start or star system scenario, false if no planet in your scenario

        // end of config

        enum FlightMode {STABILIZATION, STANDY};
        FlightMode flightIndicatorsFlightMode = FlightMode.STANDY;
        List<IMyTextPanel> flightIndicatorsLcdDisplay = new List<IMyTextPanel>();
        IMyShipController flightIndicatorsShipController = null;

        const double pidP = 0.06f;
        const double pidI = 0.0f;
        const double pidD = 0.01f;

        BasicLibrary basicLibrary;
        LCDHelper lcdHelper;
        FlightIndicators flightIndicators;
        FightStabilizator fightStabilizator;

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
            basicLibrary = new BasicLibrary(GridTerminalSystem, Echo);
            lcdHelper = new LCDHelper(basicLibrary, new Color(0, 255, 0), 1.5f);
        }

        public void Main(string argument, UpdateType updateSource)
        {
            if (!TryInit())
            {
                return;
            }

            if (argument!=null && argument.ToLower().Equals("stabilize_on"))
            {                
                flightIndicatorsFlightMode = FlightMode.STABILIZATION;                
                fightStabilizator.Reset();
            } else if(argument != null && argument.ToLower().Equals("stabilize_off"))
            {
                flightIndicatorsFlightMode = FlightMode.STANDY;                    
                fightStabilizator.Release();                
            }            

            flightIndicators.Compute();
            if(flightIndicatorsFlightMode == FlightMode.STABILIZATION)
            {
                fightStabilizator.Stabilize(true, true, stalizableYaw);
            }

            lcdHelper.ClearMessageBuffer();
            lcdHelper.AppendMessageBuffer(flightIndicators.DisplayText());
            if (flightIndicatorsFlightMode == FlightMode.STABILIZATION)
            {
                lcdHelper.AppendMessageBuffer(fightStabilizator.DisplayText());
            }             
            lcdHelper.DisplayMessageBuffer(flightIndicatorsLcdDisplay);
        }

        public class FlightIndicators
        {
            IMyShipController shipController;
            readonly Action<string> Echo;
            readonly List<IMyTextPanel> lcdDisplays = null;
            private LCDHelper lcdHelper;
            Vector3D absoluteNorthVector;

            public double CurrentSpeed { get; private set; } = 0;
            public double Pitch { get; private set; } = 0;
            public double Roll { get; private set; } = 0;
            public double Yaw { get; private set; } = 0;
            public double Elevation { get; private set; } = 0;

            const double rad2deg = 180 / Math.PI;
            // thanks whip for those vectors
            static Vector3D absoluteNorthPlanetWorldsVector = new Vector3D(0, -1, 0);
            static Vector3D absoluteNorthNotPlanetWorldsVector = new Vector3D(0.342063708833718, -0.704407897782847, -0.621934025954579);

            public FlightIndicators(IMyShipController shipController, Action<String> Echo, bool isPlanetWorld = true, List<IMyTextPanel> lcdDisplays = null, LCDHelper lcdHelper =null)
            {
                this.shipController = shipController;
                this.Echo = Echo;
                this.lcdDisplays = lcdDisplays;
                this.lcdHelper = lcdHelper;
               
                if(isPlanetWorld)
                {
                    absoluteNorthVector = absoluteNorthPlanetWorldsVector;
                } else
                {
                    absoluteNorthVector = absoluteNorthNotPlanetWorldsVector;
                }
                

            }

            public void Compute()
            {
                // speed
                var velocityVector = shipController.GetShipVelocities().LinearVelocity;
                //CurrentSpeed = velocityVec.Length(); //raw speed of ship 
                CurrentSpeed = shipController.GetShipSpeed();

                // roll pitch yaw
                Vector3D shipForwardVector = shipController.WorldMatrix.Forward;
                Vector3D shipLeftVector = shipController.WorldMatrix.Left;
                Vector3D shipDownVector = shipController.WorldMatrix.Down;
                Vector3D gravityVector = shipController.GetNaturalGravity();
                Vector3D planetRelativeLeftVector = shipForwardVector.Cross(gravityVector);               

                if (gravityVector.LengthSquared() == 0)
                {
                    Echo("No natural gravity field detected");
                    Pitch = 0;
                    Roll = 0;
                    Yaw = 0;
                    Elevation = 0;
                    return;
                }
                // Roll
                Roll = VectorHelper.VectorAngleBetween(shipLeftVector, planetRelativeLeftVector) * rad2deg * Math.Sign(shipLeftVector.Dot(gravityVector));
                if (Roll > 90 || Roll < -90)
                {
                    Roll = 180 - Roll;
                }
                // Pitch
                Pitch = VectorHelper.VectorAngleBetween(shipForwardVector, gravityVector) * rad2deg; //angle from nose direction to gravity 
                Pitch -= 90; // value computed is 90 degrees if pitch = 0
                // Yaw
                Vector3D relativeEastVector = gravityVector.Cross(absoluteNorthVector);                
                Vector3D relativeNorthVector = relativeEastVector.Cross(gravityVector);
                Vector3D forwardProjectUp = VectorHelper.VectorProjection(shipForwardVector, gravityVector);
                Vector3D forwardProjPlaneVector = shipForwardVector - forwardProjectUp;

                //find angle from abs north to projected forward vector measured clockwise  
                Yaw = VectorHelper.VectorAngleBetween(forwardProjPlaneVector, relativeNorthVector) * rad2deg;
                if (shipForwardVector.Dot(relativeEastVector) < 0)
                {
                    Yaw = 360.0d - Yaw; //because of how the angle is measured                                                                          
                }

                double tempElevation = 0;
                if (!shipController.TryGetPlanetElevation(MyPlanetElevation.Surface, out tempElevation))
                {
                    Elevation = -1; //error, no gravity field is detected earlier, so it's another kind of problem
                }
                else
                {
                    Elevation = tempElevation;
                }

            }

            public string DisplayText()
            {
                StringBuilder stringBuilder = new StringBuilder();

                BasicLibrary.AppendFormattedNewLine(stringBuilder, "Speed     {0} m/s", Math.Round(CurrentSpeed, 2));
                BasicLibrary.AppendFormattedNewLine(stringBuilder, "Pitch       {0}°", Math.Round(Pitch, 2));
                BasicLibrary.AppendFormattedNewLine(stringBuilder, "Roll         {0}°", Math.Round(Roll, 2));
                BasicLibrary.AppendFormattedNewLine(stringBuilder, "Yaw        {0}°", Math.Round(Yaw, 2));
                BasicLibrary.AppendFormattedNewLine(stringBuilder, "Elevation {0} m", Math.Round(Elevation, 0));

                return stringBuilder.ToString();
            }

            public void Display()
            {
                if(lcdHelper == null || lcdDisplays == null)
                {
                    Echo("Can't diplay, LCD or LCDHelper not set");
                    return;
                }                             
              
                lcdHelper.DisplayMessage(DisplayText(), lcdDisplays);
            }

        }

        public class FightStabilizator
        {
            private FlightIndicators flightIndicators;
            public Action<string> Echo;
            BasicLibrary basicLibrary;
            IMyShipController shipController;
            PIDController pitchPid;
            PIDController rollPid;
            PIDController yawPid;

            public double gyroscopeOverridedRoll { get; private set; } = 0;
            public double gyroscopeOverridedPitch { get; private set; } = 0;
            public double gyroscopeOverridedYaw { get; private set; } = 0;

            bool firstRun = true;            
            double lastTime = 0;

            float gyroscopeMaximumGyroscopePower = 1.0f;
            float gyroscopeMaximumErrorMargin = 0.001f;

            public float pitchDesiredAngle = 0;
            public float yawDesiredAngle = 0;
            public float rollDesiredAngle = 0;

            string WarningMessage = null;
            List<IMyGyro> gyroscopes = new List<IMyGyro>();

            public FightStabilizator(FlightIndicators flightIndicators, IMyShipController shipController, double pidP, double pidI, double pidD, BasicLibrary basicLibrary)
            {
                this.flightIndicators = flightIndicators;
                this.Echo = basicLibrary.Echo;
                this.basicLibrary = basicLibrary;
                this.shipController = shipController;

                pitchPid = new PIDController(pidP, pidI, pidD);
                rollPid = new PIDController(pidP, pidI, pidD);
                yawPid = new PIDController(pidP, pidI, pidD);
            }

            public void Reset()
            {
                firstRun = true;
                FindAndInitGyroscopesOverdrive();

                pitchPid.Reset();
                rollPid.Reset();
                yawPid.Reset();
            }

            public void Release()
            {
                ReleaseGyroscopes();
            }

            public void Stabilize()
            {
                Stabilize(true, true, false);
            }

            public void Stabilize(bool stabilizeRoll, bool stabilizePitch, bool stabilizeYaw)
            {
                if (gyroscopes.Count == 0)
                {
                    WarningMessage = "Warning no gyro found.\nCan't stabilize ship.";
                    Echo(WarningMessage);
                    return;
                }

                float maxGyroValue = gyroscopes[0].GetMaximum<float>("Yaw") * gyroscopeMaximumGyroscopePower;
                
                // center yaw at origin
                double originCenteredYaw = flightIndicators.Yaw;
                if (originCenteredYaw > 180)
                {
                    originCenteredYaw -= 360;
                }

                double currentTime = BasicLibrary.GetCurrentTimeInMs();
                double timeStep = currentTime - lastTime;

                // sometimes time difference is 0 (because system is caching getTime calls), skip computing for this time
                if (timeStep == 0)
                {
                    return;
                }

                if (!firstRun)
                {                    
                    double pitchCommand = (stabilizePitch)?ComputeCommand(flightIndicators.Pitch - pitchDesiredAngle, pitchPid, timeStep, maxGyroValue):0;
                    double yawCommand = (stabilizeYaw)?ComputeCommand(originCenteredYaw - yawDesiredAngle, yawPid, timeStep, maxGyroValue):0;
                    double rollCommand = (stabilizeRoll)?ComputeCommand(flightIndicators.Roll - rollDesiredAngle, rollPid, timeStep, maxGyroValue):0;
                    // + rollCommand because of the way we compute it
                    ApplyGyroOverride( - pitchCommand, - yawCommand, rollCommand, gyroscopes, shipController); 
                }
                else
                {                    
                    firstRun = false;
                }

                // compute overriden gyro values into controller coordonates
                Vector3D overrideData = new Vector3D(-gyroscopes[0].Pitch, gyroscopes[0].Yaw, gyroscopes[0].Roll);
                MatrixD gyroscopeWorldMatrix = gyroscopes[0].WorldMatrix;
                MatrixD controllerWorldMatrix = shipController.WorldMatrix;
                Vector3D overrideDataInControllerView = Vector3D.TransformNormal(Vector3D.TransformNormal(overrideData, gyroscopeWorldMatrix), Matrix.Transpose(controllerWorldMatrix));

                gyroscopeOverridedPitch = overrideDataInControllerView.X;
                gyroscopeOverridedYaw = overrideDataInControllerView.Y;
                gyroscopeOverridedRoll = -overrideDataInControllerView.Z; // negative because of the way we compute roll           


                lastTime = BasicLibrary.GetCurrentTimeInMs();
            }

            double ComputeCommand(double error, PIDController pid, double timeStep, double maxGyroValue)
            {
                if (Math.Abs(error) > gyroscopeMaximumErrorMargin)
                {
                    double command = pid.Control(error, timeStep / 1000);
                    command = MathHelper.Clamp(command, -maxGyroValue, maxGyroValue);
                    return command;
                }
                else
                {
                    return 0.0d;
                }
                
            }

            public string DisplayText()
            {
                StringBuilder stringBuilder = new StringBuilder();
                BasicLibrary.AppendFormattedNewLine(stringBuilder, WarningMessage);
                if(gyroscopes.Count>0)
                {
                    BasicLibrary.AppendFormattedNewLine(stringBuilder, "Auto-correcting roll and pitch");
                    BasicLibrary.AppendFormattedNewLine(stringBuilder, "Pitch overdrive {0}", Math.Round(gyroscopeOverridedPitch, 4));
                    BasicLibrary.AppendFormattedNewLine(stringBuilder, "Roll overdrive  {0}", Math.Round(gyroscopeOverridedRoll, 4));
                    BasicLibrary.AppendFormattedNewLine(stringBuilder, "Yaw overdrive   {0}", Math.Round(gyroscopeOverridedYaw, 4));
                }                
                return stringBuilder.ToString();
            }

            void SetGyroscopesYawOverride(float overrride)
            {
                foreach (IMyGyro gyroscope in gyroscopes)
                {
                    gyroscope.Yaw = overrride;
                }
            }

            void FindAndInitGyroscopesOverdrive()
            {
                if (gyroscopes.Count == 0)
                {
                    basicLibrary.GetBlocksOfType(gyroscopes);
                    if (gyroscopes.Count == 0)
                    {
                        WarningMessage = "Warning no gyro found.";
                        Echo(WarningMessage);
                        return;
                    }
                    InitGyroscopesOverride();
                }
            }

            void InitGyroscopesOverride()
            {
                foreach (IMyGyro gyroscope in gyroscopes)
                {
                    gyroscope.GyroPower = 1.0f; // set power to 100%
                    gyroscope.GyroOverride = true;
                    gyroscope.Pitch = 0;
                    gyroscope.Roll = 0;
                    gyroscope.Yaw = 0;
                    gyroscope.ApplyAction("OnOff_On");
                }
            }

            void ReleaseGyroscopes()
            {
                foreach (IMyGyro gyroscope in gyroscopes)
                {
                    gyroscope.Roll = 0;
                    gyroscope.Pitch = 0;
                    gyroscope.Yaw = 0;
                    gyroscope.GyroOverride = false;
                    gyroscope.GyroPower = 1.0f;
                }
            }

            // thanks Whip for your help
            // Whip's ApplyGyroOverride Method v9 - 8/19/17
            void ApplyGyroOverride(double pitch_speed, double yaw_speed, double roll_speed, List<IMyGyro> gyro_list, IMyTerminalBlock reference)
            {
                var rotationVec = new Vector3D(-pitch_speed, yaw_speed, roll_speed); //because keen does some weird stuff with signs
                var shipMatrix = reference.WorldMatrix;
                var relativeRotationVec = Vector3D.TransformNormal(rotationVec, shipMatrix);
                foreach (var thisGyro in gyro_list)
                {
                    var gyroMatrix = thisGyro.WorldMatrix;
                    var transformedRotationVec = Vector3D.TransformNormal(relativeRotationVec, Matrix.Transpose(gyroMatrix));
                    thisGyro.Pitch = (float)transformedRotationVec.X;
                    thisGyro.Yaw = (float)transformedRotationVec.Y;
                    thisGyro.Roll = (float)transformedRotationVec.Z;
                    thisGyro.GyroOverride = true;
                }
            }
        }       

        public static class VectorHelper
        {
            // in radians
            public static double VectorAngleBetween(Vector3D a, Vector3D b) 
            {
                if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
                    return 0;
                else
                    return Math.Acos(MathHelper.Clamp(a.Dot(b) / Math.Sqrt(a.LengthSquared() * b.LengthSquared()), -1, 1));
            }

            public static Vector3D VectorProjection(Vector3D vectorToProject, Vector3D projectsToVector)
            {
                if (Vector3D.IsZero(projectsToVector))
                    return Vector3D.Zero;

                return vectorToProject.Dot(projectsToVector) / projectsToVector.LengthSquared() * projectsToVector;
            }

        }

      

        bool TryInit()
        {
            // LCD
            if(flightIndicatorsLcdDisplay.Count==0)
            {
                if(flightIndicatorsLcdNames!=null && flightIndicatorsLcdNames.Length>0 && flightIndicatorsLcdNames[0].Length>0)
                {
                    flightIndicatorsLcdDisplay.AddList(lcdHelper.Find(flightIndicatorsLcdNames));
                } else
                {
                    IMyTextPanel textPanel = lcdHelper.FindFirst();
                    if (textPanel != null)
                    {
                        flightIndicatorsLcdDisplay.Add(textPanel);
                    } else
                    {
                        Echo("Cound not find any LCD");                        
                    }
                }
                if(flightIndicatorsLcdDisplay.Count==0)
                {
                    return false;
                }
                
            }

            
            // Controller
            if (flightIndicatorsShipController == null)
            {
                if(flightIndicatorsControllerName != null && flightIndicatorsControllerName.Length != 0)
                {
                    IMyTerminalBlock namedController = GridTerminalSystem.GetBlockWithName(flightIndicatorsControllerName);
                    if(namedController == null)
                    {
                        string message = "No controller named \n" + flightIndicatorsControllerName + " found.";
                        Echo(message);
                        lcdHelper.DisplayMessage(message, flightIndicatorsLcdDisplay);
                        return false;
                    }
                    flightIndicatorsShipController = (IMyShipController) namedController;
                } else
                {
                    List<IMyShipController> shipControllers = new List<IMyShipController>();
                    GridTerminalSystem.GetBlocksOfType<IMyShipController>(shipControllers);

                    if (shipControllers.Count != 0)
                    {
                        flightIndicatorsShipController = shipControllers[0];
                    }
                    else
                    {
                        string message = "No controller found.";
                        Echo(message);
                        lcdHelper.DisplayMessage(message, flightIndicatorsLcdDisplay);
                        return false;
                    }
                }                                                           
            }

            if (flightIndicators == null)
            {
                flightIndicators = new FlightIndicators(flightIndicatorsShipController, Echo, isPlanetWorld, flightIndicatorsLcdDisplay, lcdHelper);
            }

            if( fightStabilizator == null)
            {
                fightStabilizator = new FightStabilizator(flightIndicators, flightIndicatorsShipController, pidP, pidI, pidD, basicLibrary);
            }

            return true;
        }     

      

       
        public class PIDController
        {
            double p = 0;
            double i = 0;
            double d = 0;

            double errorIntegral = 0;
            double lastError = 0;

            bool firstRun = true;

            public PIDController(double p, double i, double d)
            {
                this.p = p;
                this.i = i;
                this.d = d;
            }

            public double Control(double error, double timeStep)
            {
                double errorDerivative;

                if (firstRun)
                {
                    errorDerivative = 0;
                    firstRun = false;
                }
                else
                {
                    errorDerivative = (error - lastError) / timeStep;
                }

                lastError = error;

                errorIntegral += error * timeStep;                
                return p * error + i * errorIntegral + d * errorDerivative;
            }

            public void Reset()
            {
                errorIntegral = 0;
                lastError = 0;
                firstRun = true;
            }
        }


        //
        // Neyna LCD LIBRARY
        // Free to use library for space engineers modders. Just credit me and link to this library in your creations workshop pages.
        // https://steamcommunity.com/workshop/filedetails/?id=1404290522
        //

        public class LCDHelper
        {
            public Color defaultFontColor = new Color(255, 255, 255);
            public float defaultSize = 2;
            BasicLibrary basicLibrary;
            StringBuilder messageBuffer = new StringBuilder();

            public LCDHelper(BasicLibrary basicLibrary)
            {
                this.basicLibrary = basicLibrary;
            }

            public LCDHelper(BasicLibrary basicLibrary, Color defaultFontColor, float defaultSize = 2)
            {
                this.basicLibrary = basicLibrary;
                this.defaultFontColor = defaultFontColor;
                this.defaultSize = defaultSize;
            }

            public void DisplayMessage(string message, List<IMyTextPanel> myTextPanels, bool append = false)
            {
                foreach (IMyTextPanel myTextPanel in myTextPanels)
                {
                    DisplayMessage(message, myTextPanel, append);
                }
            }

            public void DisplayMessage(string message, IMyTextPanel myTextPanel, bool append = false)
            {
                myTextPanel.WritePublicText(message, append);
            }

            // return null if no lcd
            public IMyTextPanel FindFirst()
            {
                IMyTextPanel lcd = basicLibrary.FindFirstBlockByType<IMyTextPanel>();
                if (lcd != null)
                {
                    InitDisplay(lcd);
                }
                return lcd;
            }

            // return all lcd in groups + all lcd by names
            public List<IMyTextPanel> Find(string[] lcdGoupsAndNames)
            {
                List<IMyTextPanel> lcds = basicLibrary.FindBlocksByNameAndGroup<IMyTextPanel>(lcdGoupsAndNames, "LCD");
                InitDisplays(lcds);
                return lcds;
            }


            public void InitDisplays(List<IMyTextPanel> myTextPanels)
            {
                InitDisplays(myTextPanels, defaultFontColor, defaultSize);
            }

            public void InitDisplay(IMyTextPanel myTextPanel)
            {
                InitDisplay(myTextPanel, defaultFontColor, defaultSize);
            }

            public void InitDisplays(List<IMyTextPanel> myTextPanels, Color color, float fontSize)
            {
                foreach (IMyTextPanel myTextPanel in myTextPanels)
                {
                    InitDisplay(myTextPanel, color, fontSize);
                }
            }

            public void InitDisplay(IMyTextPanel myTextPanel, Color color, float fontSize)
            {
                myTextPanel.ShowPublicTextOnScreen();
                myTextPanel.FontColor = color;
                myTextPanel.FontSize = fontSize;
                myTextPanel.ApplyAction("OnOff_On");
            }


            public void ClearMessageBuffer()
            {
                messageBuffer.Clear();
            }

            public void AppendMessageBuffer(string text)
            {
                messageBuffer.Append(text);
            }

            // this method does not have append boolean parameter because the plan is to use it only with a complete screen message to prevent flickering
            public void DisplayMessageBuffer(List<IMyTextPanel> myTextPanels)
            {
                DisplayMessage(messageBuffer.ToString(), myTextPanels);
            }
        }


        //
        // END LCD LIBRARY CODE
        //


        //
        // Neyna BASIC LIBRARY
        // Free to use library for space engineers modders. Just credit me and link to this library in your creations workshop pages.
        // https://steamcommunity.com/workshop/filedetails/?id=1404290522
        //

        public class BasicLibrary
        {
            IMyGridTerminalSystem GridTerminalSystem;
            public Action<string> Echo;

            public BasicLibrary(IMyGridTerminalSystem GridTerminalSystem, Action<string> Echo)
            {
                this.GridTerminalSystem = GridTerminalSystem;
                this.Echo = Echo;
            }

            public T FindFirstBlockByType<T>() where T : class
            {
                List<T> temporaryList = new List<T>();
                GridTerminalSystem.GetBlocksOfType(temporaryList);
                if (temporaryList.Count > 0)
                {
                    return temporaryList[0];
                }
                return null;
            }

            public List<T> FindBlocksByNameAndGroup<T>(string[] names, string typeOfBlockForMessage) where T : class
            {
                List<T> result = new List<T>();

                List<T> temporaryList = new List<T>();
                List<T> allBlockList = new List<T>();
                GridTerminalSystem.GetBlocksOfType(allBlockList);

                if (names == null) return result;
                for (int i = 0; i < names.Length; i++)
                {
                    if (names[i].Length == 0)
                    {
                        break;
                    }
                    IMyBlockGroup blockGroup = GridTerminalSystem.GetBlockGroupWithName(names[i]);
                    if (blockGroup != null)
                    {
                        temporaryList.Clear();
                        blockGroup.GetBlocksOfType(temporaryList);
                        if (temporaryList.Count == 0)
                        {
                            Echo($"Warning : group {names[i]} has no {typeOfBlockForMessage}.");
                        }
                        result.AddList(temporaryList);
                    }
                    else
                    {
                        bool found = false;
                        foreach (T block in allBlockList)
                        {
                            if (((IMyTerminalBlock)block).CustomName == names[i])
                            {
                                result.Add(block);
                                found = true;
                                break;
                            }

                        }
                        if (!found)
                        {
                            Echo($"Warning : {typeOfBlockForMessage} or group named\n{names[i]} not found.");
                        }
                    }
                }
                return result;
            }

            public void GetBlocksOfType<T>(List<T> list, Func<T, bool> collect = null) where T : class
            {
                GridTerminalSystem.GetBlocksOfType(list, collect);
            }

            public static void AppendFormatted(StringBuilder stringBuilder, string stringToFormat, params object[] args)
            {
                if (stringToFormat != null && stringToFormat.Length > 0)
                {
                    stringBuilder.Append(string.Format(stringToFormat, args));
                }
            }

            public static void AppendFormattedNewLine(StringBuilder stringBuilder, string stringToFormat, params object[] args)
            {
                AppendFormatted(stringBuilder, stringToFormat, args);
                stringBuilder.Append('\n');
            }

            static readonly DateTime dt1970 = new DateTime(1970, 1, 1);
            public static double GetCurrentTimeInMs()
            {
                DateTime time = System.DateTime.Now;
                TimeSpan timeSpan = time - dt1970;
                return timeSpan.TotalMilliseconds;
            }
        }

        //
        // END OF BASIC LIBRARY
        //








        #region post_script
    }

}

#endregion post_script
