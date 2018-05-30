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
            List<IMyTextPanel> temporaryLcdList = new List<IMyTextPanel>();
            GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(temporaryLcdList);
            if(temporaryLcdList.Count>0)
            {
                IMyTextPanel lcd = temporaryLcdList[0];
                InitDisplay(lcd);
                return lcd;
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
                if(lcdGoupsAndNames[i].Length==0)
                {
                    break;
                }
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
                    if(!found)
                    {
                        Echo("Warning : LCD or group named\n" + lcdGoupsAndNames[i]+" not found.");
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
            myTextPanel.ShowPublicTextOnScreen();
            myTextPanel.FontColor = color;
            myTextPanel.FontSize = defaultSize;
            myTextPanel.ApplyAction("OnOff_On");
        }

        //
        // END LCD LIBRARY CODE
        //





    }

}
