// COPYRIGHT 2014 by the Open Rails project.
// 
// This file is part of Open Rails.
// 
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Orts.Formats.Msts;
using ORTS.Common;
using ORTS.Common.Input;
using ORTS.Scripting.Api;
using Orts.Simulation;
using Orts.Simulation.RollingStocks;
using Orts.Simulation.Physics;
using ORTS.Settings;
using System.Globalization;
using TRAINOBJECTTYPE = Orts.Simulation.Physics.Train.TrainObjectItem.TRAINOBJECTTYPE;
using Orts.Simulation.Signalling;

namespace Orts.Common.Scripting
{
    public enum Variable
    {
        // Generally, apart from exceptions, index 0 means all cars in the train except current, 1 means current car.
        //
        // Get or set controller (defined in eng file) positions directly. Should rarely be needed to set.
        OrtsDirection, // value -1: backwards, 0: neutral, 1: forwards
        OrtsThrottle, // value 0-1
        OrtsDynamicBrake, // value 0-1
        OrtsEngineBrake, // value 0-1
        OrtsTrainBrake, // value 0-1

        // Controller interventions without actually moving the levers.
        OrtsThrottleIntervention, // value -1: off, 0-1: active
        OrtsDynamicBrakeIntervention, // value -1:off, 0-1: active
        OrtsEngineBrakeIntervention, // value -1: off, 0: neutral, 1: full service, 2: emergency
        OrtsTrainBrakeIntervention, // value -1: off, 0: neutral, 1: full service, 2: emergency

        // Subsystems keyboard commands already exist for
        OrtsBailOff,
        OrtsHandbrake,
        OrtsTrainRetainers, // index is 0
        OrtsBrakeHoseConnect,
        OrtsSander,
        OrtsWiper,
        OrtsHorn,
        OrtsBell,
        OrtsHeadLight, // value 0: off, 1: neutral, 2: on
        OrtsCabLight, // value 0: off, 1: on
        OrtsPowerOn, // get - index: dieselEngine nr.; value 0: stopped, 1: starting, 2: running, 3: stopping; set - index 1, value 0: off, 1: on
        OrtsMirror, // 0: close, 1: open
        OrtsCylinderCock, // 0: close, 1: open
        OrtsOdoMeter, // get - index 0: head, 1: tail; set - index 1 & value 0: reset
        OrtsPantograph, // index is the pantograph number, on set the event gets sent to the whole train
        OrtsDoors, // index 0: left, 1: right, on set the doors open/close on the whole train

        // Subsystems no dedicated keyboard assigned for
        OrtsAuxPowerOn,
        OrtsPowerAuthorization,
        OrtsCircuitBreakerClosingOrder,
        OrtsCircuitBreakerOpeningOrder,
        OrtsTractionAuthorization,
        OrtsFullDynamicBrakingOrder,
        OrtsCircuitBraker,
        OrtsCompressor,
        OrtsAcceptRemoteControlSignals,

        // Physics parameters
        OrtsGameTimeS, // read-only
        OrtsClockTimeS, // read-only
        OrtsSpeedMpS, // read-only
        OrtsDistanceM, // read-only
        OrtsBrakeResevoirPressureBar, // read-only, index 0: vacuum, 1: brake cylinder, 2: main reservoir
        OrtsBrakeLinePressureBar, // index 1: main (ro), 2: equalising, 3: engine brake, 4: EP control

        // Train protection
        OrtsAlerterButton, // value 0: released, 1: pressed or reset
        OrtsEmergencyPushButton, // value 0: released, 1: pressed
        OrtsVigilanceAlarm, // get - alerter sound is on; set - value 0: off, 1: on
        OrtsMonitoringState, // index is 1
        OrtsInterventionSpeedMpS, // index 0: release, 1: apply

        // Signalling data, read-only
        // For these index 0: current, x: next x. Second index: zero based head number
        OrtsSignalDistanceM, // (n) + (h, function)
        OrtsSignalAltitudeM, // (n) + (h, function)
        OrtsSignalHeadsCount, // (n) + (h, function)
        OrtsSignalHeadAspect, // (n, h, function)
        OrtsSignalHeadSpeedLimitMpS, // (n, h)
        OrtsSignalHeadsAspetsCount, // (n, h)
        OrtsSignalHeadAspectsList, // (n, h) - space sep. string or binary coded int
        OrtsSignalHeadType, // (n, h, function) - string: the list read from sigcfg.dat SignalTypes() and OrtsNormalSubtypes()
        OrtsSignalHeadFunction, /// (n, h) - string: can be any <see cref="MstsSignalFunction"/> + any string defined in sigcfg.dat OrtsSignalFunctions() (e.g. NORMAL, DISTANCE, stc.)
        OrtsSpeedPostDistanceM, // (n)
        OrtsSpeedPostAltitudeM, // (n)
        OrtsSpeedPostSpeedLimitMpS, // (n)
        OrtsMilePostDistanceM, // (n)
        OrtsMilePostAltitudeM, // (n)
        OrtsMilePostValue, // (n) - float
        OrtsFacingDivergingSwitchDistanceM, // (n, maxDist)
        OrtsFacingDivergingSwitchAltitudeM, // (n, maxDist)
        OrtsTrailingDivergingSwitchDistanceM, // (n, maxDist)
        OrtsTrailingDivergingSwitchAltitudeM, // (n, maxDist)
        OrtsStationDistanceM, // (n)
        OrtsStationPlatformLengthM, // (n)
        OrtsStationName, // (n) - string
        OrtsTunnelEntranceDistanceM, // (n)

        // Normally a script shouldn't have to deal with the following. What is it used for in scripts?
        OrtsTrainControlMode, /// <see cref="TRAIN_CONTROL"/>

        // Sound triggers and others, write-only
        OrtsDiscreteTrigger, // value is the trigger number
        OrtsInitializeBrakes, // index is 0. This should not be called in any normal circumstances, it is here just for the sake of the game.
        OrtsSignalEvent, /// FIXME: unimplemented, index 0: train, 1: locomotive. The value is the <see cref="Event"/> as int
        OrtsConfirmMessage, /// index is the <see cref="ConfirmLevel"/>, value is the message as string
        OrtsConfirmNameWithSetting, /// index is the <see cref="CabSetting"/>, value is the <see cref="CabControl"/> as int
        // Unimplemented:
        //OrtsConfirmNameWithPercent,
        //OrtsConfirmNameWithSettingPercent,
        //OrtsConfirmNameWithPercentSetting,

