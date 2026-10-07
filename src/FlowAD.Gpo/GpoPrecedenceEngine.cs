using FlowAD.Core.Models;

namespace FlowAD.Gpo;

public sealed class EvaluatedGpoOrder
{
    public required GroupPolicyObject Gpo { get; init; }
    public required string ContainerDn { get; init; }
    public required int ContainerDepth { get; init; } // 0=Site, 1=Domain, 2=Parent OU, 3=Child OU
    public required int LinkOrder { get; init; } // 1 is highest priority at container
    public required bool IsEnforced { get; init; }

    /// <summary>
    /// Higher precedence score means this GPO overrides lower scored GPOs.
    /// </summary>
    public long PrecedenceRank { get; set; }
}

public static class GpoPrecedenceEngine
{
    /// <summary>
    /// Orders a set of GPOs targeting an OU scope according to standard Active Directory LSDOU precedence rules,
    /// factoring in link order, Block Inheritance, and Enforced links.
    /// Returns the list ordered from lowest precedence to highest precedence (winner is last).
    /// </summary>
    public static List<EvaluatedGpoOrder> ResolvePrecedence(
        IEnumerable<(GroupPolicyObject Gpo, GpoLink Link, int ContainerDepth)> appliedLinks,
        bool blockInheritanceAtTarget = false)
    {
        var list = new List<EvaluatedGpoOrder>();

        foreach (var (gpo, link, depth) in appliedLinks)
        {
            if (!gpo.IsEnabled || gpo.IsCompletelyDisabled || !link.IsEnabled)
            {
                continue;
            }

            // If block inheritance is set at target, skip parent policies unless they are Enforced
            if (blockInheritanceAtTarget && depth < 2 && !link.IsEnforced)
            {
                continue;
            }

            long rank;
            if (link.IsEnforced)
            {
                // Enforced policies always win over non-enforced.
                // Among enforced policies, higher containers (Domain before OU) win, and lower link order wins.
                rank = 1_000_000_000L - (depth * 10_000L) - (link.Order * 10L);
            }
            else
            {
                // Normal LSDOU: Deeper containers (Child OU > Parent OU > Domain > Site) win.
                // Within container, lower link order wins.
                rank = (depth * 10_000L) - (link.Order * 10L);
            }

            list.Add(new EvaluatedGpoOrder
            {
                Gpo = gpo,
                ContainerDn = link.TargetContainerDn,
                ContainerDepth = depth,
                LinkOrder = link.Order,
                IsEnforced = link.IsEnforced,
                PrecedenceRank = rank
            });
        }

        // Return ordered from lowest to highest precedence (so that later entries win)
        return list.OrderBy(x => x.PrecedenceRank).ToList();
    }
}
