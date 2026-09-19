using System.Text.RegularExpressions;

namespace SubcongMeet.Models;

public class PlayerMedalReport
{
    public List<PlayerMedalRow> Players { get; set; } = new();
    public List<string> Sports { get; set; } = new();
    public List<string> Categories { get; set; } = new();
    public List<string> Divisions { get; set; } = new();
    public string? Sport { get; set; }
    public string? Category { get; set; }
    public string? Division { get; set; }

    public static List<PlayerMedalRow> CountMedals(IEnumerable<Event> events, IReadOnlyDictionary<int, string> teams)
    {
        var players = new Dictionary<(string Name, int Team, string Division, string Group), PlayerMedalRow>();
        foreach (var ev in events.Where(e => e.Status == "Completed"))
        {
            bool boys = Regex.IsMatch(ev.Title ?? "", @"\bboys?\b", RegexOptions.IgnoreCase);
            bool girls = Regex.IsMatch(ev.Title ?? "", @"\bgirls?\b", RegexOptions.IgnoreCase);
            string group = boys && !girls ? "Boys" : girls && !boys ? "Girls" : "Mixed";
            Add(ev.GoldWinnerName, ev.GoldTeamId, 0);
            Add(ev.SilverWinnerName, ev.SilverTeamId, 1);
            Add(ev.BronzeWinnerName, ev.BronzeTeamId, 2);

            void Add(string? names, int? teamId, int medal)
            {
                if (teamId is null or <= 0 || string.IsNullOrWhiteSpace(names)) return;
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                // Match the result form: one full player name per line, including any commas.
                foreach (var line in names.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var name = Regex.Replace(line.Trim(), @"\s+", " ");
                    if (name.Length == 0 || !seen.Add(name)) continue;
                    var key = (name.ToUpperInvariant(), teamId.Value, ev.Division.Trim().ToUpperInvariant(), group);
                    if (!players.TryGetValue(key, out var player))
                    {
                        player = new PlayerMedalRow { Name = name, District = teams.GetValueOrDefault(teamId.Value) ?? $"District ID: {teamId}", Division = ev.Division, Group = group };
                        players.Add(key, player);
                    }
                    if (medal == 0) player.Gold++;
                    else if (medal == 1) player.Silver++;
                    else player.Bronze++;
                }
            }
        }
        return players.Values.OrderBy(p => p.Division).ThenByDescending(p => p.Gold)
            .ThenByDescending(p => p.Silver).ThenByDescending(p => p.Bronze)
            .ThenBy(p => p.Name).ThenBy(p => p.District).ToList();
    }
}

public class PlayerMedalRow
{
    public string Group { get; set; } = "Mixed";
    public string Name { get; set; } = "";
    public string District { get; set; } = "";
    public string Division { get; set; } = "";
    public int Gold { get; set; }
    public int Silver { get; set; }
    public int Bronze { get; set; }
    public int Total => Gold + Silver + Bronze;
}
