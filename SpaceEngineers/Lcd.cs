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

namespace LcdLib
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

        public Program()
        {

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




        //
        // LCD library code
        // IMyTextPanel FindFirstLcd()
        // List<IMyTextPanel> FindLcds(string[] lcdGoupsAndNames)
        // void InitDisplays(List<IMyTextPanel> myTextPanels)
        // void InitDisplay(IMyTextPanel myTextPanel)

        Color defaultFontColor = new Color(150, 30, 50);
        float defaultSize = 2;

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
            IMyTextPanel lcd = FindFirstBlockByType<IMyTextPanel>();
            if(lcd != null)
            {
                InitDisplay(lcd);
            }            
            return lcd;
        }

        // return all lcd in groups + all lcd by names
        private List<IMyTextPanel> FindLcds(string[] lcdGoupsAndNames)
        {
            List<IMyTextPanel> lcds = FindBlocksByNameAndGroup<IMyTextPanel>(lcdGoupsAndNames, "LCD");
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

        private T FindFirstBlockByType<T>() where T : class
        {
            List<T> temporaryList = new List<T>();
            GridTerminalSystem.GetBlocksOfType(temporaryList);
            if (temporaryList.Count > 0)
            {
                return temporaryList[0];               
            }            
            return null;
        }

        private List<T> FindBlocksByNameAndGroup<T>(string[] names, string typeOfBlockForMessage) where T : class
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

        //
        // END OF BASIC LIBRARY
        //




    }

}
