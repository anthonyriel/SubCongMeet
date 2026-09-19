using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace SubcongMeet.Models;

public class RecordedDelegationWinner
{
    public string Name { get; set; } = "";
    public string District { get; set; } = "";
    public string Gender { get; set; } = "";
    public string Result { get; set; } = "";
    public string Key => Identity(Name, District);

    public static string Identity(string name, string? district) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(Normalize(name) + "\n" + Normalize(district ?? ""))));

    private static string Normalize(string value) => Regex.Replace(value.Trim(), @"\s+", " ").ToUpperInvariant();

    public static List<RecordedDelegationWinner> Build(IEnumerable<Event> events, Event target,
        IReadOnlyDictionary<int, string> teams)
    {
        var winners = new Dictionary<string, RecordedDelegationWinner>();
        if (string.IsNullOrWhiteSpace(target.SportName)) return new();
        foreach (var ev in events.Where(e => e.Status == "Completed"
            && Normalize(e.SportName ?? "") == Normalize(target.SportName)
            && Normalize(e.Division) == Normalize(target.Division)).OrderBy(e => e.Id))
        {
            Add(ev.SilverWinnerName, ev.SilverTeamId, "Silver");
            Add(ev.BronzeWinnerName, ev.BronzeTeamId, "Bronze");

            void Add(string? names, int? teamId, string medal)
            {
                if (teamId == null || !teams.TryGetValue(teamId.Value, out var district)
                    || string.IsNullOrWhiteSpace(names)) return;
                foreach (var line in names.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var name = Regex.Replace(line.Trim(), @"\s+", " ");
                    if (name.Length == 0) continue;
                    var key = Identity(name, district);
                    var result = $"{medal} — {ev.Title}";
                    if (winners.TryGetValue(key, out var existing))
                    {
                        if (!existing.Result.Split("; ").Contains(result)) existing.Result += "; " + result;
                        continue;
                    }
                    bool boys = Regex.IsMatch(ev.Title ?? "", @"\bboys?\b", RegexOptions.IgnoreCase);
                    bool girls = Regex.IsMatch(ev.Title ?? "", @"\bgirls?\b", RegexOptions.IgnoreCase);
                    winners.Add(key, new() { Name = name, District = district, Result = result,
                        Gender = boys && !girls ? "M" : girls && !boys ? "W" : "" });
                }
            }
        }
        return winners.Values.OrderBy(w => w.Name).ThenBy(w => w.District).ToList();
    }
}
