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

namespace PressurizedRoom
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

        // add automatic door closure for interior doors after a timer
        // faire test : avec un vent off, le passer à ON, prendre la pression, le passer en OFF dans le script et voir si ca marche

        // You can use groups or single blocks and add the following into their names :
        // [PressurizedRoom, Room:room1] => for any door, vent or light
        // name or option are not mandatory, name is used for LCD display, default names will be generated if noone are supplied
        // [PressurizedRoom, Room:room1, Name:door1, Option:externalDoor] => opening this door by the program will cause a depressurization. This is the default value, you don't need to specify it.
        // [PressurizedRoom, Room:room1, Name:door1, Option:interiorDoor] => interiorDoor won't depressurize the room when opening the door via program and can be opened manually 
        // [PressurizedRoom, Room:room1, Name:air vent1, Option:recycleAir] => recycleAir takes/puts air from/to an oxygen tank, this is the default value
        // [PressurizedRoom, Room:room1, Name:air vent1, Option:freshAir] => freshAir makes the script keeps pressure in the room > given minimum value (needs to be connected to 02 generator) - WON'T DEPRESSURIZE THE ROOM, they are shutdowned when external doors are opening/opened
        // configuration        
        const string SCRIPT_KEYWORD = "PressurizedRoom";
        const string DEFAULT_AIR_VENT_NAME = "Air vent ";
        const string DEFAULT_DOOR_NAME = "Door ";
        const string DEFAULT_LIGHT_NAME = "Light ";
        

        // end of configuration
        const string SCRIPT_OPTION_KEYWORD = "Option";
        const string SCRIPT_NAME_KEYWORD = "Name";
        const string SCRIPT_OPTION_INTERNALDOOR_KEYWORD = "interiorDoor";
        const string SCRIPT_OPTION_EXTERNALDOOR_KEYWORD = "externalDoor";
        const string SCRIPT_OPTION_RECYCLEAIR_KEYWORD = "recycleAir";
        const string SCRIPT_OPTION_FRESHAIR_KEYWORD = "freshAir";

        readonly BasicLibrary basicLibrary;
        LCDHelper lcdHelper;
        RoomManager roomManager;
        readonly Func<IMyTerminalBlock, bool> nameFilter = block => block.CustomName.Contains(SCRIPT_KEYWORD);
        readonly Func<IMyBlockGroup, bool> blockNameFilter = block => block.Name.Contains(SCRIPT_KEYWORD);
        bool configurationOk = true;

        List<IMyTextPanel> lcds; //TEMP

        public Program()
        {
            basicLibrary = new BasicLibrary(GridTerminalSystem, Echo);
            lcdHelper = new LCDHelper(basicLibrary, new Color(255,255,255), 1.5f);
            roomManager = new RoomManager();
            CreateConfiguration();

            if(!configurationOk)
            {
                Echo("Press Recompile after fixing previous errors.");
                Runtime.UpdateFrequency = UpdateFrequency.None;
                return;
            }

            Runtime.UpdateFrequency = UpdateFrequency.Update100;
            

            // close all doors
            // pressurize
            // set internal doors to ON
            // set external doors to OFF
        }

        // argument is roomName,DoorOrDoorGroupName
        public void Main(string argument)
        {
            if (!configurationOk)
            {
                Echo("Program called but configuration is invalid. Press recompile and look for errors.");
                return;
            }
            /*
            if (roomManager.commonLcds.Count > 0)
            {
                lcdHelper.ClearMessageBuffer();

                lcdHelper.AppendMessageBuffer("Test PressurizedRoom.\n");
                lcdHelper.AppendMessageBufferFormatted("Number of doors : {0}", roomManager.DoorCount());

                lcdHelper.DisplayMessageBuffer(roomManager.commonLcds);
            }
            */


        }

        void CreateConfiguration()
        {
            // get single doors with the SCRIPT_KEYWORD
            List<IMyDoor> doors = new List<IMyDoor>();
            GridTerminalSystem.GetBlocksOfType(doors, nameFilter);
            configurationOk = RegisterDoors(doors);
            if (!configurationOk) return;
            // get vents
            List<IMyAirVent> vents = new List<IMyAirVent>();
            GridTerminalSystem.GetBlocksOfType(vents, nameFilter);
            configurationOk = RegisterVents(vents);
            if (!configurationOk) return;
            // get lights
            List<IMyInteriorLight> lights = new List<IMyInteriorLight>();
            GridTerminalSystem.GetBlocksOfType(lights, nameFilter);
            configurationOk = RegisterLights(lights);
            if (!configurationOk) return;
            // lcds
            // TODO
            // TODO
            // TODO


            // get groups with the SCRIPT_KEYWORD
            List<IMyBlockGroup> groups = new List<IMyBlockGroup>();
            GridTerminalSystem.GetBlockGroups(groups, blockNameFilter);
            foreach (IMyBlockGroup group in groups)
            {
                // doors
                doors.Clear();
                group.GetBlocksOfType(doors);
                configurationOk = RegisterDoors(doors, group.Name);
                if (!configurationOk) return;
                // vents
                vents.Clear();
                group.GetBlocksOfType(vents);
                configurationOk = RegisterVents(vents, group.Name);
                if (!configurationOk) return;
                // lights
                lights.Clear();
                group.GetBlocksOfType(lights);
                configurationOk = RegisterLights(lights, group.Name);
                if (!configurationOk) return;

                // lcds
                // TODO
                // TODO
                // TODO
            }

            // common LCD
            lcds = new List<IMyTextPanel>();
            GridTerminalSystem.GetBlocksOfType(lcds, nameFilter);
            lcdHelper.InitDisplays(lcds);
        }

        private bool RegisterDoors(List<IMyDoor> doors, string groupName =null)
        {
            foreach (IMyDoor door in doors)
            {
                ParseInfo parseInfo = new ParseInfo(groupName != null ? groupName : door.CustomName, ParseInfo.ParseInfoType.DOOR, Echo);
                bool parseOk = parseInfo.Parse();
                if (!parseOk) return false;

                roomManager.RegisterDoor(door, parseInfo.roomName, parseInfo.objectName, parseInfo.isInteriorDoor);
            }
            return true;
        }

        private bool RegisterVents(List<IMyAirVent> vents, string groupName = null)
        {
            foreach (IMyAirVent vent in vents)
            {
                ParseInfo parseInfo = new ParseInfo(groupName != null ? groupName : vent.CustomName, ParseInfo.ParseInfoType.AIRVENT, Echo);
                bool parseOk = parseInfo.Parse();
                if (!parseOk) return false;

                roomManager.RegisterVent(vent, parseInfo.roomName, parseInfo.objectName, parseInfo.recycleAir);
            }
            return true;
        }

        private bool RegisterLights(List<IMyInteriorLight> lights, string groupName = null)
        {
            foreach (IMyInteriorLight light in lights)
            {
                ParseInfo parseInfo = new ParseInfo(groupName != null ? groupName : light.CustomName, ParseInfo.ParseInfoType.LIGHT, Echo);
                bool parseOk = parseInfo.Parse();
                if (!parseOk) return false;

                roomManager.RegisterLight(light, parseInfo.roomName, parseInfo.objectName);
            }
            return true;
        }


        public class RoomManager
        {
            //public List<IMyTextPanel> commonLcds { get; private set; } = new List<IMyTextPanel>();
            readonly Dictionary<string, Room> rooms = new Dictionary<string, Room>();
/*
            public int DoorCount()
            {
                int result = 0;
                foreach (Room room in rooms.Values)
                {

                }
                return result;
            }
            */

            internal void RegisterDoor(IMyDoor door, string roomName, string doorName, bool isInteriorDoor)
            {  
                Room room = GetOrCreateRoom(roomName);
                room.RegisterDoor(door, doorName, isInteriorDoor);
            }

            internal void RegisterVent(IMyAirVent vent, string roomName, string ventName, bool recycleAir)
            {
                Room room = GetOrCreateRoom(roomName);
                room.RegisterVent(vent, ventName, recycleAir);
            }

            internal void RegisterLight(IMyInteriorLight light, string roomName, string lightName)
            {
                Room room = GetOrCreateRoom(roomName);
                room.RegisterLight(light, lightName);
            }

            private Room GetOrCreateRoom(string roomName)
            {
                Room room = null;
                bool found = rooms.TryGetValue(roomName, out room);
                if (!found)
                {
                    room = new Room(roomName);
                    rooms.Add(roomName, room);
                }
                return room;
            }
        }

        public class Room
        {
            bool actionInProgress = false;
            private string name;

            readonly Dictionary<string, DoorGroup> doors = new Dictionary<string, DoorGroup>();
            readonly Dictionary<string, AirVentGroup> vents = new Dictionary<string, AirVentGroup>();
            readonly Dictionary<string, List<IMyInteriorLight>> lights = new Dictionary<string, List<IMyInteriorLight>>();            

            public Room(string name)
            {
                this.name = name;
            }

            internal void RegisterDoor(IMyDoor door, string doorName, bool isInteriorDoor)
            {
                // dictionary by doorName to create door groups
                
            }

            internal void RegisterLight(IMyInteriorLight light, string lightName)
            {
                
            }

            internal void RegisterVent(IMyAirVent vent, string ventName, bool recycleAir)
            {
                
            }
        }

        public class DoorGroup
        {
            string name;
            bool actionInProgress;
        }
        public class AirVentGroup
        {
            string name;
            bool actionInProgress;
        }

        public class ParseInfo
        {
            public enum ParseInfoType { DOOR, AIRVENT, LIGHT};
            static int doorCount = 1;
            static int airVentCount = 1;
            static int lightCount = 1;
            static int groupCount = 1;


            internal string objectName = null;
            internal string roomName = null;
            internal bool isInteriorDoor = false;
            internal bool recycleAir = true;
            internal bool error = false;
            internal ParseInfoType parseInfoType;

            private readonly string customName;
            private readonly Action<string> Echo;

            public ParseInfo(string customName, ParseInfoType parseInfoType, Action<string> Echo)
            {
                this.customName = customName;
                this.parseInfoType = parseInfoType;
                this.Echo = Echo;
            }

            public bool Parse()
            {
                int startIndex = customName.IndexOf(SCRIPT_KEYWORD);
                if (startIndex == 0 || customName[startIndex - 1] != '[')
                {
                    error = true;
                    Echo($"No '[' just before {SCRIPT_KEYWORD} in '{customName}'\n");
                    return false;
                }
                int endIndex = customName.IndexOf(']', startIndex);
                if (endIndex == -1)
                {
                    error = true;
                    Echo($"Can't find any ']' after {SCRIPT_KEYWORD} in '{customName}'\n");
                    return false;
                }
                string[] data = customName.Substring(startIndex, endIndex - startIndex).Split(',');
                // CHECK KEYWORD
                if (data.Length < 2) // TODO IF LCD ==1 is allowed
                {
                    error = true;
                    Echo($"Bad data structure in '{customName}', must be like [{SCRIPT_KEYWORD},Room:MyRoomName,...]\n");
                    return false;
                }
                // ROOM
                string[] data1 = data[1].Trim().Split(':');
                if (data1.Length != 2)
                {
                    error = true;
                    Echo($"Bad Room data structure in '{customName}', must be like [{SCRIPT_KEYWORD},Room:MyRoomName,...]\n");
                    return false;
                }
                if (data1[0].Trim() != "Room")
                {
                    error = true;
                    Echo($"No Room keyword found in second position in '{customName}', must be like [{SCRIPT_KEYWORD},Room:MyRoomName,...]\n");
                    return false;
                }
                string tempRoomName = data1[1].Trim();
                if(tempRoomName.Length==0)
                {
                    error = true;
                    Echo($"Empty room name in '{customName}'\n");
                    return false;
                }
                roomName = tempRoomName;
                // NAME and OPTION
                for (int i = 2; i < data.Length; i++)
                {
                    string[] dataI = data[i].Trim().Split(':');
                    if (dataI.Length != 2)
                    {
                        error = true;
                        Echo($"Bad Room data structure in '{customName}', parameters inside must be like Name:MyName or Option:MyOption \n");
                        return false;
                    }
                    string dataI1 = dataI[0].Trim();
                    string dataI2 = dataI[1].Trim();                    
                    if (dataI1 != SCRIPT_OPTION_KEYWORD && dataI1 != SCRIPT_NAME_KEYWORD)
                    {
                        error = true;
                        Echo($"Bad Room data structure in '{customName}', parameters inside must be like Name:MyName or Option:MyOption \n");
                        return false;
                    }
                    if(dataI1 == SCRIPT_NAME_KEYWORD)
                    {
                        if(dataI2.Length==0)
                        {
                            error = true;
                            Echo($"Bad Room data structure in '{customName}', name is empty.\n");
                            return false;
                        }
                        objectName = dataI2;
                    }
                    if (dataI1 == SCRIPT_OPTION_KEYWORD)
                    {
                        if (dataI2 != SCRIPT_OPTION_EXTERNALDOOR_KEYWORD 
                            && dataI2 != SCRIPT_OPTION_INTERNALDOOR_KEYWORD
                            && dataI2 != SCRIPT_OPTION_FRESHAIR_KEYWORD
                            && dataI2 != SCRIPT_OPTION_RECYCLEAIR_KEYWORD)
                        {
                            error = true;
                            Echo($"Bad Room data structure in '{customName}', Option value must be either {SCRIPT_OPTION_EXTERNALDOOR_KEYWORD}, {SCRIPT_OPTION_INTERNALDOOR_KEYWORD}, {SCRIPT_OPTION_FRESHAIR_KEYWORD} or {SCRIPT_OPTION_RECYCLEAIR_KEYWORD} \n");
                            return false;
                        }
                        if(dataI2 == SCRIPT_OPTION_EXTERNALDOOR_KEYWORD)
                        {
                            isInteriorDoor = false;
                        }
                        if (dataI2 == SCRIPT_OPTION_INTERNALDOOR_KEYWORD)
                        {
                            isInteriorDoor = true;
                        }
                        if (dataI2 == SCRIPT_OPTION_FRESHAIR_KEYWORD)
                        {
                            recycleAir = false;
                        }
                        if (dataI2 == SCRIPT_OPTION_RECYCLEAIR_KEYWORD)
                        {
                            recycleAir = true;
                        }
                    }

                }
                if(objectName==null)
                {
                    switch(parseInfoType)
                    {
                        case ParseInfoType.DOOR:
                            objectName = DEFAULT_DOOR_NAME + (doorCount++);
                            break;
                        case ParseInfoType.AIRVENT:
                            objectName = DEFAULT_AIR_VENT_NAME + (airVentCount++);
                            break;
                        case ParseInfoType.LIGHT:
                            objectName = DEFAULT_LIGHT_NAME + (lightCount++);
                            break;
                    }
                }
                // TODO
                // TODO
                // TODO
                // TODO
                // TODO TEMP
                Echo(this.ToString()+"\n");
                return true;
            }

            public override string ToString()
            {
                return $"ParseInfo [RoomName:{roomName},Name:{objectName},Type:{parseInfoType},isInteriorDoor:{isInteriorDoor},recycleAir:{recycleAir}]";
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


        #region post_script

    }

}
#endregion post_script
