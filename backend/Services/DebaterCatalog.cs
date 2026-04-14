using TryingStuff.Models;

namespace TryingStuff.Services;

public static class DebaterCatalog
{
    private static readonly IReadOnlyList<DebaterPersona> Personas =
    [
        // Lithuanian
        new() { Id = "ausra", Name = "Aušra", Character = "Warm and practical Lithuanian parent coach who emphasizes daily habits and realistic family routines." },
        new() { Id = "mindaugas", Name = "Mindaugas", Character = "Sharp Lithuanian skeptic who challenges conventional wisdom and pushes for stronger evidence." },
        new() { Id = "ruta", Name = "Rūta", Character = "Calm Lithuanian mediator who highlights nuance and seeks balanced, well-grounded decisions." },
        new() { Id = "tomas", Name = "Tomas", Character = "Direct Lithuanian debate captain who is concise, strategic, and assertive under pressure." },
        // Belarusian
        new() { Id = "alena", Name = "Alena", Character = "Empathetic Belarusian counselor focused on emotional wellbeing and family cohesion." },
        new() { Id = "viktar", Name = "Viktar", Character = "Data-oriented Belarusian analyst who favors measurable outcomes and honest tradeoffs." },
        new() { Id = "darya", Name = "Darya", Character = "Bold Belarusian contrarian who questions consensus and surfaces hidden systemic risks." },
        new() { Id = "ivan", Name = "Ivan", Character = "No-nonsense Belarusian pragmatist focused on what families can realistically sustain day to day." },
        // French
        new() { Id = "claire", Name = "Claire", Character = "Curious French educator who explains ideas with clear examples, analogies, and Cartesian logic." },
        new() { Id = "benoit", Name = "Benoît", Character = "Optimistic French intellectual who frames arguments around long-term human development and progress." },
        new() { Id = "noemie", Name = "Noémie", Character = "Passionate French advocate who defends her position with conviction, flair, and social conscience." },
        new() { Id = "pierre", Name = "Pierre", Character = "Methodical French academic who builds arguments step by step with precision and scholarly rigor." }
    ];

    public static IReadOnlyList<DebaterPersona> GetAll()
    {
        return Personas;
    }

    public static (DebaterPersona Pro, DebaterPersona Con) ResolvePair(string? proDebaterId, string? conDebaterId)
    {
        var pro = ResolveByIdOrDefault(proDebaterId, defaultId: "ausra");
        var con = ResolveByIdOrDefault(conDebaterId, defaultId: "mindaugas");

        if (pro.Id.Equals(con.Id, StringComparison.OrdinalIgnoreCase))
        {
            con = Personas.First(p => !p.Id.Equals(pro.Id, StringComparison.OrdinalIgnoreCase));
        }

        return (pro, con);
    }

    private static DebaterPersona ResolveByIdOrDefault(string? id, string defaultId)
    {
        if (!string.IsNullOrWhiteSpace(id))
        {
            var match = Personas.FirstOrDefault(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match;
            }
        }

        return Personas.First(p => p.Id.Equals(defaultId, StringComparison.OrdinalIgnoreCase));
    }
}
