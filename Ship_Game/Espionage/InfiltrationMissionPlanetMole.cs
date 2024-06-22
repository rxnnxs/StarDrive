﻿using Ship_Game.Data.Serialization;

namespace Ship_Game
{

    [StarDataType]
    public class InfiltrationMissionPlantMole : InfiltrationMission
    {
        [StarData] readonly Empire Owner;
        [StarData] readonly Empire Them;
        const float PercentOfLevelCost = 0.2f;
        const int SuccessTargetNumber = 20; // need to get 20 and above in a roll of d100)
        const float BaseRelationDamage = 10;

        public InfiltrationMissionPlantMole(Empire owner, Empire them, int levelCost, byte level) :
            base((int)(levelCost * PercentOfLevelCost), level, InfiltrationMissionType.PlantMole)
        {
            Owner = owner;
            Them = them;
        }

        public override void CompleteMission()
        {
            InfiltrationMissionResolve aftermath = new InfiltrationMissionResolve(Owner, Them);
            var result = RollMissionResult(Owner, Them, Owner.IsAlliedWith(Them) ? SuccessTargetNumber / 2 : SuccessTargetNumber, Level);
            Espionage espionage = Owner.GetEspionage(Them);
            switch (result)
            {
                case InfiltrationMissionResult.GreatSuccess:
                case InfiltrationMissionResult.Success:
                    SetProgress(Cost * 0.5f);
                    aftermath.GoodResult = true;
                    var mole = Mole.PlantMole(Owner, Them, out string planetName);
                    if (mole != null)
                        aftermath.CustomMessage = $"{Localizer.Token(GameText.NewSuccessfullyInfiltratedAColony)} {planetName}.";
                    else
                        aftermath.CustomMessage = $"{Localizer.Token(GameText.NoColonyForInfiltration)} {Them.data.Traits.Name}";

                    if (result == InfiltrationMissionResult.GreatSuccess)
                    {
                        SetProgress(Cost * 0.5f);
                        if (mole != null)
                            aftermath.CustomMessage = $"{aftermath.CustomMessage}\n{Localizer.Token(GameText.WeMadeInfiltrationProgress)}";
                    }

                    break;
                case InfiltrationMissionResult.Fail:
                    aftermath.CustomMessage = $"{Them.data.Traits.Name}: {Localizer.Token(GameText.NewWasUnableToInfiltrate)}";
                    break;
                case InfiltrationMissionResult.MiserableFail:
                    aftermath.CustomMessage = $"{Them.data.Traits.Name}: {Localizer.Token(GameText.NewWasUnableToInfiltrateMiserable)}";
                    espionage.ReduceInfiltrationLevel();
                    break;
                case InfiltrationMissionResult.CriticalFail:
                    aftermath.CustomMessage = $"{Them.data.Traits.Name}: {Localizer.Token(GameText.NewWasUnableToInfiltrateDetected)}";
                    aftermath.MessageToVictim = $"{Localizer.Token(GameText.AnEnemyAgentWasFoiled)} {Localizer.Token(GameText.NtheAgentWasSentBy)} {Owner.data.Traits.Name}";
                    aftermath.RelationDamage = CalcRelationDamage(BaseRelationDamage, espionage);
                    aftermath.DamageReason = "Caught Spying";
                    espionage.ReduceInfiltrationLevel();
                    break;
                case InfiltrationMissionResult.Disaster:
                    aftermath.CustomMessage = $"{Them.data.Traits.Name}: {Localizer.Token(GameText.NewWasUnableToInfiltrateWipedOut)}";
                    aftermath.MessageToVictim = $"{Localizer.Token(GameText.NewWipedOutNetworkInfiltration)}\n" +
                                                $"{Localizer.Token(GameText.NtheAgentWasSentBy)} {Owner.data.Traits.Name}\n" +
                                                $"{Localizer.Token(GameText.TheirInfiltrationLevelWas)} {espionage.Level}";
                    aftermath.RelationDamage = CalcRelationDamage(BaseRelationDamage, espionage, withLevelMultiplier: true);
                    aftermath.DamageReason = "Caught Spying Failed";
                    espionage.WipeoutInfiltration();
                    break;
            }

            aftermath.SendNotifications(Owner.Universe);
        }
    }
}
