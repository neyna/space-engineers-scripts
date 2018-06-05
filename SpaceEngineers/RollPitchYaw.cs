#region pre_script
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
using SpaceEngineers.Game.ModAPI;
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


        // TODO
        // cockpit must have its bottom towards gravity, add parameter to tell which direction the cockpit is facing (TOP, BOTTOM, HORIZONTAL)

        #endregion pre_script
       

        // config
        string[] flightIndicatorsLcdNames = {""};
        string flightIndicatorsControllerName = "";

        // end of config
        enum FlightMode {CALIBRATION, STABILIZATION, STANDY};
        FlightMode flightIndicatorsFlightMode = FlightMode.STANDY;
        List<IMyTextPanel> flightIndicatorsLcdDisplay = new List<IMyTextPanel>();
        IMyShipController flightIndicatorsShipController = null;
        double flightIndicatorsShipControllerCurrentSpeed = 0;
        Vector3D flightIndicatorsShipControllerAbsoluteNorthVec;
        double flightIndicatorsPitch;
        double flightIndicatorsRoll;
        double flightIndicatorsYaw;
        double flightIndicatorsElevation = 0;

        List<IMyGyro> flightIndicatorsGyroscopes = new List<IMyGyro>();
        PIDController flightIndicatorsPID = new PIDController(0.06, 0, 0.01);
        float flightIndicatorsGyroscopeMaximumGyroscopePower = 0.3f;
        float flightIndicatorsGyroscopeMaximumErrorMargin = 0.001f;
        float flightIndicatorsDesiredAngle = 0;
        string flightIndicatorsWarningMessage = null;

        const double flightIndicatorsRad2deg = 180 / Math.PI;
        const double flightIndicatorsDeg2rad = Math.PI / 180;

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update10;            
        }

        public void Main(string argument, UpdateType updateSource)
        {            
            if(argument!=null && argument.ToLower().Equals("stabilize_on"))
            {                
                flightIndicatorsFlightMode = FlightMode.STABILIZATION;
                Me.CustomData = "";
                firstRun = true;
                FlightIndicatorsFindAndInitGyroscopesOverdrive();
                flightIndicatorsPID.Reset();
            } else if(argument != null && argument.ToLower().Equals("stabilize_off"))
            {
                flightIndicatorsFlightMode = FlightMode.STANDY;
                //Me.CustomData = "";               
                FlightIndicatorsReleaseGyroscopes();
            } else if (argument != null && argument.ToLower().Equals("calibration"))
            {
                Runtime.UpdateFrequency = UpdateFrequency.Update1;
                Me.CustomData = "";
                flightIndicatorsFlightMode = FlightMode.CALIBRATION;
                // TODO bring back the ship to 0/0/0 for optimal test
                FlightIndicatorsFindAndInitGyroscopesOverdrive();
            } else if(argument != null && argument.ToLower().Equals("abort_calibration"))
            {
                //Me.CustomData = "";
                EndCalibrationConfig();
            }

            if (!TryInit())
            {
                return;
            }
            FlightIndicatorsCompute();
            if(flightIndicatorsFlightMode == FlightMode.STABILIZATION)
            {
                FlightIndicatorsCorrectRollAndPitch();
            } else if(flightIndicatorsFlightMode == FlightMode.CALIBRATION)
            {
                FlightIndicatorsCalibration();
            }
                
            FlightIndicatorsDisplay();            
        }

        void EndCalibrationConfig()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
            flightIndicatorsFlightMode = FlightMode.STANDY;
            FlightIndicatorsReleaseGyroscopes();
        }     
  

        void FlightIndicatorsCalibration()
        {
       
        }

       
        void SetGyroscopesYawOverride(float overrride)
        {
            foreach (IMyGyro gyroscope in flightIndicatorsGyroscopes)
            {
                gyroscope.Yaw = overrride;
            }
        }


        float t_gyroRoll;
        float t_gyroPitch;
        float t_gyroYaw;

        bool firstRun = true;        
 
        double error = 0;
        double command = 0;
        double lastTime = 0;


        void FlightIndicatorsCorrectRollAndPitch()
        {            
            float maxYaw = flightIndicatorsGyroscopes[0].GetMaximum<float>("Yaw") * flightIndicatorsGyroscopeMaximumGyroscopePower;                      
            
            double tempYaw= flightIndicatorsYaw;
            if (tempYaw > 180)
            {
                tempYaw -= 360;
            }
                                    
            error = 0;            
            double currentTime = GetCurrentTimeInMs();
            double timeStep = currentTime - lastTime;

            // sometimes time difference is 0 (because system is caching getTime calls), skip computing for this time
            if(timeStep==0)
            {
                return;
            }

            if(!firstRun)
            {
                error = tempYaw - flightIndicatorsDesiredAngle;                
                if (Math.Abs(error)> flightIndicatorsGyroscopeMaximumErrorMargin)
                {
                    // using old command + error correction given by PID ? or PID gives corrected value?
                    command = - flightIndicatorsPID.Control(error, timeStep / 1000);
                    command = MathHelper.Clamp(command, -maxYaw, maxYaw);
                    ApplyGyroOverride(0, command, 0, flightIndicatorsGyroscopes, flightIndicatorsShipController);
                } else
                {                    
                    ApplyGyroOverride(0, 0, 0, flightIndicatorsGyroscopes, flightIndicatorsShipController);
                }
            } else
            {
                command = (tempYaw > 0) ? -maxYaw / 100 : maxYaw / 100;
                firstRun = false;
            }
            //Me.CustomData += $"command : {Math.Round(command,4)} - timeStep : {Math.Round(timeStep,0)} - error : {Math.Round(Math.Abs(error),2)}\n";
            
            t_gyroPitch = flightIndicatorsGyroscopes[0].Pitch;
            t_gyroRoll = flightIndicatorsGyroscopes[0].Roll;
            t_gyroYaw = flightIndicatorsGyroscopes[0].Yaw;
            lastTime = GetCurrentTimeInMs();
        }

        void FlightIndicatorsFindAndInitGyroscopesOverdrive()
        {
            if (flightIndicatorsGyroscopes.Count == 0)
            {
                GridTerminalSystem.GetBlocksOfType(flightIndicatorsGyroscopes);
                if (flightIndicatorsGyroscopes.Count == 0)
                {
                    flightIndicatorsWarningMessage = "Warning no gyro found.";
                    Echo(flightIndicatorsWarningMessage);
                    return;
                }
                FlightIndicatorsInitGyroscopesOverride();
            }
        }

        void FlightIndicatorsInitGyroscopesOverride()
        {
            foreach (IMyGyro gyroscope in flightIndicatorsGyroscopes)
            {
                gyroscope.GyroPower = 1.0f; // set power to 100%
                gyroscope.GyroOverride = true;
                gyroscope.Pitch = 0;
                gyroscope.Roll = 0;
                gyroscope.Yaw = 0;
                gyroscope.ApplyAction("OnOff_On");
            }
        }

        void FlightIndicatorsReleaseGyroscopes()
        {
            foreach (IMyGyro gyroscope in flightIndicatorsGyroscopes)
            {   
                gyroscope.Roll = 0;
                gyroscope.Pitch = 0;
                gyroscope.Yaw = 0;
                gyroscope.GyroOverride = false;
                gyroscope.GyroPower = 1.0f;
            }
        }        

        bool TryInit()
        {
            // LCD
            if(flightIndicatorsLcdDisplay.Count==0)
            {
                if(flightIndicatorsLcdNames!=null && flightIndicatorsLcdNames.Length>0 && flightIndicatorsLcdNames[0].Length>0)
                {
                    flightIndicatorsLcdDisplay.AddList(FindLcds(flightIndicatorsLcdNames));
                } else
                {
                    IMyTextPanel textPanel = FindFirstLcd();
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
                        LcdDisplayMessage(message, flightIndicatorsLcdDisplay);
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
                        LcdDisplayMessage(message, flightIndicatorsLcdDisplay);
                        return false;
                    }
                }
                      

                // compute absoluteNorthVec
                Vector3D shipForwardVec = flightIndicatorsShipController.WorldMatrix.Forward;
                Vector3D gravityVec = flightIndicatorsShipController.GetNaturalGravity();
                Vector3D planetRelativeLeftVec = shipForwardVec.Cross(gravityVec);
                flightIndicatorsShipControllerAbsoluteNorthVec = planetRelativeLeftVec;
            }
            
            return true;
        }

        void FlightIndicatorsDisplay()
        {
            StringBuilder stringBuilder = new StringBuilder();
            WriteOutput(stringBuilder, "Speed     {0} m/s", Math.Round(flightIndicatorsShipControllerCurrentSpeed, 2));
            WriteOutput(stringBuilder, "Pitch       {0}°", Math.Round(flightIndicatorsPitch, 2));
            WriteOutput(stringBuilder, "Roll         {0}°", Math.Round(flightIndicatorsRoll, 2));
            WriteOutput(stringBuilder, "Yaw        {0}°", Math.Round(flightIndicatorsYaw, 2));
            WriteOutput(stringBuilder, "Elevation {0} m", Math.Round(flightIndicatorsElevation, 0));
            WriteOutput(stringBuilder, flightIndicatorsWarningMessage);            
            
            if (flightIndicatorsFlightMode == FlightMode.STABILIZATION)
            {
                WriteOutput(stringBuilder, "Auto-correcting roll and pitch");
                WriteOutput(stringBuilder, "Pitch overdrive {0}", Math.Round(t_gyroPitch, 4));
                WriteOutput(stringBuilder, "Roll overdrive  {0}", Math.Round(t_gyroRoll, 4));
                WriteOutput(stringBuilder, "Yaw overdrive   {0}", Math.Round(t_gyroYaw, 4));
                WriteOutput(stringBuilder, "Error           {0}", Math.Round(error, 4));
                WriteOutput(stringBuilder, "Command         {0}", Math.Round(command, 4));

                
            }
            LcdDisplayMessage(stringBuilder.ToString(), flightIndicatorsLcdDisplay);
        }

        void FlightIndicatorsCompute()
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
         
            // could use next line for North Vector but we use left vector of the ship at init.
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
                flightIndicatorsYaw = 360.0d - flightIndicatorsYaw; //because of how the angle is measured  
                /*
                if(flightIndicatorsYaw>180)
                {
                    flightIndicatorsYaw -= 360;
                }*/
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
            if (fmt != null && fmt.Length > 0)
            {
                output.Append(string.Format(fmt, args));
                output.Append('\n');
            }               
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

                errorIntegral += error * timeStep;

                lastError = error;

                return p * error + i * errorIntegral + d * errorDerivative;
            }

            public void Reset()
            {
                errorIntegral = 0;
                lastError = 0;
                firstRun = true;
            }
        }

        // thanks Whip for your help
        //Whip's ApplyGyroOverride Method v9 - 8/19/17
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


        //
        // LCD library code
        // IMyTextPanel FindFirstLcd()
        // List<IMyTextPanel> FindLcds(string[] lcdGoupsAndNames)
        // void InitDisplays(List<IMyTextPanel> myTextPanels)
        // void InitDisplay(IMyTextPanel myTextPanel)

        Color defaultFontColor = new Color(0, 255, 0);
        float defaultSize = 1.5f;

        void LcdDisplayMessage(string message, List<IMyTextPanel> myTextPanels, bool append = false)
        {
            foreach (IMyTextPanel myTextPanel in myTextPanels)
            {
                myTextPanel.WritePublicText(message, append);
            }
        }

        // return null if no lcd
        IMyTextPanel FindFirstLcd()
        {
            IMyTextPanel lcd = FindFirstBlockByType<IMyTextPanel>();
            if (lcd != null)
            {
                InitDisplay(lcd);
            }
            return lcd;
        }

        // return all lcd in groups + all lcd by names
        List<IMyTextPanel> FindLcds(string[] lcdGoupsAndNames)
        {
            List<IMyTextPanel> lcds = FindBlocksByNameAndGroup<IMyTextPanel>(lcdGoupsAndNames, "LCD");
            InitDisplays(lcds);
            return lcds;
        }


        void InitDisplays(List<IMyTextPanel> myTextPanels)
        {
            InitDisplays(myTextPanels, defaultFontColor);
        }

        void InitDisplay(IMyTextPanel myTextPanel)
        {
            InitDisplay(myTextPanel, defaultFontColor);
        }

        void InitDisplays(List<IMyTextPanel> myTextPanels, Color color)
        {
            foreach (IMyTextPanel myTextPanel in myTextPanels)
            {
                InitDisplay(myTextPanel, color);
            }
        }

        void InitDisplay(IMyTextPanel myTextPanel, Color color)
        {
            myTextPanel.ShowPublicTextOnScreen();
            myTextPanel.FontColor = color;
            myTextPanel.FontSize = defaultSize;
            myTextPanel.ApplyAction("OnOff_On");
        }

        //
        // END LCD LIBRARY CODE
        //


        //
        // BASIC LIBRARY
        //

        T FindFirstBlockByType<T>() where T : class
        {
            List<T> temporaryList = new List<T>();
            GridTerminalSystem.GetBlocksOfType(temporaryList);
            if (temporaryList.Count > 0)
            {
                return temporaryList[0];
            }
            return null;
        }

        List<T> FindBlocksByNameAndGroup<T>(string[] names, string typeOfBlockForMessage) where T : class
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

        DateTime dt1970 = new DateTime(1970, 1, 1);
        double GetCurrentTimeInMs()
        {
            DateTime time = System.DateTime.Now;
            TimeSpan timeSpan = time - dt1970;
            return timeSpan.TotalMilliseconds;
        }

        //
        // END OF BASIC LIBRARY
        //

       





        #region post_script
    }
    
}

#endregion post_script
