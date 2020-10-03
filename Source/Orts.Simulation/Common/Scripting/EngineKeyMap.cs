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
using System.Diagnostics;
using System.IO;
using System.ComponentModel;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using ORTS.Settings;
using Orts.Simulation.RollingStocks;
using Orts.Simulation.Physics;
using ORTS.Common;
using ORTS.Common.Input;

namespace Orts.Common.Scripting
{
    public class KeyMap
    {
        private readonly MSTSLocomotive Locomotive;
        private string KeyMapFileName;
        private List<KeyMapCommand> KeyMapList;

        // These delegates are received from MSTSLocomotiveViewer:
        public static Func<UserCommandInput, bool> UserInputIsPressed;
        public static Func<UserCommandInput, bool> UserInputIsReleased;
        public static Action<UserCommand, int> UserInputCommands;

        /// <summary>
        /// Command names defined in keymap.json, assigned to their { released_command, pressed_command }.
        /// Only the custom command names are stored here.
        /// </summary>
        private readonly Dictionary<string, Dictionary<KeyMapCommand, UserCommandInput>> KeyMapCommands = new Dictionary<string, Dictionary<KeyMapCommand, UserCommandInput>>();
        /// <summary>
        /// Built-in command names the keymap redefines to a different key, assigned to their keyboard commands.
        /// An event is signalled to the script with command name, if associated key is pressed or released.
        /// </summary>
        private readonly Dictionary<UserCommand, UserCommandInput> RemappedCommands = new Dictionary<UserCommand, UserCommandInput>();
        /// <summary>
        /// Commands triggered with <see cref="KeyMapCommand.Events.StartContinuousChange"/>, actually changing continuously with their changing speed [1/s].
        /// When triggered with <see cref="KeyMapCommand.Events.StopContinuousChange"/>, a command is removed from this list.
        /// </summary>
        private readonly Dictionary<KeyMapCommand, float> ChangingControls = new Dictionary<KeyMapCommand, float>();

        private readonly Dictionary<string, KeyMapCommand> ActivatedCommands = new Dictionary<string, KeyMapCommand>();

        public KeyMap(MSTSLocomotive locomotive) => Locomotive = locomotive;

        public KeyMap(KeyMap keyMap) : this(keyMap.Locomotive) => KeyMapFileName = keyMap.KeyMapFileName;

        public KeyMap Clone() => new KeyMap(this);

        public void ParseScripts(string lowercasetoken, Parsers.Msts.STFReader stf) => KeyMapFileName = lowercasetoken == "engine(ortskeymap" ? stf.ReadStringBlock(null) : "";
        
        public void Initialize()
        {
            var keyMapPathArray = new[] {
                Path.Combine(Path.GetDirectoryName(Locomotive.WagFilePath), "Script"),
            };
            var keyMapPath = ORTSPaths.GetFileFromFolders(keyMapPathArray, KeyMapFileName ?? "");
            if (File.Exists(keyMapPath))
            {
                var streamReader = new StreamReader(keyMapPath);
                try
                {
                    KeyMapList = JsonConvert.DeserializeObject<List<KeyMapCommand>>(streamReader.ReadToEnd(), KeyMapSerializerSettings);
                }
                catch (Exception error)
                {
                    Trace.TraceWarning("Keymap loading failed: {0} in file {1}", error.Message, keyMapPath);
                }
                finally
                {
                    streamReader.Dispose();
                }
            }

            if (KeyMapList?.Count == 0)
                return;

            // Put an existing command onto an other key, or just register the custom command name
            foreach (var command in KeyMapList)
            {
                var modifiers = KeyModifiers.None;
                foreach (var modifier in command.Modifiers)
                    modifiers |= modifier;
                var userCommandKeyInput = new UserCommandKeyInput(command.ScanCode, modifiers);

                Locomotive.ContentScript.RegisterControl(command.Name, command.Index);

                if (ContentScript.AllUserCommands.ContainsKey(command.Name))
                {
                    if (!RemappedCommands.ContainsKey(ContentScript.AllUserCommands[command.Name]))
                        RemappedCommands.Add(ContentScript.AllUserCommands[command.Name], userCommandKeyInput);
                }
                else
                {
                    if (!KeyMapCommands.ContainsKey(command.Name))
                        KeyMapCommands.Add(command.Name, new Dictionary<KeyMapCommand, UserCommandInput>());
                    KeyMapCommands[command.Name].Add(command, userCommandKeyInput);
                    DisableByKey(userCommandKeyInput);
                }
            }
        }

        private static JsonSerializerSettings KeyMapSerializerSettings = new JsonSerializerSettings() 
        { 
            DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate,
            Converters = { new StringEnumConverter() },
        };

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
                    ChangingControls.Clear();
                    return;
            }

