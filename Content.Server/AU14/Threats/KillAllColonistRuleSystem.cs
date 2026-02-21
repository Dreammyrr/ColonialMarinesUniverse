using System;
using System.Linq;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs;
using Content.Shared.NPC.Components;
using Content.Shared.GameTicking.Components;

namespace Content.Server.AU14.Threats;

public sealed class KillAllColonistRuleSystem : GameRuleSystem<KillAllColonistRuleComponent>
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly Round.AuRoundSystem _auRoundSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMobStateChanged(MobStateChangedEvent ev)
    {
        // Only run this logic when the KillAllColonist rule is active
        if (!_gameTicker.IsGameRuleActive<KillAllColonistRuleComponent>())
            return;

        // Only care about dead mobs
        if (ev.NewMobState != MobState.Dead)
            return;

        // Get the active rule entity and its component to read Percent
        var queryRule = EntityQueryEnumerator<KillAllColonistRuleComponent, GameRuleComponent>();
        if (!queryRule.MoveNext(out var ruleEnt, out var ruleComp, out var gameRuleComp) || !GameTicker.IsGameRuleActive(ruleEnt, gameRuleComp))
            return;

        var requiredPercent = Math.Clamp(ruleComp.Percent, 1, 100);

        // Count total and dead AUColonist mobs
        var total = 0;
        var dead = 0;

        var query = _entityManager.EntityQueryEnumerator<MobStateComponent, NpcFactionMemberComponent>();
        while (query.MoveNext(out var _, out var mobState, out var faction))
        {
            if (faction.Factions.Any(f => f.ToString().ToLowerInvariant() == "aucolonist"))
            {
                total++;
                if (mobState.CurrentState == MobState.Dead)
                    dead++;
            }
        }

        if (total == 0)
            return; // nothing to count

        var percentDead = (int) ((double)dead / total * 100.0);

        if (percentDead >= requiredPercent)
        {

            if (_gameTicker.RunLevel != GameRunLevel.InRound)
                return;

            // End round, threat wins
            var winMessage = _auRoundSystem._selectedthreat?.WinMessage;
            if (!string.IsNullOrEmpty(winMessage))
                _gameTicker.EndRound(winMessage!);
            else
                _gameTicker.EndRound("Threat victory: Required percentage of Colonists eliminated.");
        }
    }
}