        // Locomotive states, read-only
        OrtsFlippedLeftRight, // only for custom commands, no need to use this for built-in commands like OrtsDoors, those should work properly without this
        OrtsLocomotiveType, // 0: none, 1: steam, 2: diesel, 3: electric
        OrtsTrainLength, // FIXME: unimplemented
    }

    public class ContentScript
    {
        /// Control types to be used by functions e.g. GetFloatVariable, SetBoolVariable:
        ///
        /// 1. OpenRails keyboard commands
        ///      - Defined in the <see cref="UserCommand"/> enum, e.g. ControlThrottleStartIncrease, ControlHorn
        ///      - Index: always 1
        ///      - By taking over control, the automatic execution upon keypress/release is disabled, an event is signalled to the script instead.
        ///      - Set: Take over control from core (1), release control (0).
        ///      - Get: Return result of IsDown method, whether key is released/pressed (0/1).
        ///      - Calling back built-in keyboard commands by the script is not made possible intetionally, because OR executes these commands
        ///      - on the locomotive attached to the actual viewer, not the one attached to the script, so this would lead to undesired results.
        ///      - The script writer must use the additional scripting variables to set these as needed. (See point 4.)
        ///
        /// 2. Custom keyboard commands
        ///      - Defined in the <see cref="KeyMap"/>, e.g. keymap.json.
        ///      - Index: always 1
        ///      - When the assigned key is pressed or released, an event is signalled to the script to handle.
        ///      - Can be StartContinuousChange or StopContinuousChange with predefined speed 1/s.
        ///      - Can be ChangeTo float value, where a setting from float.MinValue to MaxValue represents a toggle between 0 and 1.
        ///      - Name can be any string except the built-in MSTS control names or the ones starting with "Control" or "Orts".
        ///      - Set: Force signalling an event of press (1) or release (0) to the script.
        ///      - Get: Return result of IsDown method, whether key is pressed/released.
        ///
        /// 3. Built-in named cabview control types
        ///      - Defined in the <see cref="CABViewControlTypes"/> enum, e.g. WIPERS, HORN, AMMETER.
        ///      - Index: always 1
        ///      - By taking over control the displayed value in cabview will be the one set by the script.
        ///      - Can be used as an animation node name in 3D cabviews.
        ///      - Set: Set an overridden value to be displayed in cabview. After first use the built-in calculated data will stopped to be displayed.
        ///      - Get: Get the data value of the original control, in units as configured in cvf. If unconfigured, get in SI units.
        ///
        /// 4. Additional OpenRails controls
        ///      - Defined in the <see cref="Variable"/> enum, e.g. OrtsSignalSpeedLimitMpS, OrtsSpeedMpS
        ///      - Index: Some of them have index values other than 1.
        ///      - Make interaction with OpenRails physics and signalling code possible.
        ///      - Some of the controls are read-only, setting these has no effect.
        ///      - Set: Set the value of associated parameter, nothing special.
        ///      - Get: Get the value of associated parameter, nothing special.
        ///
        /// 5. Custom controls
        ///      - Defined implicitly at first use
        ///      - Index: always 1
        ///      - Can be any string except the built-in MSTS control names or the ones starting with "Control" or "Orts".
        ///      - Can be used as an animation node name in 3D cabviews.
        ///      - Can be thought on it as a variable common to all scripts for a particular locomotive.
        ///      - Set: Set the value of control. Custom name will be registered implicitly at first use.
        ///      - Get: Get the value of control. Custom name will be registered implicitly at first use.
        ///

        private readonly MSTSLocomotive Locomotive;
        private readonly Simulator Simulator;

        private readonly List<string> ScriptNames = new List<string>();
        private readonly List<ControllerScript> Scripts = new List<ControllerScript>();
        public readonly Dictionary<ControllerScript, string> SoundManagementFiles = new Dictionary<ControllerScript, string>();

        // Receives a delegate from Viewer3D.UserInput:
        public static Func<UserCommand, bool> UserInputIsDown;
        
        /// <summary>
        /// Command names the script wants to handle instead.
        /// It is a built-in UserCommand "ControlXxxx" name the execution was taken over by the script.
        /// An event is signalled to the script with command name, if associated key is pressed or released.
        /// </summary>
        private readonly List<string> DisabledBuiltInCommands = new List<string>();
        /// <summary>
        /// Control names the script wants to handle, assigned to a blank or to their cvf-defined cabview configuration.
        /// It can be either a custom control name, or a built-in MSTS cabview control name, for which the script wants to override the visible value.
        /// A name can be added here by RegisterControl script method. Custom controls will be commonly visible among scripts of a particular locomotive.
        /// </summary>
        public readonly Dictionary<string, CabViewControl> ScriptedControls = new Dictionary<string, CabViewControl>();
        /// <summary>
        /// Controls of the <see cref="CABViewControlTypes"/> configured in the cvf files, assigned to their configuration object.
        /// </summary>
        private readonly Dictionary<string, CabViewControl> ConfiguredCabviewControls = new Dictionary<string, CabViewControl>();

        /// <summary>
        /// String -> enum lookup table for <see cref="CABViewControlTypes"/> to be used in GetBoolVariable("WIPERS") style script methods,
        /// defined to avoid using code reflection beyond initialization.
        /// </summary>
        private static readonly Dictionary<string, UserCabViewControl> AllCabviewControlTypes = new Dictionary<string, UserCabViewControl>();
        /// <summary>
        /// String -> enum lookup table for <see cref="Variable"/> controls to be used in GetFloatVariable("OrtsSpeedMpS") style script methods,
        /// defined to avoid using code reflection beyond initialization.
        /// </summary>
        private static readonly Dictionary<string, UserCabViewControl> AllOrtsVariables = new Dictionary<string, UserCabViewControl>();
        /// <summary>
        /// A string -> enum lookup table for <see cref="UserCommand"/> starting with "Control" (e.g. ControlThrottleIncrease).
        /// This is defined to avoid using code reflection beyond initialization.
        /// </summary>
        public static readonly Dictionary<string, UserCommand> AllUserCommands = new Dictionary<string, UserCommand>();
        
        public ContentScript(MSTSLocomotive locomotive)
        {
            Simulator = locomotive.Simulator;
            Locomotive = locomotive;

            if (AllCabviewControlTypes.Count == 0)
                foreach (var controlType in (CABViewControlTypes[])Enum.GetValues(typeof(CABViewControlTypes)))
                    AllCabviewControlTypes.Add(controlType.ToString(), new UserCabViewControl() { ControlType = controlType });

            if (AllOrtsVariables.Count == 0)
                foreach (var controlType in (Variable[])Enum.GetValues(typeof(Variable)))
                    AllOrtsVariables.Add(controlType.ToString(), new UserCabViewControl() { OrtsVariable = controlType });

            if (AllUserCommands.Count == 0)
                foreach (var controlCommand in (UserCommand[])Enum.GetValues(typeof(UserCommand)))
                    AllUserCommands.Add(controlCommand.ToString(), controlCommand);
        }

