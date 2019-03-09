#region pre_script
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

namespace Rocket
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





        // TODO obtenir la liste des thruster par orientation 
        // TODO landing gear OFF au décollage
        //thrusters[0].GridThrustDirection VRageMath.Vector3I.Forward Backward Left Right Up Down 
        //thrusters[0].Orientation

        // pid version if atmo
        // physic version if full hydro, calculer la poussée pour atteindre 98% max speed en 30s puis poussée pour contrer la gravité, puis passer en PID.

        /*
         * calculate height under controller
            IMySoundBlock block = null;
        var a = block.Position;
        var b = block.Max;
        var c = block.Min;
         */

        // IMyShipController.CalculateShipMass().PhysicalMass        

        // utiliser roll et pitch pour réorienter la fusée à la verticale (utiliser les gyros?)
        // si speed < 20 % mettre full poussée

        // configuration
        string mainCockpitName = "";
        readonly string[] lcdNamesAndGroupNames = { "LCD rocket launch" };
        readonly string upThrusterGroup = "Rocket Takeoff - up thrusters";
        const float cockpitHeight = 8.0f; // height of your cockpit from the ground, can see it when rocket is landed, enter cockpit and look at altitude
        const int countDown = 9; // timer to takeoff, set to -1 for no countdown
        // sound configuration
        const string soundProgrammingBlock = "";
        const string countDownSound = "";
        const string IgnitionSound = "";

        // takeoff constants
        const double maxSpeed = 100;
        const double minTakeOffSpeedTolerance = 0.97;
        const double maxTakeOffSpeedTolerance = 0.99;
        const double gravityThreshold = 0.03;
        const float minOverride = 0.01f; // prevent shutdown of engines because they need time to startup again

        // dot not touch    
        readonly LCDHelper lcdHelper;
        readonly BasicLibrary basicLibrary;
        readonly Color defaultColor = new Color(255, 255, 255);

        readonly List<IMyTextPanel> lcds = null;
        readonly List<IMyThrust> thrusters = null;
        IMyShipController reference = null;
        
        double initTime = -1;
        double currentTime = -1;
        double liftOffTime = -1;
        RocketMode rocketMode = RocketMode.IDLE;
        enum RocketMode { LAUNCH, LANDING, ABORT, IDLE };
        

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Once;
            basicLibrary = new BasicLibrary(GridTerminalSystem, Echo);
            lcdHelper = new LCDHelper(basicLibrary, defaultColor);
            lcds = new List<IMyTextPanel>();
            thrusters = new List<IMyThrust>();
        }

        // argument is only sent once when program is called via run
        public void Main(string argument, UpdateType updateSource)
        {
            if(!TryInit(argument!=null && argument.Length>0))
            {
                Echo("Init failed. Check previous logs.");
                return;
            }           
            
            currentTime = BasicLibrary.GetCurrentTimeInMs();
          
            if (argument.ToLower().Equals("launch"))
            {
                if (!TryInitializeController()) return;
                initTime = currentTime;
                rocketMode = RocketMode.LAUNCH;
                Runtime.UpdateFrequency = UpdateFrequency.Update10;
                reference.DampenersOverride = true;
                
            }
            else if (argument.ToLower().Equals("landing"))
            {
                if (!TryInitializeController()) return;
                initTime = currentTime;
                rocketMode = RocketMode.LANDING;
                Runtime.UpdateFrequency = UpdateFrequency.Update10;
                reference.DampenersOverride = false;
            }
            else if (argument.ToLower().Equals("abort"))
            {
                if (!TryInitializeController()) return;
                rocketMode = RocketMode.ABORT;
                Runtime.UpdateFrequency = UpdateFrequency.Once;
            }

            lcdHelper.ClearMessageBuffer();            

            //LAUNCH, LANDING, ABORT, NONE
            switch (rocketMode)
            {
                case RocketMode.LAUNCH:                    
                    ProcessRocketLauch();
                    break;
                case RocketMode.LANDING:
                    Land();
                    break;
                case RocketMode.ABORT:
                    Abort();
                    rocketMode = RocketMode.IDLE;                    
                    break;
                case RocketMode.IDLE:
                    lcdHelper.AppendMessageBuffer("\n\n\n   Rocket launching\n      system idle ...");                    
                    break;
            }

            lcdHelper.DisplayMessageBuffer(lcds);

        }

        bool TryInit(bool shouldFetchThrusters)
        {
            if(lcds.Count==0)
            {
                lcds.AddList(lcdHelper.Find(lcdNamesAndGroupNames));
                if (lcds.Count == 0)
                {
                    return false;
                }
            }
            if (shouldFetchThrusters && !TryGetThrusterGroup())
            {
                return false;
            }

            return true;
        }

        private bool TryGetThrusterGroup()
        {
            IMyBlockGroup thrustGroup = GridTerminalSystem.GetBlockGroupWithName(upThrusterGroup);
            if (thrustGroup == null)
            {
                Echo("Unable to find thruster group, aborting...");
                lcdHelper.DisplayMessage("Unable to find thruster\ngroup, aborting...", lcds);                
                return false;
            }

            thrusters.Clear();            
            thrustGroup.GetBlocksOfType(thrusters);
            if (thrusters.Count == 0)
            {
                Echo("No thrusters in specified group, aborting...");                
                lcdHelper.DisplayMessage("No thrusters in\nspecified group\nAborting...", lcds);
                return false;
            }

            for (int i = 0; i < thrusters.Count; i++)
                thrusters[i].ApplyAction("OnOff_On");
            //SetThrustersOverridePercent(0, true);

            return true;
        }

        private bool TryInitializeController()
        {
            if (reference == null)
            {
                if (mainCockpitName.Length>0)
                {
                    var block = GridTerminalSystem.GetBlockWithName(mainCockpitName);
                    if(block==null)
                    {
                        string message = $"Can't find ship controller named {mainCockpitName}";
                        Echo(message);
                        lcdHelper.DisplayMessage(message, lcds);
                        return false;
                    }
                    if (block.GetType() != typeof(IMyShipController))
                    {
                        string message = $"BLock named {mainCockpitName} is not a controller.";
                        Echo(message);
                        lcdHelper.DisplayMessage(message, lcds);
                        return false;
                    }

                    if (!CanControllerControlShip(block as IMyShipController))
                    {
                        string message = $"Controller named {mainCockpitName} can't control the ship or no one is sitting in it.";
                        Echo(message);
                        lcdHelper.DisplayMessage(message, lcds);
                        return false;
                    }

                    reference = block as IMyShipController;
                }
                else
                {
                    List<IMyShipController> shipControllers = new List<IMyShipController>();
                    GridTerminalSystem.GetBlocksOfType<IMyShipController>(shipControllers);

                    reference = GetControlledShipController(shipControllers);
                    if(reference==null)
                    {
                        string message = $"Could not find any controller with a user sitting in it.";
                        Echo(message);
                        lcdHelper.DisplayMessage(message, lcds);
                        return false;
                    }
                }
                
            }
            return true;
        }

        private void Land()
        {
            /*
            lcdDisplay.SetValue<Single>("FontSize", (Single)1);
            double currentGravity = reference.GetNaturalGravity().Length();
            StringBuilder output = new StringBuilder();
            double speed = reference.GetShipSpeed();
            double timeSinceLandingStarted = (currentTime - initTime) / 1000;
            double elevation = -1;
            reference.TryGetPlanetElevation(MyPlanetElevation.Surface, out elevation);
            float thrusterOverridePercent = GetThrusterOverridePercent(thrusters);

            double minLandingSpeedTolerance = 1;
            double maxLandingSpeedTolerance = 1;

            


            if (elevation < elevationThreshold1)
            {              
               
                WriteOutput(output, "Elevation Threshold 1 reached");
                WriteOutput(output, "Slowing down to half max speed");
                lcdDisplay.WritePublicText(output.ToString());
                minLandingSpeedTolerance = 0.45;
                maxLandingSpeedTolerance = 0.5;
            }
            else if (elevation < elevationThreshold2)
            {

                WriteOutput(output, "Elevation Threshold 2 reached");
                WriteOutput(output, "Slowing down to quarter max speed");
                lcdDisplay.WritePublicText(output.ToString());
                minLandingSpeedTolerance = 0.2;
                maxLandingSpeedTolerance = 0.25;
            } else if(elevation >= elevationThreshold1)
            {
                // do nothing, let the rocket fall
            } else
            {

            }


            WriteOutput(output, "Thrusters Override: {0}%", Math.Round(thrusterOverridePercent * 100, 2));
            WriteOutput(output, "Current Gravity: {0}g", Math.Round(currentGravity, 2));
            WriteOutput(output, "Time since take off: {0}s", Math.Round(timeSinceTakeOff, 0));
            WriteOutput(output, "Elevation: {0}m", Math.Round(elevation, 0));
            WriteOutput(output, "Speed: {0}m/s", Math.Round(speed, 2));


            if (speed < maxSpeed * minTakeOffSpeedTolerance)
            {
                WriteOutput(output, "Increasing thrust");
                SetThrustersOverridePercent(thrusterOverridePercent + increaseOverrideRate);
            }
            else if (speed > maxSpeed * maxTakeOffSpeedTolerance)
            {
                WriteOutput(output, "Decreasing thrust");
                SetThrustersOverridePercent(thrusterOverridePercent - decreaseOverrideRate);
            }
            else
            {
                WriteOutput(output, "Speed is in acceptable range");
            }

            lcdDisplay.WritePublicText(output.ToString());
            */


            /*
            if (liftOffTime < 0)
            {
                liftOffTime = timeSinceLanding;
            }
            */
            // cut Dampeners and engines
            //reference.DampenersOverride = false;
            //SetThrustersOverridePercent(0, true);


        }

        private void Abort()
        {
            lcdHelper.AppendMessageBuffer("Aborting ...");
            SetThrustersOverridePercent(0f, true);
        }        
        
        private void ProcessRocketLauch()
        {
            int secondsPassedSinceInit = SecondsPassedSinceInit();
            if(countDown>0)
            {
                int messageDisplayTime = 2;
                if (secondsPassedSinceInit < messageDisplayTime)
                {
                    LCDHelper.ChangeColorAndFontSize(lcds, defaultColor, 3);
                    lcdHelper.AppendMessageBuffer("\n     LAUNCH\n   SEQUENCE\n  INITIATED !!!\n");
                }
                else if (secondsPassedSinceInit < countDown + messageDisplayTime)
                {
                    LCDHelper.ChangeColorAndFontSize(lcds, defaultColor, 10);                    
                    int currentTimer = countDown + messageDisplayTime - secondsPassedSinceInit;
                    if(currentTimer>9)
                    {
                        lcdHelper.AppendMessageBuffer("  " + currentTimer);
                    } else
                    {
                        lcdHelper.AppendMessageBuffer("   " + currentTimer);
                    }
                    
                }
                else if (secondsPassedSinceInit < countDown + messageDisplayTime +1)
                {
                    LCDHelper.ChangeColorAndFontSize(lcds, defaultColor, 5);
                    lcdHelper.AppendMessageBuffer("\nIGNITION");
                }
                else
                {
                    LiftOff();
                }
            } else
            {
                LiftOff();
            }
            
        }


        private void LiftOff()
        {
            LCDHelper.ChangeColorAndFontSize(lcds, defaultColor, 1);            
            double currentGravity = reference.GetNaturalGravity().Length();
            StringBuilder output = new StringBuilder();
            double speed = reference.GetShipSpeed();
            double timeSinceTakeOff = (currentTime - initTime) / 1000;

            if (currentGravity < gravityThreshold)
            {
                if(liftOffTime<0)
                {                    
                    liftOffTime = timeSinceTakeOff;
                }
                
                lcdHelper.AppendMessageBufferFormatted("Lift off complete in : {0}s\n", Math.Round(liftOffTime, 2));
                lcdHelper.AppendMessageBuffer("Cutting Dampener");                
                // cut Dampeners and engines
                reference.DampenersOverride = false;
                SetThrustersOverridePercent(0, true);
                return;
            }     

            double elevation = -1;
            reference.TryGetPlanetElevation(MyPlanetElevation.Surface, out elevation);
            float thrusterOverridePercent = GetThrusterOverridePercent(thrusters);


            lcdHelper.AppendMessageBufferFormatted("Thrusters Override:  {0}%\n", Math.Round(thrusterOverridePercent * 100, 2));
            lcdHelper.AppendMessageBufferFormatted("Current Gravity:        {0}g\n", Math.Round(currentGravity, 2));
            lcdHelper.AppendMessageBufferFormatted("Time since take off:  {0}s\n", Math.Round(timeSinceTakeOff, 0));
            lcdHelper.AppendMessageBufferFormatted("Elevation:                {0}m\n", Math.Round(elevation, 0));
            lcdHelper.AppendMessageBufferFormatted("Speed:                    {0}m/s\n", Math.Round(speed, 2));


            if (speed < maxSpeed * minTakeOffSpeedTolerance)
            {
               
                //SetThrustersOverridePercent(0);
            }               
            else if (speed > maxSpeed * maxTakeOffSpeedTolerance)
            {
                
            }                
            else
            {
                //WriteOutput(output, "Speed is in acceptable range");
            }
        }

        void SetThrustersOverridePercent(float percent, Boolean force = false)
        {
            if (percent < minOverride && !force)
                percent = minOverride;
            else if(percent > 1)
                percent = 1;
            for (int i = 0; i < thrusters.Count; i++)
                thrusters[i].ThrustOverridePercentage = percent;
        }

        int SecondsPassedSinceInit()
        {
            return Convert.ToInt32((currentTime - initTime) / 1000);
        }

        IMyShipController GetControlledShipController(List<IMyShipController> SCs)
        {
            foreach (IMyShipController thisController in SCs)
            {
                if (CanControllerControlShip(thisController))
                    return thisController;
            }

            return null;
        }

        private bool CanControllerControlShip(IMyShipController controller)
        {
            return controller.IsUnderControl && controller.CanControlShip;
        }

        float GetThrusterOverridePercent(List<IMyThrust> thrusters)
        {
            return thrusters[0].ThrustOverridePercentage;
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
                myTextPanel.ApplyAction("OnOff_On");
                ChangeColorAndFontSize(myTextPanel, color, fontSize);
            }

            static public void ChangeColorAndFontSize(List<IMyTextPanel> myTextPanels, Color color, float fontSize = 2)
            {
                foreach (IMyTextPanel myTextPanel in myTextPanels)
                {
                    ChangeColorAndFontSize(myTextPanel, color, fontSize);
                }
            }

            static public void ChangeColorAndFontSize(IMyTextPanel myTextPanel, Color color, float fontSize = 2)
            {
                myTextPanel.FontColor = color;
                myTextPanel.FontSize = fontSize;
            }

            public void ClearMessageBuffer()
            {
                messageBuffer.Clear();
            }

            public void AppendMessageBuffer(string text)
            {
                messageBuffer.Append(text);
            }

            public void AppendMessageBufferFormatted(string text, params object[] args)
            {
                BasicLibrary.AppendFormatted(messageBuffer, text, args);
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

            static DateTime dt1970 = new DateTime(1970, 1, 1);
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
