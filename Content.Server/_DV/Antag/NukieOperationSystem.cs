using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Objectives;
using Content.Shared._Mono.Company;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Objectives.Components;
using Content.Shared.Random.Helpers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._DV.Antag;

public sealed partial class NukieOperationSystem : GameRuleSystem<NukieOperationComponent>
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private ObjectivesSystem _objectives = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerCompanyCompSpawned);
    }

    private void OnPlayerCompanyCompSpawned(PlayerSpawnCompleteEvent args)
    {
        if (!_mind.TryGetMind(args.Player, out var mindId, out var mind))
            return;

        if (!TryComp<CompanyComponent>(args.Mob, out var userCompany))
            return;

        var query = QueryActiveRules();
        var rules = new List<(EntityUid, NukieOperationComponent)>();
        while (query.MoveNext(out var uid, out _, out var operation, out _))
        {
            rules.Add((uid, operation));
        }
        foreach (var (uid, operation) in rules)
        {
            if (operation.ChosenOperation == null)
            {
                if (!_proto.TryIndex(operation.Operations, out var opProto))
                    return;

                operation.ChosenOperation = _random.Pick(opProto.Weights);
            }

            if (!_proto.TryIndex(operation.ChosenOperation, out var chosenOp))
                return;

            foreach (var objectiveProto in chosenOp.OperationObjectives)
            {
                if (operation.ParticipatingCompany != userCompany.CompanyName)
                    return;
                if (!_objectives.TryCreateObjective((mindId, mind), objectiveProto, out var objective))
                {
                    Log.Error("Couldn't create objective for company member: " + mindId); // This should never happen.
                    continue;
                }

                _mind.AddObjective(mindId, mind, objective.Value);
                Log.Info("Adding objective " + objective +  " to mindId " + mindId);
            }
        }
    }

    protected override void AppendRoundEndText(EntityUid uid,
        NukieOperationComponent component,
        GameRuleComponent gameRule,
        ref RoundEndTextAppendEvent args)
    {
        if (_proto.TryIndex(component.ChosenOperation, out var opProto) &&
            _proto.TryIndex(component.ParticipatingCompany, out var company))
        {
            var compMemberQuery = EntityQueryEnumerator<MindContainerComponent, CompanyComponent>();
            var compMembers = new List<Entity<MindContainerComponent, CompanyComponent>>();
            while (compMemberQuery.MoveNext(out var eligibleUid, out var mindComp, out var compComp))
            {
                if (compComp.CompanyName == company)
                    compMembers.Add((eligibleUid, mindComp, compComp));

            }
            var startText = Loc.GetString("fac-operation-start", ("company", company.Name), ("$color", company.Color));
            args.AddLine(startText);
            args.AddLine(Loc.GetString("fac-operation-members-list-start"));

            foreach (var member in compMembers)
            {
                args.AddLine(Loc.GetString("fac-operation-members-list-name",("name", Name(member))));
            }

            var objectives = opProto.OperationObjectives;
            args.AddLine(Loc.GetString("fac-operation-objectives-list-start"));
            foreach(var obj in objectives)
            {
                if (_proto.TryIndex<EntityPrototype>(obj, out var entityProto))
                {
                    entityProto.Components.TryGetComponent("Objective", out var objComp);
                    if (objComp != null)
                    {
                        var objCompNew = (ObjectiveComponent)objComp;
                            args.AddLine(objCompNew.LocRoundEndText);
                    }
                }
            }
        }
    }
}
