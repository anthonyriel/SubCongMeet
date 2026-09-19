namespace SubcongMeet.Models;

public class PlayerEventReport : PlayerMedalReport
{
    public string? Player { get; set; }
    public string? District { get; set; }
    public List<string> Districts { get; set; } = new();
    public List<PlayerEventMedal> Results { get; set; } = new();

    public static List<PlayerEventMedal> Build(IEnumerable<Event> events, IReadOnlyDictionary<int, string> teams, string? search, string? district)
    {
        var query = System.Text.RegularExpressions.Regex.Replace(search?.Trim() ?? "", @"\s+", " ");
        var results = new List<PlayerEventMedal>();
        foreach (var ev in events)
        {
            foreach (var player in CountMedals(new[] { ev }, teams))
            {
                if (!player.Name.Contains(query, StringComparison.OrdinalIgnoreCase)) continue;
                if (!string.IsNullOrEmpty(district) && player.District != district) continue;
                if (player.Gold > 0) Add("Gold", 0);
                if (player.Silver > 0) Add("Silver", 1);
                if (player.Bronze > 0) Add("Bronze", 2);
                void Add(string medal, int order) => results.Add(new PlayerEventMedal {
                    Name = player.Name, District = player.District, Division = player.Division,
                    Group = player.Group, EventTitle = ev.Title, Sport = ev.SportName ?? "Unassigned",
                    Category = ev.SportCategory ?? "Unassigned", Medal = medal, MedalOrder = order
                });
            }
        }
        return results.OrderBy(r => r.MedalOrder).ThenBy(r => r.Name).ThenBy(r => r.Division)
            .ThenBy(r => r.Sport).ThenBy(r => r.EventTitle).ToList();
    }
}

public class PlayerEventMedal
{
    public string Name { get; set; } = "";
    public string District { get; set; } = "";
    public string Division { get; set; } = "";
    public string Group { get; set; } = "";
    public string EventTitle { get; set; } = "";
    public string Sport { get; set; } = "";
    public string Category { get; set; } = "";
    public string Medal { get; set; } = "";
    public int MedalOrder { get; set; }
}