        public ContentScript(ContentScript contentScript) : this(contentScript.Locomotive) => ScriptNames = contentScript.ScriptNames;

        public ContentScript Clone() => new ContentScript(this);

        public void ParseScripts(string lowercasetoken, Parsers.Msts.STFReader stf)
        {
            switch (lowercasetoken)
            {
                case "engine(ortsscripts":
                    stf.MustMatch("(");
                    string script;
                    while ((script = stf.ReadItem()) != ")")
                        ScriptNames.Add(script);
                    break;
            }
        }
        
        public void Initialize()
        {
            foreach (var cabViewList in Locomotive.CabViewList)
                foreach (var cabViewControl in cabViewList.CVFFile.CabViewControls)
                    if (cabViewControl.ControlType != CABViewControlTypes.NONE && !ConfiguredCabviewControls.ContainsKey(cabViewControl.ControlType.ToString()))
                        ConfiguredCabviewControls.Add(cabViewControl.ControlType.ToString(), cabViewControl);

            var pathArray = new[] { Path.Combine(Path.GetDirectoryName(Locomotive.WagFilePath), "Script") };
            var soundPathArray = new[] {
                Path.Combine(Path.GetDirectoryName(Locomotive.WagFilePath), "SOUND"),
                Path.Combine(Simulator.BasePath, "SOUND"),
            };
            foreach (var scriptName in ScriptNames)
            {
                if (!(Simulator.ScriptManager.Load(pathArray, scriptName) is ControllerScript script))
                    continue;

                Scripts.Add(script);
                SetVariableDelegates(script);

                var soundPath = ORTSPaths.GetFileFromFolders(soundPathArray, script.SoundFileName);
                if (File.Exists(soundPath))
                    SoundManagementFiles.Add(script, soundPath);
                
                script.Initialize();
            }

            if (Scripts.Count == 0)
                Scripts.Add(new DummyControllerScript());
        }

        public void SetVariableDelegates(AbstractScriptClass script)
        {
            script._getFloatDelegate = (variableName, index1, index2, index3) => { GetFloatVariable(variableName, index1, index2, index3, out var floatResult, true); return floatResult; };
            script._getIntDelegate = (variableName, index1, index2, index3) => { GetIntVariable(variableName, index1, index2, index3, out var intResult, true); return intResult; };
            script._getBoolDelegate = (variableName, index1, index2, index3) => { GetBoolVariable(variableName, index1, index2, index3, out var boolResult, true); return boolResult; };
            script._getStringDelegate = (variableName, index1, index2, index3) => { GetStringVariable(variableName, index1, index2, index3, out var stringResult, true); return stringResult; };
            script._setFloatDelegate = (variableName, index, value) => SetFloatVariable(variableName, index, value, true);
            script._setIntDelegate = (variableName, index, value) => SetIntVariable(variableName, index, value, true);
            script._setBoolDelegate = (variableName, index, value) => SetBoolVariable(variableName, index, value, true);
            script._setStringDelegate = (variableName, index, value) => SetStringVariable(variableName, index, value, true);
        }

        public class UserCabViewControl : CabViewControl
        {
            public Variable OrtsVariable;

            public UserCabViewControl() { }
        }

        /// <summary>
        /// Called to define a new custom control or command, or override a built-in MSTS cabview control value, or disable a built-in keyboard command
        /// </summary>
        public bool RegisterControl(string controlName, int index)
        {
            if (controlName.ToLower().StartsWith("orts")) return false;

            if (AllUserCommands.ContainsKey(controlName))
            {
                // Disable original UserCommand, not to execute automatically, but signal an event to the script instead.
                if (!DisabledBuiltInCommands.Contains(controlName))
                    DisabledBuiltInCommands.Add(controlName);
            }
            else
            {
                if (controlName.ToLower().StartsWith("control")) return false;
                if (!ScriptedControls.ContainsKey(controlName))
                    ScriptedControls.Add(controlName, ConfiguredCabviewControls.ContainsKey(controlName) ? ConfiguredCabviewControls[controlName] : new UserCabViewControl() { MinValue = double.MinValue, MaxValue = double.MaxValue });
            }
            return true;
        }

        private void UnregisterControl(string controlName, int index) { if (index == 1) DisabledBuiltInCommands.Remove(controlName); }
        
        /// <summary>
        /// Execute action on either the whole train, or just the current locomotive, based on scope parameter
        /// </summary>
        /// <typeparam name="T">vehicle type</typeparam>
        /// <param name="scope">0: train, 1: current locomotive</param>
        /// <param name="action">action to execute</param>
        public void TrainCarAction<T>(int scope, Action<T> action) where T : MSTSWagon
        {
            if (scope == 0 && Locomotive.Train != null)
            {
                foreach (var car in Locomotive.Train.Cars)
                    if (car != Locomotive && car is T && car.AcceptMUSignals)
                        action(car as T);
            }
            else if (scope == 1 && Locomotive is T)
                action(Locomotive as T);
        }

        private bool TryUseScriptedControl(string controlName, int index, float value)
        {
            if (ScriptedControls.ContainsKey(controlName))
            {
                if (index == 0)
                    TrainCarAction<MSTSLocomotive>(0, l => l.ContentScript.SignalEvent(controlName, value));
                else if (index == 1)
                    ScriptedControls[controlName].OldValue = MathHelper.Clamp(value, (float)ScriptedControls[controlName].MinValue, (float)ScriptedControls[controlName].MaxValue);
                return true;
            }
            return false;
        }

