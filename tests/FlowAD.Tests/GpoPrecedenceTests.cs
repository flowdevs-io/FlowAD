using FlowAD.Core.Models;
using FlowAD.Gpo;
using Xunit;

namespace FlowAD.Tests;

public class GpoPrecedenceTests
{
    [Fact]
    public void ResolvePrecedence_StandardLsdou_ChildOuWinsOverDomain()
    {
        var domainGpo = new GroupPolicyObject
        {
            Guid = Guid.NewGuid(),
            DisplayName = "Domain Policy",
            IsEnabled = true
        };

        var ouGpo = new GroupPolicyObject
        {
            Guid = Guid.NewGuid(),
            DisplayName = "OU Policy",
            IsEnabled = true
        };

        var applied = new List<(GroupPolicyObject Gpo, GpoLink Link, int Depth)>
        {
            (domainGpo, new GpoLink { TargetContainerDn = "DC=flowdevs,DC=local", Order = 1, IsEnabled = true, IsEnforced = false }, 1),
            (ouGpo, new GpoLink { TargetContainerDn = "OU=Workstations,DC=flowdevs,DC=local", Order = 1, IsEnabled = true, IsEnforced = false }, 2)
        };

        var ordered = GpoPrecedenceEngine.ResolvePrecedence(applied);

        Assert.Equal(2, ordered.Count);
        // The last GPO in ordered list has highest precedence (wins)
        Assert.Equal("Domain Policy", ordered[0].Gpo.DisplayName);
        Assert.Equal("OU Policy", ordered[1].Gpo.DisplayName);
    }

    [Fact]
    public void ResolvePrecedence_EnforcedLink_OverridesChildOu()
    {
        var domainGpo = new GroupPolicyObject
        {
            Guid = Guid.NewGuid(),
            DisplayName = "Enforced Domain Security Policy",
            IsEnabled = true
        };

        var ouGpo = new GroupPolicyObject
        {
            Guid = Guid.NewGuid(),
            DisplayName = "OU Relaxed Policy",
            IsEnabled = true
        };

        var applied = new List<(GroupPolicyObject Gpo, GpoLink Link, int Depth)>
        {
            (domainGpo, new GpoLink { TargetContainerDn = "DC=flowdevs,DC=local", Order = 1, IsEnabled = true, IsEnforced = true }, 1),
            (ouGpo, new GpoLink { TargetContainerDn = "OU=Workstations,DC=flowdevs,DC=local", Order = 1, IsEnabled = true, IsEnforced = false }, 2)
        };

        var ordered = GpoPrecedenceEngine.ResolvePrecedence(applied);

        Assert.Equal(2, ordered.Count);
        // Enforced domain policy must win over non-enforced child OU policy!
        Assert.Equal("OU Relaxed Policy", ordered[0].Gpo.DisplayName);
        Assert.Equal("Enforced Domain Security Policy", ordered[1].Gpo.DisplayName);
    }

    [Fact]
    public void ResolvePrecedence_BlockInheritance_ExcludesNonEnforcedParent()
    {
        var domainGpo = new GroupPolicyObject
        {
            Guid = Guid.NewGuid(),
            DisplayName = "Standard Domain Policy",
            IsEnabled = true
        };

        var ouGpo = new GroupPolicyObject
        {
            Guid = Guid.NewGuid(),
            DisplayName = "Isolated OU Policy",
            IsEnabled = true
        };

        var applied = new List<(GroupPolicyObject Gpo, GpoLink Link, int Depth)>
        {
            (domainGpo, new GpoLink { TargetContainerDn = "DC=flowdevs,DC=local", Order = 1, IsEnabled = true, IsEnforced = false }, 1),
            (ouGpo, new GpoLink { TargetContainerDn = "OU=Isolated,DC=flowdevs,DC=local", Order = 1, IsEnabled = true, IsEnforced = false }, 2)
        };

        // Block inheritance active at target OU
        var ordered = GpoPrecedenceEngine.ResolvePrecedence(applied, blockInheritanceAtTarget: true);

        Assert.Single(ordered);
        Assert.Equal("Isolated OU Policy", ordered[0].Gpo.DisplayName);
    }
}