            foreach (var command in ChangingControls.Keys)
            {
                Locomotive.ContentScript.GetFloatVariable(command.Name, 1, 0, 0, out var oldValue, true);
                var speed = ChangingControls[command];
                var newValue = oldValue + speed * elapsedSeconds;
                newValue = speed > 0 ? Math.Min(newValue, command.ToValue) : Math.Max(newValue, command.ToValue);

                if (newValue != oldValue)
                {
                    Locomotive.ContentScript.TrainCarAction<MSTSLocomotive>(1, l => l.ContentScript.SignalEvent(command.Name, newValue));
                    Locomotive.ContentScript.SetFloatVariable(command.Name, 1, newValue, true);
                }
            }
        }
        
        /// <summary>
        /// Called from MSTSLocomotiveViewer.HandleUserInput(). Handles custom commands and redefined built-in commands only.
        /// </summary>
        public void HandleUserInput(ElapsedTime elapsedTime)
        {
            if (KeyMapList?.Count == 0)
                return;

            ActivatedCommands.Clear();

            // Custom commands
            foreach (var name in KeyMapCommands.Keys)
            {
                if (!ActivatedCommands.ContainsKey(name))
                    continue;

                foreach (var command in KeyMapCommands[name].Keys)
                {
                    if (command.ButtonState == KeyMapCommand.ButtonStates.Pressed && UserInputIsPressed(KeyMapCommands[name][command]) ||
                        command.ButtonState == KeyMapCommand.ButtonStates.Released && UserInputIsReleased(KeyMapCommands[name][command]))
                    {
                        if (command.Event == KeyMapCommand.Events.StartContinuousChange && ChangingControls.ContainsKey(command) ||
                            command.Event == KeyMapCommand.Events.StopContinuousChange && !ChangingControls.ContainsKey(command))
                            continue;

                        if (command.Event == KeyMapCommand.Events.ChangeTo && command.Value != float.MinValue)
                        {
                            // Value is a "from value" in this case. When the variable is not there, the command is not triggered.
                            Locomotive.ContentScript.GetFloatVariable(command.Name, command.Index, 0, 0, out var floatResult, true);
                            if (command.Value != floatResult)
                                continue;
                        }

                        ActivatedCommands.Add(command.Name, command);
                    }
                }
            }
            // Must be rolled through the command pattern for the replay to stay working correctly.
            foreach (var command in ActivatedCommands.Values)
                new ScriptedControlCommand(Locomotive.Simulator.Log, command);

            // Built-in remapped commands
            foreach (var command in RemappedCommands.Keys)
            {
                if (UserInputIsPressed(RemappedCommands[command]))
                    UserInputCommands(command, 1);
                if (UserInputIsReleased(RemappedCommands[command]))
                    UserInputCommands(command, 0);
            }
        }
        
        /// <summary>
        /// When a key of a custom command defined in keymap was pressed, signal an event to the script and store the resulting value.
        /// </summary>
        public void Redo(KeyMapCommand command)
        {
            switch (command.Event)
            {
                case KeyMapCommand.Events.ChangeTo:
                    var toValue = command.ToValue;
                    if (command.ToValue == float.MaxValue && command.Index == 1 &&
                        Locomotive.ContentScript.GetFloatVariable(command.Name, command.Index, 0, 0, out var oldValue, true))
                        toValue = 1 - MathHelper.Clamp(oldValue, 0, 1);
                    Locomotive.ContentScript.TrainCarAction<MSTSLocomotive>(command.Index, l => l.ContentScript.SignalEvent(command.Name, toValue));
                    if (command.Index == 1)
                        Locomotive.ContentScript.SetFloatVariable(command.Name, command.Index, toValue, true);
                    break;
                case KeyMapCommand.Events.StartContinuousChange:
                    if (command.Index == 1 && !ChangingControls.ContainsKey(command))
                        ChangingControls.Add(command, command.Value == float.MinValue ? 0.1f : command.Value);
                    break;
                case KeyMapCommand.Events.StopContinuousChange:
                    if (command.Index == 1)
                        ChangingControls.Remove(command);
                    break;
            }
        }
        
        /// <summary>
        /// Search in built-in keymap for key combination, and disable if found
        /// </summary>
        public void DisableByKey(UserCommandKeyInput input)
        {
            foreach (var command in ContentScript.AllUserCommands.Keys)
            {
                var commandKeyInput = Locomotive.Simulator.Settings.Input.Commands[(int)ContentScript.AllUserCommands[command]] as UserCommandKeyInput;
                if (commandKeyInput != null)
                {
                    if (commandKeyInput.ScanCode == input.ScanCode
                        && commandKeyInput.Control == input.Control
                        && commandKeyInput.Alt == input.Alt
                        && commandKeyInput.Shift == input.Shift)
                    {
                        Locomotive.ContentScript.RegisterControl(command, 1);
                        break;
                    }
                }
            }
        }
    }

    public class KeyMapCommand
    {
        [JsonProperty]
        public string Name { get; private set; }

        [JsonProperty]
        public int ScanCode { get; private set; }

        [JsonProperty]
        [DefaultValue(1)]
        public int Index { get; private set; }

        [JsonProperty]
        [DefaultValue(new KeyModifiers[] { KeyModifiers.None })]
        public KeyModifiers[] Modifiers { get; private set; }

        [JsonProperty]
        [DefaultValue(ButtonStates.Pressed)]
        public ButtonStates ButtonState { get; private set; }

        [JsonProperty]
        [DefaultValue(Events.ChangeTo)]
        public Events Event { get; private set; }

        [JsonProperty]
        [DefaultValue(float.MinValue)]
        public float Value { get; private set; }

        [JsonProperty]
        [DefaultValue(float.MaxValue)]
        public float ToValue { get; private set; }

        [JsonProperty]
        [DefaultValue(0)]
        public int SoundTrigger { get; private set; }

        public enum Events
        {
            ChangeTo,
            StartContinuousChange,
            StopContinuousChange,
        }

        public enum ButtonStates
        {
            Released,
            Pressed,
        }
    }
}