        public bool GetFloatVariable(string controlName, int index, int head, int function, out float ret, bool doCast)
        {
            Train.TrainObjectItem sti(TRAINOBJECTTYPE type) => Locomotive.TrainControlSystem.SelectFromTrainInfo(type, index, ref head, ref function);
            Func<float> currentAltitudeM = () => Locomotive.WorldPosition.Location.Y;
            
            if (AllOrtsVariables.ContainsKey(controlName))
            {
                switch (AllOrtsVariables[controlName].OrtsVariable)
                {
                    case Variable.OrtsThrottle: ret = index != 1 ? 0 : Locomotive.ThrottleController.CurrentValue; return true;
                    case Variable.OrtsDynamicBrake: ret = index != 1 ? 0 : Locomotive.DynamicBrakeController.CurrentValue; return true;
                    case Variable.OrtsEngineBrake: ret = index != 1 ? 0 : Locomotive.EngineBrakeController.CurrentValue; return true;
                    case Variable.OrtsTrainBrake: ret = index != 1 ? 0 : Locomotive.TrainBrakeController.CurrentValue; return true;
                    case Variable.OrtsThrottleIntervention: ret = index != 1 ? 0 : Locomotive.ThrottleIntervention; return true;
                    case Variable.OrtsDynamicBrakeIntervention: ret = index != 1 ? 0 : Locomotive.DynamicBrakeIntervention; return true;
                    case Variable.OrtsEngineBrakeIntervention: ret = index != 1 ? 0 : Locomotive.EngineBrakeIntervention; return true;
                    case Variable.OrtsTrainBrakeIntervention: ret = index != 1 ? 0 : Locomotive.TrainBrakeIntervention; return true;
                    case Variable.OrtsTrainRetainers: ret = Locomotive.Train?.RetainerPercent / 100f ?? 0; return true;
                    case Variable.OrtsInterventionSpeedMpS: ret = index == 0 ? 0 : index == 1 ? Locomotive.TrainControlSystem.InterventionSpeedLimitMpS : 0; return true;
                    case Variable.OrtsSpeedMpS: ret = index != 1 ? 0 : Math.Abs(Locomotive.SpeedMpS); return true;
                    case Variable.OrtsDistanceM: ret = index != 1 ? 0 : Locomotive.DistanceM; return true;
                    case Variable.OrtsClockTimeS: ret = index != 1 ? 0 : (float)Locomotive.Simulator.ClockTime; return true;
                    case Variable.OrtsGameTimeS: ret = index != 1 ? 0 : (float)Locomotive.Simulator.GameTime; return true;
                    case Variable.OrtsOdoMeter: ret = index == 0 ? Locomotive.OdometerM : index == 1 && Locomotive.Train != null ? Locomotive.OdometerM - Locomotive.Train.Length : 0; return true;
                    case Variable.OrtsBrakeLinePressureBar:
                        switch (index)
                        {
                            case 1: ret = Bar.FromPSI(Locomotive.BrakeSystem.BrakeLine1PressurePSI); break;
                            case 2: ret = Bar.FromPSI(Locomotive.BrakeSystem.BrakeLine2PressurePSI); break;
                            case 3: ret = Bar.FromPSI(Locomotive.BrakeSystem.BrakeLine3PressurePSI); break;
                            case 4: ret = Locomotive.Train?.BrakeLine4 ?? 0; break;
                            default: ret = float.MinValue; break;
                        }
                        return true;
                    case Variable.OrtsBrakeResevoirPressureBar:
                        switch (index)
                        {
                            case 0: ret = Bar.FromPSI(Locomotive.BrakeSystem.GetVacResPressurePSI()); break;
                            case 1: ret = Bar.FromPSI(Locomotive.BrakeSystem.GetCylPressurePSI()); break;
                            case 2: ret = Bar.FromPSI(Locomotive.MainResPressurePSI); break;
                            default: ret = float.MinValue; break;
                        }
                        return true;
                    case Variable.OrtsSignalDistanceM: ret = index <= 0 ? -1 : sti(TRAINOBJECTTYPE.SIGNAL)?.DistanceToTrainM ?? float.MaxValue; return true;
                    //case Variable.OrtsSignalAltitudeM: ret = index <= 0 ? currentAltitudeM() : sti(TRAINOBJECTTYPE.SIGNAL)?.AltitudeM ?? float.MaxValue; return true;
                    case Variable.OrtsSignalHeadSpeedLimitMpS: ret = index <= 0 ? Locomotive.Train?.allowedMaxSpeedSignalMpS ?? -1 : sti(TRAINOBJECTTYPE.SIGNAL)?.AllowedSpeedMpS ?? -1; return true;
                    case Variable.OrtsSpeedPostDistanceM: ret = index <= 0 ? -1 : sti(TRAINOBJECTTYPE.SPEEDPOST)?.DistanceToTrainM ?? float.MaxValue; return true;
                    //case Variable.OrtsSpeedPostAltitudeM: ret = index <= 0 ? currentAltitudeM() : sti(TRAINOBJECTTYPE.SPEEDPOST)?.AltitudeM ?? float.MaxValue; return true;
                    case Variable.OrtsSpeedPostSpeedLimitMpS: ret = index <= 0 ? Locomotive.Train?.allowedMaxSpeedLimitMpS ?? -1 : sti(TRAINOBJECTTYPE.SPEEDPOST)?.AllowedSpeedMpS ?? -1; return true;
                    case Variable.OrtsMilePostDistanceM: ret = index <= 0 ? -1 : sti(TRAINOBJECTTYPE.MILEPOST)?.DistanceToTrainM ?? float.MaxValue; return true;
                    //case Variable.OrtsMilePostAltitudeM: ret = index <= 0 ? currentAltitudeM() : sti(TRAINOBJECTTYPE.MILEPOST)?.AltitudeM ?? float.MaxValue; return true;
                    case Variable.OrtsFacingDivergingSwitchDistanceM: ret = index <= 0 ? -1 : sti(TRAINOBJECTTYPE.FACING_SWITCH)?.DistanceToTrainM ?? float.MaxValue; return true;
                    //case Variable.OrtsFacingDivergingSwitchAltitudeM: ret = index <= 0 ? currentAltitudeM() : sti(TRAINOBJECTTYPE.MILEPOST)?.AltitudeM ?? float.MaxValue; return true;
                    case Variable.OrtsTrailingDivergingSwitchDistanceM: ret = index <= 0 ? -1 : sti(TRAINOBJECTTYPE.TRAILING_SWITCH)?.DistanceToTrainM ?? float.MaxValue; return true;
                    //case Variable.OrtsTrailingDivergingSwitchAltitudeM: ret = index <= 0 ? currentAltitudeM() : sti(TRAINOBJECTTYPE.MILEPOST)?.AltitudeM ?? float.MaxValue; return true;
                    case Variable.OrtsStationDistanceM: ret = index <= 0 ? -1 : sti(TRAINOBJECTTYPE.STATION)?.DistanceToTrainM ?? float.MaxValue; return true;
                    case Variable.OrtsStationPlatformLengthM: ret = index <= 0 ? -1 : sti(TRAINOBJECTTYPE.STATION)?.StationPlatformLength ?? float.MaxValue; return true;
                    case Variable.OrtsTunnelEntranceDistanceM: ret = index <= 0 ? -1 : sti(TRAINOBJECTTYPE.TUNNEL)?.DistanceToTrainM ?? float.MaxValue; return true;
                    default: break;
                }
            }
            // Make the original data available for scripts in first place, even if the control is taken over
            if (ConfiguredCabviewControls.ContainsKey(controlName)) { ret = index != 1 ? 0 : Locomotive.GetDataOf(ConfiguredCabviewControls[controlName]); return true; }
            else if (AllCabviewControlTypes.ContainsKey(controlName)) { ret = index != 1 ? 0 : Locomotive.GetDataOf(AllCabviewControlTypes[controlName]); return true; }
            else if (ScriptedControls.ContainsKey(controlName)) { ret = (float)ScriptedControls[controlName].OldValue; return true; }
            if (doCast)
            {
                if (GetIntVariable(controlName, index, head, function, out var intResult, false)) { ret = (float)intResult; return true; }
                if (GetBoolVariable(controlName, index, head, function, out var boolResult, false)) { ret = boolResult ? 1 : 0; return true; }
                if (GetStringVariable(controlName, index, head, function, out var stringResult, false)) { ret = stringResult == string.Empty ? 0 : 1; return true; }
                if (RegisterControl(controlName, index)) { ret = float.MinValue; return true; }
            }
            ret = float.MinValue;
            return false;
        }

