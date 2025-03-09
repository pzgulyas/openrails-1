// COPYRIGHT 2009, 2010, 2011, 2012, 2013, 2014 by the Open Rails project.
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
using Orts.Parsers.Msts;

namespace Orts.Simulation.RollingStocks.SubSystems.Brakes.MSTS
{
    public abstract class MSTSBrakeSystem : BrakeSystem
    {
        public static BrakeSystem Create(string type, TrainCar car)
        {
            switch (type)
            {
                case "manual_braking": return new ManualBraking(car);
                case "straight_vacuum_single_pipe": return new StraightVacuumSinglePipe(car);
                case "vacuum_twin_pipe":
                case "vacuum_single_pipe": return new VacuumSinglePipe(car);
                case "air_twin_pipe": return new AirTwinPipe(car);
                case "air_single_pipe": return new AirSinglePipe(car);
                case "ecp":
                case "ep": return new EPBrakeSystem(car);
                case "sme": return new SMEBrakeSystem(car);
                case "air_piped":
                case "vacuum_piped": return new SingleTransferPipe(car);
                default: return new SingleTransferPipe(car);
            }
        }

        protected TrainCar Car;

        public abstract void Update(float elapsedClockSeconds);

        public abstract void InitializeFromCopy(BrakeSystem copy);

        public virtual void InitializeDefault() { }

        public virtual bool InitializePredefined(string type, BrakeModes mode, FrictionType frictionType) { return false; }

        public virtual void Parse(string lowercasetoken, STFReader stf)
        {
            MSTSBrakeSystem newSystem;
            switch (lowercasetoken)
            {
                case "wagon(ortsbrakemodesfilter":
                    stf.VerifyStartOfBlock();
                    while (!stf.EndOfBlock())
                        BrakeModesFilter.Add(stf.ReadString());
                    break;
                case "wagon(ortsbrakemode":
                    stf.VerifyStartOfBlock();
                    MSTSBrakeSystem newBrakeSystem = null;
                    while (!stf.EndOfBlock())
                    {
                        stf.ReadItem();
                        var lowercasetoken2 = stf.Tree.ToLower();
                        switch (lowercasetoken2)
                        {
                            case "wagon(ortsbrakemode(ortsbrakemodename":
                                if (stf.ReadStringBlock(null) is var brakeMode && string.IsNullOrEmpty(brakeMode) && Enum.TryParse(brakeMode, out BrakeModes _))
                                    BrakeMode = brakeMode;
                                break;
                            case "wagon(ortsbrakemode(brakesystemtype":
                                var brakeSystemType = stf.ReadStringBlock(null).ToLower();
                                if (BrakeMode != null && !Car.BrakeSystems.ContainsKey(BrakeMode))
                                {
                                    newBrakeSystem = Create(brakeSystemType, Car) as MSTSBrakeSystem;
                                    newBrakeSystem.BrakeMode = BrakeMode;
                                    newBrakeSystem.InitializeDefault();
                                    newBrakeSystem.Diff = true;
                                    Car.BrakeSystems.Add(BrakeMode, newBrakeSystem);
                                }
                                break;
                            default:
                                if (newBrakeSystem != null)
                                {
                                    lowercasetoken2 = "wagon" + lowercasetoken2.Substring(lowercasetoken2.LastIndexOf('('));
                                    newBrakeSystem.Parse(lowercasetoken2, stf);
                                }
                                else
                                {
                                    stf.SkipRestOfBlock();
                                }
                                break;
                        }
                    }
                    break;
            }
        }

        public enum BrakeModes
        {
            G, // Goods
            P, // Passanger
            R, // Rapid
            RR, // Rapid with accelerator, <R>
            R_Mg, // Rapid with Magnetic Track Brakes, R+Mg
            RR_Mg, // Rapid with accelerator and Magnetic Track Brakes, <R>+Mg
            T, // Passanger + railcar (FIXME, probably to be removed)
            EP, // Electro-Pneumatic
            LE, // Light Engine
            PL, // Passanger Long train
            PS, // Passanger Short train
            AU, // Air Unfitted/unbraked

            VB, // Vacuum Goods
            VP, // Vacuum Passanger
            VU, // Vacuum Unfitted/unbraked
        }

        public enum FrictionType
        {
            Clasp,
            Disc,
        }
    }
}
