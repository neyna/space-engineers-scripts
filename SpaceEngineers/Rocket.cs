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







        // TODO obtenir la liste des thruster par orientation 
        // TODO landing gear OFF au décollage
        //thrusters[0].GridThrustDirection VRageMath.Vector3I.Forward Backward Left Right Up Down 
        //thrusters[0].Orientation
        // TOTO changer les lcd.setValue
        // TODO lib pour les LCD panel en prenant un tableau de string mix groupes et noms LCD

        // utiliser roll et pitch pour réorienter la fusée à la verticale (utiliser les gyros?)

        // si speed < 20 % mettre full poussée

        // configuration
        string mainCockpitName = "";
        string mainLcdPanelName = "LCD rocket launch";
        string upThrusterGroup = "Rocket Takeoff - up thrusters";
        int countDown = -1; // timer to takeoff, set to -1 for no countdown

        // takeoff constants
        const double maxSpeed = 100;
        const double minTakeOffSpeedTolerance = 0.95;
        const double maxTakeOffSpeedTolerance = 0.98;
        const double gravityThreshold = 0.03;
        const float minOverride = 0.01f;
        const float decreaseOverrideRate = 0.002f;                                                    
        const float increaseOverrideRate = 0.002f;
        const double elevationThreshold1 = 1500;
        const double elevationThreshold2 = 750;


        // dot not touch
        DateTime dt1970 = new DateTime(1970, 1, 1);
        IMyTextPanel lcdDisplay = null;
        IMyShipController reference = null;
        List<IMyThrust> thrusters = null;
        double initTime = -1;
        double currentTime = -1;
        double liftOffTime = -1;
        RocketMode rocketMode = RocketMode.IDLE;
        enum RocketMode { LAUNCH, LANDING, ABORT, IDLE };
        

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Once;          
        }

        // argument is only sent once when program is called via run
        public void Main(string argument, UpdateType updateSource)
        {
            if(lcdDisplay==null)
            {
                InitializeLcd();
            }
            if( ! argument.Equals(""))
            {
                SetLcdScreenParamters();
                if( ! TryGetThrusterGroup() )
                {
                    return;
                }
            }
            
            currentTime = GetCurrentTimeInMs();
          
            if (argument.ToLower().Equals("launch"))
            {
                initTime = currentTime;
                rocketMode = RocketMode.LAUNCH;
                Runtime.UpdateFrequency = UpdateFrequency.Update10;
                reference.DampenersOverride = true;
                InitializeController();
            }
            else if (argument.ToLower().Equals("landing"))
            {
                initTime = currentTime;
                rocketMode = RocketMode.LANDING;
                Runtime.UpdateFrequency = UpdateFrequency.Update10;
                reference.DampenersOverride = false;
                InitializeController();
            }
            else if (argument.ToLower().Equals("abort"))
            {
                rocketMode = RocketMode.ABORT;
                Runtime.UpdateFrequency = UpdateFrequency.Update100;
                InitializeController();
            }

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
                    lcdDisplay.WritePublicText("\n\n\n   Rocket launching\n      system idle ...");
                    break;
            }

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
            lcdDisplay.WritePublicText("Aborting ...");
            SetThrustersOverridePercent(0f, true);
        }

        private bool TryGetThrusterGroup()
        {
            IMyBlockGroup thrustGroup = GridTerminalSystem.GetBlockGroupWithName(upThrusterGroup);
            if (thrustGroup == null)
            {
                Echo("Unable to find thruster group, aborting...");
                lcdDisplay.WritePublicText("Unable to find thruster\ngroup, aborting...");
                return false;
            }

            thrusters = new List<IMyThrust>();
            thrustGroup.GetBlocksOfType(thrusters);
            if (thrusters.Count == 0)
            {
                Echo("No thrusters in specified group, aborting...");
                lcdDisplay.WritePublicText("No thrusters in\nspecified group\nAborting...");
                return false;
            }

            for (int i = 0; i < thrusters.Count; i++)
                thrusters[i].ApplyAction("OnOff_On");
            //SetThrustersOverridePercent(0, true);

            return true;
        }

        private void InitializeLcd()
        {
            if (lcdDisplay == null)
            {
                if (!mainLcdPanelName.Equals(""))
                {
                    try
                    {
                        lcdDisplay = GridTerminalSystem.GetBlockWithName(mainLcdPanelName) as IMyTextPanel;
                    }
                    catch (Exception e)
                    {
                        throw new Exception("Can't find LCD named " + mainLcdPanelName);
                    }

                }
                else
                {
                    List<IMyTextPanel> lcdPanels = new List<IMyTextPanel>();
                    GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(lcdPanels);
                    if (lcdPanels.Count == 0)
                    {
                        throw new Exception("No LCD Screen");
                    }
                    lcdDisplay = (IMyTextPanel)lcdPanels[0];
                }
            }
        }

        private void InitializeController()
        {
            if (reference == null)
            {
                if (!mainCockpitName.Equals(""))
                {
                    try
                    {
                        reference = GridTerminalSystem.GetBlockWithName(mainCockpitName) as IMyShipController;
                        if (!CanControllerControlShip(reference))
                        {
                            reference = null;
                        }
                    }
                    catch (Exception e)
                    {
                        throw new Exception("Can't find ship controller named " + mainCockpitName);
                    }

                }
                else
                {
                    List<IMyShipController> shipControllers = new List<IMyShipController>();
                    GridTerminalSystem.GetBlocksOfType<IMyShipController>(shipControllers);

                    reference = GetControlledShipController(shipControllers);
                }

                if (reference == null)
                {
                    throw new Exception("No user detected in any control seats");
                }
            }
        }

        private void SetLcdScreenParamters()
        {
            lcdDisplay.ShowPublicTextOnScreen();
            lcdDisplay.SetValue("FontColor", new Color(150, 30, 50));
            lcdDisplay.SetValue<Single>("FontSize", (Single)2);
            lcdDisplay.ApplyAction("OnOff_On");
        }

        private void ProcessRocketLauch()
        {
            int secondsPassedSinceInit = SecondsPassedSinceInit();
            if(countDown>0)
            {
                int messageDisplayTime = 2;
                if (secondsPassedSinceInit < messageDisplayTime)
                {
                    lcdDisplay.SetValue<Single>("FontSize", (Single)3);
                    lcdDisplay.WritePublicText("\n     LAUNCH\n   SEQUENCE\n  INITIATED !!!\n");
                }
                else if (secondsPassedSinceInit < countDown + messageDisplayTime)
                {
                    lcdDisplay.SetValue<Single>("FontSize", (Single)10);
                    int currentTimer = countDown + messageDisplayTime - secondsPassedSinceInit;
                    if(currentTimer>9)
                    {
                        lcdDisplay.WritePublicText("  " + currentTimer);
                    } else
                    {
                        lcdDisplay.WritePublicText("   " + currentTimer);
                    }
                    
                }
                else if (secondsPassedSinceInit < countDown + messageDisplayTime +1)
                {
                    lcdDisplay.SetValue<Single>("FontSize", (Single)5);
                    lcdDisplay.WritePublicText("\nIGNITION");
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
            lcdDisplay.SetValue<Single>("FontSize", (Single)1);
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
                WriteOutput(output, "Lift off complete in : {0}s", Math.Round(liftOffTime, 2));
                WriteOutput(output, "Cutting Dampener");
                lcdDisplay.WritePublicText(output.ToString());
                // cut Dampeners and engines
                reference.DampenersOverride = false;
                SetThrustersOverridePercent(0, true);
                return;
            }     

            double elevation = -1;
            reference.TryGetPlanetElevation(MyPlanetElevation.Surface, out elevation);
            float thrusterOverridePercent = GetThrusterOverridePercent(thrusters);

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

        double GetCurrentTimeInMs()
        {
            DateTime time = System.DateTime.Now;
            TimeSpan timeSpan = time - dt1970;
            return timeSpan.TotalMilliseconds;
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

        private Boolean CanControllerControlShip(IMyShipController controller)
        {
            return controller.IsUnderControl && controller.CanControlShip;
        }

        void WriteOutput(StringBuilder output, string fmt, params object[] args)
        {
            output.Append(string.Format(fmt, args));
            output.Append('\n');
        }

        float GetThrusterOverridePercent(List<IMyThrust> thrusters)
        {
            return thrusters[0].ThrustOverridePercentage;
        }







    }

}