        public bool GetIntVariable(string controlName, int index, int head, int function, out int ret, bool doCast)
        {
            Train.TrainObjectItem sti(TRAINOBJECTTYPE type) => Locomotive.TrainControlSystem.SelectFromTrainInfo(type, index, ref head, ref function);
            if (AllOrtsVariables.ContainsKey(controlName))
            {
                switch (AllOrtsVariables[controlName].OrtsVariable)
                {
                    case Variable.OrtsDirection: ret = index != 1 ? 0 : Locomotive.Direction == Direction.Reverse ? -1 : Locomotive.Direction == Direction.N ? 0 : 1; return true;
                    case Variable.OrtsHeadLight: ret = index != 1 ? 0 : Locomotive.Headlight; return true;
                    case Variable.OrtsPowerOn:
                        ret = !(Locomotive is MSTSDieselLocomotive)
                            ? (index == 1 && Locomotive.PowerOn ? 2 : 0)
                            : index >= 0 && index < (Locomotive as MSTSDieselLocomotive).DieselEngines.Count
                            ? (int)(Locomotive as MSTSDieselLocomotive).DieselEngines[index].EngineStatus
                            : 0;
                         return true;
                    // The following enum-s are handled as strings too:
                    case Variable.OrtsMonitoringState: ret = index != 1 ? 0 : (int)Locomotive.TrainControlSystem.MonitoringStatus; return true;
                    case Variable.OrtsTrainControlMode: ret = index != 1 ? 0 : (int)Locomotive.Train.ControlMode; return true;
                    case Variable.OrtsSignalHeadAspect: ret = (int)(sti(TRAINOBJECTTYPE.SIGNAL)?.SignalState ?? TrackMonitorSignalAspect.None); return true;

                    case Variable.OrtsSignalHeadsCount: ret = sti(TRAINOBJECTTYPE.SIGNAL)?.SignalObject.SignalHeads.Count ?? 0; return true;
                    case Variable.OrtsSignalHeadsAspetsCount:
                        ret = sti(TRAINOBJECTTYPE.SIGNAL)?.SignalObject.SignalHeads[head]?.signalType?.Aspects.Count ?? 0;
                        return true;
                    // Handled as string too, here it is a binary coded list by set bits:
                    case Variable.OrtsSignalHeadAspectsList:
                        var signalObject = sti(TRAINOBJECTTYPE.SIGNAL)?.SignalObject;
                        // The original format of these aspects is MstsSignalAspect. Need to use the TranslateTMAspect() to get them as Aspect or TrackMonitorSignalAspect.
                        ret = signalObject?.SignalHeads[head].signalType?.Aspects?.Sum(a => 1 << (int)signalObject.TranslateTMAspect(a.Aspect)) ?? 0;
                        return true;
                    case Variable.OrtsLocomotiveType:
                        if (Locomotive is MSTSSteamLocomotive) ret = 1;
                        else if (Locomotive is MSTSDieselLocomotive) ret = 2;
                        else if (Locomotive is MSTSElectricLocomotive) ret = 3;
                        else ret = 0;
                        return true;
                    default: break;
                }
            }
            if (doCast)
            {
                if (GetFloatVariable(controlName, index, head, function, out var floatResult, false)) { ret = (int)floatResult; return true; }
                if (GetBoolVariable(controlName, index, head, function, out var boolResult, false)) { ret = boolResult ? 1 : 0; return true; }
                if (GetStringVariable(controlName, index, head, function, out var stringResult, false)) { ret = stringResult == string.Empty ? 0 : 1; return true; }
                if (RegisterControl(controlName, index)) { ret = int.MinValue; return true; }
            }
            ret = int.MinValue;
            return false;
        }

