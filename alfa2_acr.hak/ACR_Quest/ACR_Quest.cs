﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using CLRScriptFramework;
using ALFA;
using NWScript;
using NWScript.ManagedInterfaceLayer.NWScriptManagedInterface;

using NWEffect = NWScript.NWScriptEngineStructure0;
using NWEvent = NWScript.NWScriptEngineStructure1;
using NWLocation = NWScript.NWScriptEngineStructure2;
using NWTalent = NWScript.NWScriptEngineStructure3;
using NWItemProperty = NWScript.NWScriptEngineStructure4;

namespace ACR_Quest
{
    public partial class ACR_Quest : CLRScriptBase, IGeneratedScriptProgram
    {
        public ACR_Quest([In] NWScriptJITIntrinsics Intrinsics, [In] INWScriptProgram Host)
        {
            InitScript(Intrinsics, Host);
        }

        private ACR_Quest([In] ACR_Quest Other)
        {
            InitScript(Other);

            LoadScriptGlobals(Other.SaveScriptGlobals());
        }

        public static Type[] ScriptParameterTypes =
        { typeof(int), typeof(string), typeof(int), typeof(string) };

        public Int32 ScriptMain([In] object[] ScriptParameters, [In] Int32 DefaultReturnCode)
        {
            int command = (int)ScriptParameters[0]; // ScriptParameterTypes[0] is typeof(int)
            string name = (string)ScriptParameters[1];
            int state = (int)ScriptParameters[2];
            string template = (string)ScriptParameters[3];
            Infestation infest = null;

            switch((Command)command)
            {
                case Command.InitializeInfestations:
                    Infestation.InitializeInfestations();
                    break;
                case Command.CreateInfestation:
                    new Infestation(name, template, state, this);
                    break;
                case Command.GrowInfestation:
                    infest = QuestStore.GetInfestation(name);
                    if(infest != null)
                    {
                        infest.GrowInfestation(this);
                    }
                    break;
                case Command.AddSpawnToInfestation:

                    break;
                case Command.SetInfestationFecundity:
                    infest = QuestStore.GetInfestation(name);
                    if (infest != null)
                    {
                        infest.Fecundity = state;
                    }
                    break;
                case Command.PrintInfestations:
                    foreach(Infestation inf in QuestStore.LoadedInfestations)
                    {
                        SendMessageToAllDMs(inf.ToString());
                    }
                    break;
            }

            SendMessageToAllDMs(String.Format("Command: {0}, Name: {1}, State: {2}, Template: {3}.", command, name, state, template));
            return 0;
        }

        enum Command 
        {
            InitializeInfestations = 0,
            CreateInfestation = 1,
            GrowInfestation = 2,
            AddSpawnToInfestation = 3,
            SetInfestationFecundity = 4,

            PrintInfestations = 999,
        }

        public const string AREA_MAX_INFESTATION = "ACR_QST_MAX_INFESTATION";
        public const string GLOBAL_QUEST_INFESTATION_KEY = "Infestation";
    }
}
