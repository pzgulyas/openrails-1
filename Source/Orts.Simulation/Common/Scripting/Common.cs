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
using Orts.Common;
using Orts.Simulation;

namespace ORTS.Scripting.Api
{
    public abstract class AbstractScriptClass
    {
        /// <summary>
        /// Clock value (in seconds) for the simulation. Starts with a value = session start time.
        /// </summary>
        public Func<float> ClockTime;
        /// <summary>
        /// Clock value (in seconds) for the simulation. Starts with a value = 0.
        /// </summary>
        public Func<float> GameTime;
        /// <summary>
        /// Running total of distance travelled - always positive, updated by train physics.
        /// </summary>
        public Func<float> DistanceM;
        /// <summary>
        /// Confirms a command done by the player with a pre-set message on the screen.
        /// </summary>
        public Action<CabControl, CabSetting> Confirm;
        /// <summary>
        /// Displays a message on the screen.
        /// </summary>
        public Action<ConfirmLevel, string> Message;
        /// <summary>
        /// Sends an event to the locomotive.
        /// </summary>
        public Action<Event> SignalEvent;
        /// <summary>
        /// Sends an event to the train.
        /// </summary>
        public Action<Event> SignalEventToTrain;

        /// <summary>
        /// Helper delegate for the real interface function. This is not intended for being accessed by the script writers.
        /// </summary>
        public Func<string, int, int, int, float> _getFloatDelegate { private get; set; }
        public Func<string, int, int, int, string> _getStringDelegate { private get; set; }
        public Func<string, int, int, int, bool> _getBoolDelegate { private get; set; }
        public Func<string, int, int, int, int> _getIntDelegate { private get; set; }
        public Action<string, int, float> _setFloatDelegate { private get; set; }
        public Action<string, int, string> _setStringDelegate { private get; set; }
        public Action<string, int, bool> _setBoolDelegate { private get; set; }
        public Action<string, int, int> _setIntDelegate { private get; set; }

        public float GetFloatVariable(string variableName, int index1 = 1, int index2 = 0, int index3 = 0)
            => _getFloatDelegate(variableName, index1, index2, index3);

        public string GetStringVariable(string variableName, int index1 = 1, int index2 = 0, int index3 = 0)
            => _getStringDelegate(variableName, index1, index2, index3);

        public bool GetBoolVariable(string variableName, int index1 = 1, int index2 = 0, int index3 = 0)
            => _getBoolDelegate(variableName, index1, index2, index3);

        public int GetIntVariable(string variableName, int index1 = 1, int index2 = 0, int index3 = 0)
            => _getIntDelegate(variableName, index1, index2, index3);

        public void SetFloatVariable(string variableName, float value) => SetFloatVariable(variableName, 1, value);
        public void SetFloatVariable(string variableName, int index, float value) => _setFloatDelegate(variableName, index, value);

        public void SetStringVariable(string variableName, string value) => SetStringVariable(variableName, 1, value);
        public void SetStringVariable(string variableName, int index, string value) => _setStringDelegate(variableName, index, value);

        public void SetBoolVariable(string variableName, bool value) => SetBoolVariable(variableName, 1, value);
        public void SetBoolVariable(string variableName, int index, bool value) => _setBoolDelegate(variableName, index, value);

        public void SetIntVariable(string variableName, int value) => SetIntVariable(variableName, 1, value);
        public void SetIntVariable(string variableName, int index, int value) => _setIntDelegate(variableName, index, value);

    }

    /// <summary>
    /// Base class for Timer and OdoMeter. Not to be used directly.
    /// </summary>
    public class Counter
    {
        float EndValue;
        protected Func<float> CurrentValue;

        public float AlarmValue { get; private set; }
        public float RemainingValue { get { return EndValue - CurrentValue(); } }
        public bool Started { get; private set; }
        public void Setup(float alarmValue) { AlarmValue = alarmValue; }
        public void Start() { EndValue = CurrentValue() + AlarmValue; Started = true; }
        public void Stop() { Started = false; }
        public bool Triggered { get { return Started && CurrentValue() >= EndValue; } }
    }

    public class Timer : Counter
    {
        public Timer(AbstractScriptClass asc)
        {
            CurrentValue = asc.GameTime;
        }
    }

    public class OdoMeter : Counter
    {
        public OdoMeter(AbstractScriptClass asc)
        {
            CurrentValue = asc.DistanceM;
        }
    }

    public class Blinker
    {
        float StartValue;
        protected Func<float> CurrentValue;

        public float FrequencyHz { get; private set; }
        public bool Started { get; private set; }
        public void Setup(float frequencyHz) { FrequencyHz = frequencyHz; }
        public void Start() { StartValue = CurrentValue(); Started = true; }
        public void Stop() { Started = false; }
        public bool On { get { return Started && ((CurrentValue() - StartValue) % (1f / FrequencyHz)) * FrequencyHz * 2f < 1f; } }

        public Blinker(AbstractScriptClass asc)
        {
            CurrentValue = asc.GameTime;
        }
    }
}