        public bool GetBoolVariable(string controlName, int index, int head, int function, out bool ret, bool doCast)
        {
            if (AllOrtsVariables.ContainsKey(controlName))
            {
                switch (AllOrtsVariables[controlName].OrtsVariable)
                {
                    case Variable.OrtsCylinderCock: ret = (Locomotive as MSTSSteamLocomotive)?.CylinderCocksAreOpen ?? false; return true;
                    case Variable.OrtsCircuitBraker: ret = index == 1 && ((Locomotive as MSTSElectricLocomotive)?.PowerSupply.CircuitBreaker.State ?? CircuitBreakerState.Open) == CircuitBreakerState.Closed; return true;
                    case Variable.OrtsAuxPowerOn: ret = index == 1 && Locomotive.AuxPowerOn; return true;
                    case Variable.OrtsCompressor: ret = index == 1 && Locomotive.CompressorIsOn; return true;
                    case Variable.OrtsPowerAuthorization: ret = (Locomotive.Train?.LeadLocomotive as MSTSLocomotive ?? Locomotive).TrainControlSystem.PowerAuthorization; return true;
                    case Variable.OrtsCircuitBreakerClosingOrder: ret = (Locomotive.Train?.LeadLocomotive as MSTSLocomotive ?? Locomotive).TrainControlSystem.CircuitBreakerClosingOrder; return true;
                    case Variable.OrtsCircuitBreakerOpeningOrder: ret = (Locomotive.Train?.LeadLocomotive as MSTSLocomotive ?? Locomotive).TrainControlSystem.CircuitBreakerOpeningOrder; return true;
                    case Variable.OrtsTractionAuthorization: ret = (Locomotive.Train?.LeadLocomotive as MSTSLocomotive ?? Locomotive).TrainControlSystem.TractionAuthorization; return true;
                    case Variable.OrtsFullDynamicBrakingOrder: ret = (Locomotive.Train?.LeadLocomotive as MSTSLocomotive ?? Locomotive).TrainControlSystem.FullDynamicBrakingOrder; return true;
                    case Variable.OrtsBailOff: ret = index == 1 && Locomotive.BailOff; return true;
                    case Variable.OrtsHandbrake: ret = index == 1 && Locomotive.BrakeSystem.GetHandbrakeStatus(); return true;
                    case Variable.OrtsBrakeHoseConnect: ret = index == 1 && Locomotive.BrakeSystem.BrakeLine1PressurePSI >= 0; return true;
                    case Variable.OrtsSander: ret = index == 1 && Locomotive.Sander; return true;
                    case Variable.OrtsWiper: ret = index == 1 && Locomotive.Wiper; return true;
                    case Variable.OrtsHorn: ret = index == 1 && Locomotive.Horn; return true;
                    case Variable.OrtsBell: ret = index == 1 && Locomotive.Bell; return true;
                    case Variable.OrtsMirror: ret = index == 1 && Locomotive.MirrorOpen; return true;
                    case Variable.OrtsAcceptRemoteControlSignals: ret = index == 1 && Locomotive.AcceptMUSignals; return true;
                    case Variable.OrtsDoors: ret = index == 0 ^ Locomotive.GetCabFlipped() ? Locomotive.DoorLeftOpen : Locomotive.DoorRightOpen; return true;
                    case Variable.OrtsPantograph: ret = Locomotive.Pantographs[index].CommandUp; return true;
                    case Variable.OrtsCabLight: ret = index == 1 && Locomotive.CabLightOn; return true;
                    case Variable.OrtsAlerterButton: ret = index == 1 && Locomotive.TrainControlSystem.AlerterButtonPressed; return true;
                    case Variable.OrtsEmergencyPushButton: ret = index == 1 && Locomotive.EmergencyButtonPressed; return true;
                    case Variable.OrtsVigilanceAlarm: ret = index == 1 && Locomotive.AlerterSnd; return true;
                    case Variable.OrtsFlippedLeftRight: ret = index == 1 && Locomotive.GetCabFlipped(); return true;
                    default: break;
                }
            }
            if (AllUserCommands.ContainsKey(controlName)) { ret = UserInputIsDown(AllUserCommands[controlName]); return true; }
            if (doCast)
            {
                if (GetIntVariable(controlName, index, head, function, out var intResult, false)) { ret = intResult > 0; return true; }
                if (GetFloatVariable(controlName, index, head, function, out var floatResult, false)) { ret = floatResult > 0; return true; }
                if (GetStringVariable(controlName, index, head, function, out var stringResult, false)) { ret = stringResult != string.Empty; return true; }
                if (RegisterControl(controlName, index)) { ret = false; return true; }
            }
            ret = false;
            return false;
        }

        public bool GetStringVariable(string controlName, int index, int head, int function, out string ret, bool doCast)
        {
            Train.TrainObjectItem sti(TRAINOBJECTTYPE type) => Locomotive.TrainControlSystem.SelectFromTrainInfo(type, index, ref head, ref function);
            if (AllOrtsVariables.ContainsKey(controlName))
            {
                switch (AllOrtsVariables[controlName].OrtsVariable)
                {
                    // The following enum-s are handled as int-s too:
                    case Variable.OrtsMonitoringState: ret = index != 1 ? string.Empty : Locomotive.TrainControlSystem.MonitoringStatus.ToString(); return true;
                    case Variable.OrtsTrainControlMode: ret = Locomotive.Train.ControlMode.ToString(); return true;
                    // TODO: Signaling variables
                    case Variable.OrtsSignalHeadAspect: ret = (sti(TRAINOBJECTTYPE.SIGNAL)?.SignalState ?? TrackMonitorSignalAspect.None).ToString(); return true;
                    case Variable.OrtsSignalHeadFunction: ret = (sti(TRAINOBJECTTYPE.SIGNAL)?.SignalObject?.SignalHeads[head].sigFunction ?? MstsSignalFunction.UNKNOWN).ToString(); return true;
                    case Variable.OrtsSignalHeadType: ret = sti(TRAINOBJECTTYPE.SIGNAL)?.SignalObject?.SignalHeads[head].signalType.Name ?? string.Empty; return true;
                    case Variable.OrtsSignalHeadAspectsList:
                        var signalObject = sti(TRAINOBJECTTYPE.SIGNAL)?.SignalObject;
                        ret = string.Join(", ", signalObject?.SignalHeads[head].signalType?.Aspects?.Select(a => ((Aspect)signalObject.TranslateTMAspect(a.Aspect)).ToString()) ?? Array.Empty<string>());
                        return true;
                    case Variable.OrtsMilePostValue: ret = sti(TRAINOBJECTTYPE.MILEPOST)?.ThisMile ?? string.Empty; return true;
                    case Variable.OrtsStationName: ret = index > 0 && Locomotive.Train?.StationStops?.Count > index - 1 ? Locomotive.Train.StationStops[index - 1].PlatformItem.Name : string.Empty ?? string.Empty; return true;
                    default: break;
                }
            }
            if (doCast)
            {
                if (GetIntVariable(controlName, index, head, function, out var intResult, false)) { ret = intResult.ToString(); return true; }
                if (GetFloatVariable(controlName, index, head, function, out var floatResult, false)) { ret = floatResult.ToString(); return true; }
                if (GetBoolVariable(controlName, index, head, function, out var boolResult, false)) { ret = boolResult.ToString(); return true; }
                if (RegisterControl(controlName, index)) { ret = string.Empty; return true; }
            }
            ret = string.Empty;
            return false;
        }

