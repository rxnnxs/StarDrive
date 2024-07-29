﻿using Microsoft.Xna.Framework;
using SDUtils;
using Ship_Game.Data.Serialization;
using System.Linq;

namespace Ship_Game
{

    [StarDataType]
    public class InfiltrationOpsCounterEspionage : InfiltrationOperation
    {
        [StarData] readonly Empire Owner;
        [StarData] readonly Empire Them;
        public const float PercentOfLevelCost = 0.5f;
        const int SuccessTargetNumber = 25; // need to get 25 and above in a roll of d100)
        const float BaseRelationDamage = 10;
        public const int BaseRampUpTurns = 30;

        [StarDataConstructor]
        public InfiltrationOpsCounterEspionage() { }

        public InfiltrationOpsCounterEspionage(Empire owner, Empire them, int levelCost) :
            base((int)(levelCost * PercentOfLevelCost), InfiltrationOpsType.CounterEspionage, BaseRampUpTurns, owner)
        {
            Owner = owner;
            Them = them;
        }

        public override void CompleteOperation()
        {
            var result = RollMissionResult(Owner, Them, Owner.IsAlliedWith(Them) ? SuccessTargetNumber / 2 : SuccessTargetNumber);
            InfiltrationOpsResolve aftermath = new InfiltrationOpsResolve(Owner, Them, result);
            Espionage espionage = Owner.GetEspionage(Them);
            Espionage theirEspionage = Them.GetEspionage(Owner);
            var potentialMoles = Them.data.MoleList.Filter(m => !m.Sticky && Owner.GetPlanets().Any(p => p.Id == m.PlanetId));

            switch (result)
            {
                case InfiltrationOpsResult.Phenomenal:
                    if (theirEspionage.Level > 0)
                    {
                        theirEspionage.WipeoutInfiltration();
                        aftermath.CustomMessage = $"{Them.data.Traits.Name}: {Localizer.Token(GameText.CounterEspioangeOpsExposedWeWipedOut)}\n" +
                                                  $"{Localizer.Token(GameText.TheirInfiltrationLevelWas)} {theirEspionage.Level}";
                        aftermath.MessageToVictim = $"{Owner.data.Traits.Name}: {Localizer.Token(GameText.CounterEspioangeOpsExposedAndWipedOut)}";
                    }
                    else if (potentialMoles.Length > 0)
                    {
                        RemoveMole();
                    }

                    break;
                case InfiltrationOpsResult.GreatSuccess:
                    if (theirEspionage.Level > 0)
                    {
                        aftermath.Message = GameText.CounterEspioangeOpsWeExposedPartially;
                        aftermath.MessageToVictim = $"{Owner.data.Traits.Name}: {Localizer.Token(GameText.CounterEspioangeOpsWeExposedPartially)}";
                        theirEspionage.ReduceInfiltrationLevel();
                    }
                    else if (potentialMoles.Length > 0)
                    {
                        RemoveMole();
                    }

                    break;
                case InfiltrationOpsResult.Success:
                    if (potentialMoles.Length > 0)
                        RemoveMole();
                    break;
                case InfiltrationOpsResult.Fail:
                    aftermath.Message = Localizer.Token(GameText.CounterEspioangeOpsFailed);
                    break;
                case InfiltrationOpsResult.MiserableFail:
                    aftermath.Message = GameText.CounterEspioangeOpsFailedMiserably;
                    aftermath.MessageToVictim = Localizer.Token(GameText.InfiltrationCounterEspionageMiserableFailVictim);
                    break;
                case InfiltrationOpsResult.CriticalFail:
                    aftermath.Message = GameText.CounterEspioangeOpsFailedDetected;
                    aftermath.MessageToVictim = $"{Localizer.Token(GameText.CounterEspioangeOpsFailedAgentCaught)}\n{Localizer.Token(GameText.NtheAgentWasSentBy)} {Owner.data.Traits.Name}";
                    aftermath.RelationDamage = CalcRelationDamage(BaseRelationDamage, espionage);
                    espionage.ReduceInfiltrationLevel();
                    aftermath.breakTreatiesIfAllied = false;
                    aftermath.DamageReason = "Caught Spying";
                    break;
                case InfiltrationOpsResult.Disaster:
                    aftermath.Message = GameText.CounterEspioangeOpsFailedWipeout;
                    aftermath.MessageToVictim = $"{Localizer.Token(GameText.CounterEspioangeOpsFailedWipeoutVictim)}\n" +
                                                $"{Localizer.Token(GameText.NtheAgentWasSentBy)} {Owner.data.Traits.Name}\n" +
                                                $"{Localizer.Token(GameText.TheirInfiltrationLevelWas)} {espionage.EffectiveLevel}.";
                    aftermath.RelationDamage = CalcRelationDamage(BaseRelationDamage, espionage, withLevelMultiplier: true);
                    aftermath.breakTreatiesIfAllied = false;
                    aftermath.DamageReason = "Caught Spying Failed";
                    espionage.WipeoutInfiltration();
                    break;
            }

            aftermath.SendNotifications(Owner.Universe);

            void RemoveMole()
            {
                Mole mole = Them.Random.Item(potentialMoles);
                aftermath.Planet = Them.Universe.GetPlanet(mole.PlanetId);
                aftermath.CustomMessage = $"{Localizer.Token(GameText.EliminatedMole)} {aftermath.Planet.Name}\n({Them.data.Traits.Name})";
                aftermath.MessageToVictim = $"{Localizer.Token(GameText.LostMole)} {aftermath.Planet.Name}";
                Them.RemoveMole(mole, Owner);
            }
        }
    }
}
