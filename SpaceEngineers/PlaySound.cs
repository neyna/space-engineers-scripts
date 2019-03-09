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

namespace PlaySound
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

        /*public void Main(string argument)
{
throw new NotImplementedException();
}*/

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


        #endregion pre_script


        // CONFIG
        string soundBLockName = "";        

        // DO NOT MODIFY ANYTHING BEYOND THIS LINE
        // must run the program twice to allow the sound block to update the sound we set, strange tempo bug
        bool mustPlaySound = false;
        IMySoundBlock soundBlock = null;

        // argument is the sound to play
        public void Main(string argument)
        {
            FindSoundBlock();
            
            if (soundBlock == null)
            {
                Echo("Sound Block not Found");
                return;
            }                       

            if (argument != null && argument.ToLower().Equals("sound_list"))
            {
                DisplaySoundList();
                return;
            }           
            
            if (mustPlaySound)
            {
                soundBlock.Play();
                mustPlaySound = false;             
                Runtime.UpdateFrequency = UpdateFrequency.None;
                return;
            }
            
            if (argument != null && argument.Length > 0)
            {
                soundBlock.Stop();
                soundBlock.LoopPeriod = 0;
                soundBlock.SelectedSound = argument;                
                mustPlaySound = true;                
                Runtime.UpdateFrequency = UpdateFrequency.Update10;                
            }
        }

        void FindSoundBlock()
        {
            if (soundBLockName != null && soundBLockName.Length > 0)
            {
                var tempSoundBlock = GridTerminalSystem.GetBlockWithName(soundBLockName);
                if (tempSoundBlock != null && tempSoundBlock is IMySoundBlock)
                {
                    soundBlock = tempSoundBlock as IMySoundBlock;
                }
            }
            else
            {
                List<IMySoundBlock> sounds = new List<IMySoundBlock>();
                GridTerminalSystem.GetBlocksOfType<IMySoundBlock>(sounds);
                if (sounds.Count != 0)
                {
                    soundBlock = sounds[0];
                    Echo($"Taking first sound block named {soundBlock.CustomName}");
                }
            }
        }

        void DisplaySoundList()
        {
            Echo("Displaying sound list (also in the programmable block custom data to copy paste it)");
            List<string> list = new List<string>();
            soundBlock.GetSounds(list);
            Me.CustomData = "";
            for (int i = 0; i < list.Count; i++)
            {
                Echo(list[i]);
                Me.CustomData += list[i] + "\n";
            }
        }

        #region post_script

    }

}
#endregion post_script