        public bool SetFloatVariable(string controlName, int index, float value, bool doCast)
        {
            if (AllOrtsVariables.ContainsKey(controlName))
            {
                switch (AllOrtsVariables[controlName].OrtsVariable)
                {
                    // Board computer doesn't normally move levers, just overrides their settings
                    case Variable.OrtsThrottle: if (index == 1) Locomotive.ThrottleController.SetValue(MathHelper.Clamp(value, 0, 1)); return true;
                    case Variable.OrtsDynamicBrake: if (index == 1) Locomotive.DynamicBrakeController.SetValue(MathHelper.Clamp(value, -1, 1)); return true;
                    case Variable.OrtsEngineBrake: if (index == 1) Locomotive.EngineBrakeController.SetValue(MathHelper.Clamp(value, 0, 1)); return true;
                    case Variable.OrtsTrainBrake: if (index == 1) Locomotive.TrainBrakeController.SetValue(MathHelper.Clamp(value, 0, 1)); return true;
                    case Variable.OrtsThrottleIntervention: TrainCarAction<MSTSLocomotive>(index, l => l.ThrottleIntervention = MathHelper.Clamp(value, -1, 1)); return true;
                    case Variable.OrtsDynamicBrakeIntervention: TrainCarAction<MSTSLocomotive>(index, l => l.DynamicBrakeIntervention = MathHelper.Clamp(value, -1, 1)); return true;
                    case Variable.OrtsEngineBrakeIntervention: TrainCarAction<MSTSLocomotive>(index, l => l.EngineBrakeIntervention = (int)MathHelper.Clamp(value, -1, 2)); return true;
                    case Variable.OrtsTrainBrakeIntervention: TrainCarAction<MSTSLocomotive>(index, l => l.TrainBrakeIntervention = (int)MathHelper.Clamp(value, -1, 2)); return true;
                    case Variable.OrtsHandbrake: TrainCarAction<MSTSWagon>(index, l => l.BrakeSystem.SetHandbrakePercent(value * 100f)); return true;
                    case Variable.OrtsInterventionSpeedMpS: if (index == 0) { } else if (index == 1) Locomotive.TrainControlSystem.InterventionSpeedLimitMpS = value; return true;
                    case Variable.OrtsOdoMeter: if (index == 1 && value == 0) Locomotive.OdometerReset(); return true;
                    case Variable.OrtsBrakeLinePressureBar:
                        switch (index)
                        {
                            case 1: Locomotive.BrakeSystem.BrakeLine1PressurePSI = Bar.ToPSI(value); return true;
                            case 2: Locomotive.BrakeSystem.BrakeLine2PressurePSI = Bar.ToPSI(value); return true;
                            case 3: Locomotive.BrakeSystem.BrakeLine3PressurePSI = Bar.ToPSI(value); return true;
                            case 4: if (Locomotive.Train != null && Locomotive.IsLeadLocomotive()) Locomotive.Train.BrakeLine4 = value; return true;
                            default: return true;
                        }
                    default: break;
                }
            }
            if (TryUseScriptedControl(controlName, index, value)) return true;
            if (doCast)
            {
                if (SetIntVariable(controlName, index, (int)value, false)) return true;
                if (SetBoolVariable(controlName, index, value > 0, false)) return true;
                if (SetStringVariable(controlName, index, value.ToString(), false)) return true;
                if (RegisterControl(controlName, index)) return TryUseScriptedControl(controlName, index, value);
            }
            return false;
        }

        public bool SetIntVariable(string controlName, int index, int value, bool doCast)
        {
            if (AllOrtsVariables.ContainsKey(controlName))
            {
                switch (AllOrtsVariables[controlName].OrtsVariable)
                {
                    case Variable.OrtsDirection: if (index == 1) Locomotive.SetDirection(value > 0 ? Direction.Forward : value < 0 ? Direction.Reverse : Direction.N); return true;
                    case Variable.OrtsDiscreteTrigger: TrainCarAction<MSTSLocomotive>(index, l => HandleEvent(l.EventHandlers, (int)value)); return true;
                    case Variable.OrtsPowerOn: TrainCarAction<MSTSLocomotive>(index, l => l.SetPower(value > 0)); return true;
                    case Variable.OrtsHeadLight: if (index == 1) Locomotive.Headlight = (int)MathHelper.Clamp(value, 0, 2); return true;
                    case Variable.OrtsMonitoringState: if (index == 0) Locomotive.TrainControlSystem.MonitoringStatus = (MonitoringStatus)(int)MathHelper.Clamp(value, 0, 5); return true;
                    case Variable.OrtsConfirmNameWithSetting:
                        if (Locomotive == Locomotive.Simulator.PlayerLocomotive)
                            Locomotive.Simulator.Confirmer.Confirm((CabControl)value, (CabSetting)index);
                        return true;
                    default: break;
                }
            }
            // Enable/disable built-in keyboard commands
            if (AllUserCommands.ContainsKey(controlName))
            {
                if (index == 1)
                {
                    switch (value)
                    {
                        case 0: UnregisterControl(controlName, index); break; // enable
                        case 1: RegisterControl(controlName, index); break; // disable
                        default: break;
                    }
                }
                return true;
            }
            if (doCast)
            {
                if (SetFloatVariable(controlName, index, value, false)) return true;
                if (SetBoolVariable(controlName, index, value > 0, false)) return true;
                if (SetStringVariable(controlName, index, value.ToString(), false)) return true;
                if (RegisterControl(controlName, index)) return TryUseScriptedControl(controlName, index, (float)value);
            }
            return false;
        }

