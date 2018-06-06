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
        //

        public class LCDHelper
        {
            public Color defaultFontColor = new Color(150, 30, 50);
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
                    myTextPanel.WritePublicText(message, append);
                }
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
                InitDisplays(myTextPanels, defaultFontColor);
            }

            public void InitDisplay(IMyTextPanel myTextPanel)
            {
                InitDisplay(myTextPanel, defaultFontColor);
            }

            public void InitDisplays(List<IMyTextPanel> myTextPanels, Color color)
            {
                foreach (IMyTextPanel myTextPanel in myTextPanels)
                {
                    InitDisplay(myTextPanel, color);
                }
            }

            public void InitDisplay(IMyTextPanel myTextPanel, Color color)
            {
                myTextPanel.ShowPublicTextOnScreen();
                myTextPanel.FontColor = color;
                myTextPanel.FontSize = defaultSize;
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
        // BASIC LIBRARY
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
                    stringBuilder.Append('\n');
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




    }

}
