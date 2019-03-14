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

namespace Extractor
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

        public void Save()
        {
            throw new NotImplementedException();
        }

        public void Main(string argument, UpdateType updateSource)
        {
            throw new NotImplementedException();
        }


        /*
         * 
         * 
         * 
         */
        #endregion pre_script

        #region in-game

        // afficher le nombre de blocs à extraire et le progress
        // ensuite maintenir la mémoire des blocs extraits dans custom data

        // you can add any object name or group name 
        string[] lcdNamesAndGroupNames = { "LCD Panel - mining op group" };
        string[] widthPistonNames = { "Width Pistons" };
        string[] verticalPistonNames = { "Vertical Pistons" };
        string[] depthPistonNames = { "Depth Pistons" };
        string[] drillNames = { "Drill" };

        // do not touch anything behind this line
        BasicLibrary basicLibrary;
        LCDHelper lcdHelper;
        List<IMyTextPanel> lcds;

        List<IMyPistonBase> widthPistons = new List<IMyPistonBase>();
        List<IMyPistonBase> verticalPistons = new List<IMyPistonBase>();
        List<IMyPistonBase> depthPistons = new List<IMyPistonBase>();

        List<IMyShipDrill> drills = new List<IMyShipDrill>();
        
        readonly Queue<Action> actions = new Queue<Action>();
        Action currentAction = null;

        int gridSize = -1;
        int xGridSize = -1;
        int yGridSize = -1;
        int zGridSize = -1;
        int maxNumberOfAction = 0;       

        const float PISTON_MIN_LIMIT = 0.0f;
        const float PISTON_MAX_LIMIT = 10.0f; // TODO, depends of grid size
        const float PISTON_LIMIT_INCREMENT = PISTON_MAX_LIMIT / 4;

        const float PISTON_SPEED = 0.5f;
        const float PISTON_RETRACT_SPEED = -2.5f;
        const float DIFFERENCE_TOLERANCE = 0.01f;

        public Program()
        {
            basicLibrary = new BasicLibrary(GridTerminalSystem, Echo);
            lcdHelper = new LCDHelper(basicLibrary, new Color(255, 255, 255), 1.5f);
            Runtime.UpdateFrequency = UpdateFrequency.Update10;

            loadData();

            setAllPistonOnOff(true);
            setDrillsOnOff(drills, false);

            createFullRetractActions();
        }

        private void loadData()
        {
            lcds = lcdHelper.Find(lcdNamesAndGroupNames);
            widthPistons = basicLibrary.FindBlocksByNameAndGroup<IMyPistonBase>(widthPistonNames, "Pistons");
            verticalPistons = basicLibrary.FindBlocksByNameAndGroup<IMyPistonBase>(verticalPistonNames, "Pistons");
            depthPistons = basicLibrary.FindBlocksByNameAndGroup<IMyPistonBase>(depthPistonNames, "Pistons");

            drills = basicLibrary.FindBlocksByNameAndGroup<IMyShipDrill>(drillNames, "Drills");

            xGridSize = widthPistons.Count;
            yGridSize = depthPistons.Count;
            zGridSize = verticalPistons.Count;
            
            gridSize = xGridSize * yGridSize * zGridSize * 5 * 5 * 5 ;
            //gridSize = yGridSize * zGridSize * 5 * 5;
        }
      
        private void setAllPistonOnOff(bool on)
        {
            setPistonOnOff(widthPistons, on);
            setPistonOnOff(verticalPistons, on);
            setPistonOnOff(depthPistons, on);
        }

        private static void setPistonOnOff(List<IMyPistonBase> pistons, bool on)
        {
            pistons.ForEach(p => p.Enabled = on);
        }

        private static void setDrillsOnOff(List<IMyShipDrill> drills, bool on)
        {
            drills.ForEach(p => p.Enabled = on);
        }

        private void createFullRetractActions()
        {
            actions.Enqueue(new RetractAction(verticalPistons));
            actions.Enqueue(new RetractAction(depthPistons));
            actions.Enqueue(new RetractAction(widthPistons));
        }

        abstract class Action
        {
            protected bool completed;
            internal bool isCompleted()
            {
                return completed;
            }

            internal abstract void process();
        }

        class ExpandAction : Action
        {
            private IMyPistonBase piston;
            private float pistonMaxLimit;
            bool init = true;

            public ExpandAction(IMyPistonBase piston, float pistonMaxLimit)
            {
                this.piston = piston;
                this.pistonMaxLimit = pistonMaxLimit;
            }

            internal override void process()
            {
                if(init)
                {
                    piston.MaxLimit = pistonMaxLimit;
                    piston.Velocity = PISTON_SPEED;
                }
                if(areFloatEqual(piston.CurrentPosition, pistonMaxLimit))
                {
                    completed = true;
                }
            }

            public override string ToString()
            {
                return String.Format("ExpandAction completed : {0}, name : {1}, pistonMaxLimit: {2}", completed, piston.CustomName, pistonMaxLimit);
            }
        }

        class RetractAction : Action
        {
            private List<IMyPistonBase> pistons;
            bool hasBegan = false;

            public RetractAction(List<IMyPistonBase> pistons)
            {
                this.pistons = pistons;
            }

            internal override void process()
            {
                if(!hasBegan)
                {
                    foreach (IMyPistonBase piston in pistons)
                    {
                        piston.Velocity = PISTON_RETRACT_SPEED;
                        piston.MinLimit = PISTON_MIN_LIMIT;
                        piston.MaxLimit = PISTON_MAX_LIMIT;
                    }
                    hasBegan = true;
                }
                // check end of task
                float length = getPistonsLength(pistons);
                if(isZero(length))
                {
                    completed = true;
                }
            }

            public override string ToString()
            {
                return String.Format("RetractAction completed : {0}, getPistonsLength(pistons) : {1}, name : {2}", completed, getPistonsLength(pistons), pistons[0].CustomName);
            }
        }

        class StopDrillAction : Action
        {
            List<IMyShipDrill> drills;
            public StopDrillAction(List<IMyShipDrill> drills)
            {
                this.drills = drills;
            }
            internal override void process()
            {
                setDrillsOnOff(drills, false);
                completed = true;
            }
        }

        class StartDrillAction : Action
        {
            List<IMyShipDrill> drills;
            public StartDrillAction(List<IMyShipDrill> drills)
            {
                this.drills = drills;
            }
            internal override void process()
            {
                setDrillsOnOff(drills, true);
                completed = true;
            }
        }


        public void Main(string argument)
        {
            // reload data when not performing actions, in case configuration has changed
            // (UpdateFrequency is set to Update10 only when action queue is not empty)
            if (Runtime.UpdateFrequency != UpdateFrequency.Update10)
            {
                loadData();
            }
            /*
            if (currentAction == null)
            {
                this.Me.CustomData += "null" + "\n";
            } else
            {
                this.Me.CustomData += currentAction.ToString() + "\n";
            }*/
            processArgument(argument);
            processActions();
            display();
        }

        private void processActions()
        {        
            if(currentAction == null)
            {
                TryFetchNewAction();
            }

            if (currentAction != null)
            {
                Runtime.UpdateFrequency = UpdateFrequency.Update10;
                if (currentAction.isCompleted())
                {
                    currentAction = null;
                    TryFetchNewAction();
                } 
                if(currentAction != null) { 
                    currentAction.process();
                }                
            } else
            {
                Runtime.UpdateFrequency = UpdateFrequency.None;
            }

        }

        private void TryFetchNewAction()
        {
            Action action;
            if (actions.TryDequeue(out action))
            {
                currentAction = action;
            }
        }

        private void processArgument(string argument)
        {
            if (argument.ToUpper() == "START")
            {
                createDrillActions();
                maxNumberOfAction = actions.Count;                
            }
            if (argument.ToUpper() == "PAUSE")
            {
                setAllPistonOnOff(false);
                setDrillsOnOff(drills, false);
            }
            if (argument.ToUpper() == "RESUME")
            {
                setAllPistonOnOff(true);
                setDrillsOnOff(drills, true);
            }
            if (argument.ToUpper() == "STOP")
            {
                currentAction = null;
                actions.Clear();
                createFullRetractActions();
                actions.Enqueue(new StopDrillAction(drills));                
            }

        }

        private void createDrillActions()
        {
            //int xGridSize = -1;
            //actions.Enqueue(new RetractAction(widthPistons));

            actions.Enqueue(new StartDrillAction(drills));

            for (int x = 0; x < xGridSize; x++)
            {
                for (int xx = 0; xx < 5; xx++)
                {
                    actions.Enqueue(new ExpandAction(widthPistons[x], PISTON_LIMIT_INCREMENT * xx));


                    for (int y = 0; y < yGridSize; y++)
                    {
                        for (int yy = 0; yy < 5; yy++)
                        {
                            actions.Enqueue(new ExpandAction(depthPistons[y], PISTON_LIMIT_INCREMENT * yy));
                            for (int z = 0; z < zGridSize; z++)
                            {
                                for (int zz = 0; zz < 5; zz++)
                                {
                                    actions.Enqueue(new ExpandAction(verticalPistons[z], PISTON_LIMIT_INCREMENT * zz));
                                }
                            }
                            actions.Enqueue(new RetractAction(verticalPistons));
                        }
                    }
                    actions.Enqueue(new RetractAction(depthPistons));
                }
            }
            actions.Enqueue(new RetractAction(widthPistons));

            

            actions.Enqueue(new StopDrillAction(drills));
        }

        private static bool isZero(float f1)
        {
            return Math.Abs(f1) < DIFFERENCE_TOLERANCE;
        }

        private static bool areFloatEqual(float f1, float f2)
        {
            return Math.Abs(f1-f2)< DIFFERENCE_TOLERANCE;
        }

        private void display()
        {
            if (lcds.Count > 0)
            {
                StringBuilder stringBuilder = new StringBuilder();

                BasicLibrary.AppendFormattedNewLine(stringBuilder, "Drilling station enabled !");
                BasicLibrary.AppendFormattedNewLine(stringBuilder, "Grid size : {0}", gridSize);

                if (currentAction == null)
                {
                    BasicLibrary.AppendFormattedNewLine(stringBuilder, "\nDrill is in STANDBY mode,\nuse 'start' argument\nto start drilling");
                } else
                {
                    BasicLibrary.AppendFormattedNewLine(stringBuilder, "\nDrilling in progress {0}%", Math.Round((maxNumberOfAction - actions.Count) / (float)maxNumberOfAction * 100, 0f));
                }
                lcdHelper.DisplayMessage(stringBuilder.ToString(), lcds);
            }
            else
            {
                Echo("Could not find any LCD.");
            }
        }

        private static float getPistonsLength(List<IMyPistonBase> pistons)
        {
            float length = 0;
            pistons.ForEach(p => length += p.CurrentPosition);
            return length;
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

            public LCDHelper(BasicLibrary basicLibrary, Color defaultFontColor, float defaultSize=2)
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

            public void GetBlocksOfType<T>(List<T> list, Func<T, bool> collect = null) where T:class
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

        #endregion in-game

        #region post_script

    }
}
#endregion post_script