        public bool SetBoolVariable(string controlName, int index, bool value, bool doCast)
        {
            if (AllOrtsVariables.ContainsKey(controlName))
            {
                switch (AllOrtsVariables[controlName].OrtsVariable)
                {
                    // Board computer doesn't normally move levers, just overrides their settings
                    case Variable.OrtsPowerAuthorization: TrainCarAction<MSTSLocomotive>(index, l => l.TrainControlSystem.PowerAuthorization = value); return true;
                    case Variable.OrtsCircuitBraker: TrainCarAction<MSTSLocomotive>(index, l => l.SignalEvent(value ? PowerSupplyEvent.CloseCircuitBreaker : PowerSupplyEvent.OpenCircuitBreaker, index)); return true;
                    case Variable.OrtsCircuitBreakerClosingOrder: TrainCarAction<MSTSLocomotive>(index, l => l.TrainControlSystem.CircuitBreakerClosingOrder = value); return true;
                    case Variable.OrtsCircuitBreakerOpeningOrder: TrainCarAction<MSTSLocomotive>(index, l => l.TrainControlSystem.CircuitBreakerOpeningOrder = value); return true;
                    case Variable.OrtsTractionAuthorization: TrainCarAction<MSTSLocomotive>(index, l => l.TrainControlSystem.TractionAuthorization = value); return true;
                    case Variable.OrtsFullDynamicBrakingOrder: TrainCarAction<MSTSLocomotive>(index, l => l.TrainControlSystem.FullDynamicBrakingOrder = value); return true;
                    case Variable.OrtsAuxPowerOn: TrainCarAction<MSTSElectricLocomotive>(index, l => l.PowerSupply.AuxiliaryState = value ? PowerSupplyState.PowerOn : PowerSupplyState.PowerOff); return true;
                    case Variable.OrtsCompressor: if (index == 1) Locomotive.SignalEvent(value ? Event.CompressorOn : Event.CompressorOff); return true;
                    case Variable.OrtsPantograph: Locomotive.Train?.SignalEvent(value ? PowerSupplyEvent.RaisePantograph : PowerSupplyEvent.LowerPantograph, index); return true;
                    case Variable.OrtsBailOff: if (index == 1) Locomotive.SetBailOff(value); return true;
                    case Variable.OrtsInitializeBrakes: if (index == 0) Locomotive.Train?.UnconditionalInitializeBrakes(); return true;
                    case Variable.OrtsTrainRetainers: if (index == 0) Locomotive.SetTrainRetainers(value); return true;
                    case Variable.OrtsBrakeHoseConnect: if (index == 1) Locomotive.BrakeHoseConnect(value); return true;
                    case Variable.OrtsSander: TrainCarAction<MSTSLocomotive>(index, l => l.SignalEvent(value ? Event.SanderOn : Event.SanderOff)); return true;
                    case Variable.OrtsWiper: if (index == 1) Locomotive.SignalEvent(value ? Event.WiperOn : Event.WiperOff); return true;
                    case Variable.OrtsHorn: if (index == 1) Locomotive.ManualHorn = value; return true;
                    case Variable.OrtsBell: if (index == 1) Locomotive.ManualBell = value; return true;
                    case Variable.OrtsCabLight: if (index == 1 && (value != Locomotive.CabLightOn)) Locomotive.ToggleCabLight(); return true;
                    case Variable.OrtsAlerterButton: if (index == 1) { Locomotive.AlerterPressed(value); if (value) Locomotive.SignalEvent(Event.VigilanceAlarmReset); } return true;
                    case Variable.OrtsEmergencyPushButton: if (index == 1) { Locomotive.EmergencyButtonPressed = !Locomotive.EmergencyButtonPressed; Locomotive.TrainBrakeController.EmergencyBrakingPushButton = Locomotive.EmergencyButtonPressed; } return true;
                    case Variable.OrtsVigilanceAlarm: if (index == 1) Locomotive.SignalEvent(value ? Event.VigilanceAlarmOn : Event.VigilanceAlarmOff); return true;
                    case Variable.OrtsMirror: if (index == 1 && (value != Locomotive.MirrorOpen)) Locomotive.ToggleMirrors(); return true;
                    case Variable.OrtsAcceptRemoteControlSignals: if (index == 1) Locomotive.AcceptMUSignals = value; return true;
                    case Variable.OrtsDoors:
                        if (index == 1 ^ Locomotive.GetCabFlipped()) { if (Locomotive.DoorRightOpen != value) Locomotive.ToggleDoorsRight(); }
                        else { if (Locomotive.DoorLeftOpen != value) Locomotive.ToggleDoorsLeft(); }
                        return true;
                    case Variable.OrtsCylinderCock: TrainCarAction<MSTSSteamLocomotive>(index, l => { if (l.CylinderCocksAreOpen != value) l.ToggleCylinderCocks(); }); return true;
                    default: break;
                }
            }
            if (doCast)
            {
                if (SetFloatVariable(controlName, index, value ? 1 : 0, false)) return true;
                if (SetIntVariable(controlName, index, value ? 1 : 0, false)) return true;
                if (SetStringVariable(controlName, index, value.ToString(), false)) return true;
                if (RegisterControl(controlName, index)) return TryUseScriptedControl(controlName, index, value ? 1 : 0);
            }
            return false;
        }

        public bool SetStringVariable(string controlName, int index, string value, bool doCast)
        {
            if (AllOrtsVariables.ContainsKey(controlName))
            {
                switch (AllOrtsVariables[controlName].OrtsVariable)
                {
                    case Variable.OrtsConfirmMessage:
                        if (Locomotive == Locomotive.Simulator.PlayerLocomotive)
                            Locomotive.Simulator.Confirmer.Message((ConfirmLevel)index, value);
                        return true;
                    default: break;
                }
            }
            if (doCast)
            {
                if (float.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var floatValue)
                    && SetFloatVariable(controlName, index, floatValue, false)) return true;
                if (int.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var intValue)
                    && SetIntVariable(controlName, index, intValue, false)) return true;
                if (bool.TryParse(value, out var boolValue)
                    && SetBoolVariable(controlName, index, boolValue, false)) return true;
                if (RegisterControl(controlName, index)) return TryUseScriptedControl(controlName, index, floatValue);
            }
            return false;
        }

        /// <summary>
        /// Return the override value for a built-in MSTS cabcontrol.
        /// </summary>
        public float GetScriptedDataOf(CabViewControl cvc)
        {
            if (ScriptedControls.ContainsKey(cvc.ControlType.ToString()))
                return (float)ScriptedControls[cvc.ControlType.ToString()].OldValue;
            
            return Locomotive.GetDataOf(cvc);
        }
        
        public void Update(float elapsedSeconds)
        {
            switch (Locomotive.Train.TrainType)
            {
                case Train.TRAINTYPE.STATIC:
                case Train.TRAINTYPE.REMOTE:
                case Train.TRAINTYPE.AI:
                case Train.TRAINTYPE.AI_AUTOGENERATE:
                case Train.TRAINTYPE.AI_NOTSTARTED:
                case Train.TRAINTYPE.AI_INCORPORATED:
                    return;
            }

            foreach (var script in Scripts)
                script.Update(elapsedSeconds);

            //foreach (var aaa in Locomotive.Train.Cars)
            //    Console.WriteLine("___{0} {1} {2}", aaa.CarID, (aaa as MSTSWagon).DoorLeftOpen, (aaa as MSTSWagon).DoorRightOpen);
        }

        /// <summary>
        /// Handle sound triggers
        /// </summary>
        public static void HandleEvent(List<EventHandler> eventHandlers, int customEventID)
        {
            foreach (var eventHandler in eventHandlers)
                eventHandler.HandleEvent((Event)customEventID);
                //eventHandler.HandleEvent((Event)customEventID, script);
        }

        public bool SignalEvent(string controlName, float? value)
        {
            if (DisabledBuiltInCommands.Contains(controlName) || ScriptedControls.ContainsKey(controlName))
            {
                foreach (var script in Scripts)
                {
                    script.HandleEvent(controlName, value);
                }
                return true;
            }
            return false;
        }

        public void Save(BinaryWriter outf)
        {
            //outf.Write(CurrentValue);
        }

        public void Restore(BinaryReader inf)
        {
            //SendEvent(BrakeControllerEvent.SetCurrentValue, inf.ReadSingle());
        }
    }

    public class DummyControllerScript : ControllerScript
    {
        public DummyControllerScript() { }
        public override void Initialize() { }
        public override void Update(float elapsedClockSeconds) { }
        public override void HandleEvent(string controlName, float? value) { }
    }
}