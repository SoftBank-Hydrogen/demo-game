using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Draws one Mochi per joined player: team A on the left facing right, team B on the right facing left,
// holding a rope at hand height. My character stands nearest the rope centre and gets a "YOU" marker.
// More than 5 players per team use two rows (back row smaller and darker). The rope's knot moves
// toward the team that is ahead, and a team leans back when it taps.
public class TugCrowdView : MonoBehaviour
{
    [Header("Scene references")]
    [SerializeField] TugClient client;
    [SerializeField] RectTransform arena;       // area the crowd lives in (this object)
    [SerializeField] RectTransform crowd;       // parent for character instances
    [SerializeField] RectTransform template;    // inactive character: Mochi (image) + Marker
    [SerializeField] RectTransform rope, knot;
    [SerializeField] Text overflowA, overflowB;

    [Header("Look")]
    // Complete character images (Assets/Art/Tug/MochiA.png, MochiB.png). Edit the PNGs directly;
    // both face right, team B is mirrored here.
    [SerializeField] Sprite teamASprite, teamBSprite;
    [SerializeField, Min(1)] int maxPerTeam = 10, maxPerRow = 5;
    [SerializeField, Range(.3f, 1.2f)] float characterHeightShare = .62f;   // of arena height
    [SerializeField] float centreGap = 34, maxShiftShare = .06f;
    [SerializeField] float backRowScale = .84f, backRowLift = .34f, backRowShade = .78f;
    [SerializeField] float leanDegrees = 14, leanDecay = 5, bobPixels = 2.5f;

    const float HandHeight = 58f / 128f;        // matches TugArtwork: hands at y≈58 of 128

    readonly List<Character> pool = new List<Character>();
    int shownRosterVersion = -1, lastTapsA, lastTapsB, lastRound = -1;
    float shift, leanA, leanB;
    bool reducedMotion;

    class Character
    {
        public RectTransform root, marker;
        public Image image;
        public string team;
        public int row, column, rowCount;       // rowCount: characters in this row
        public float phase;
    }

    public void SetReducedMotion(bool value) => reducedMotion = value;

    void Update()
    {
        if (client.RosterVersion != shownRosterVersion) Rebuild();

        // Lean when a team's tap count goes up; reset counters on a new round.
        var taps = client.Taps;
        if (client.Round != lastRound) { lastRound = client.Round; lastTapsA = taps.A; lastTapsB = taps.B; }
        if (taps.A > lastTapsA) leanA = Mathf.Min(1, leanA + (taps.A - lastTapsA) * .25f);
        if (taps.B > lastTapsB) leanB = Mathf.Min(1, leanB + (taps.B - lastTapsB) * .25f);
        lastTapsA = taps.A; lastTapsB = taps.B;
        float dt = Time.unscaledDeltaTime;
        leanA = Mathf.Max(0, leanA - dt * (leanDecay * leanA + .2f));
        leanB = Mathf.Max(0, leanB - dt * (leanDecay * leanB + .2f));

        // The knot is pulled toward the team that is ahead.
        int total = taps.A + taps.B;
        float ratioA = total > 0 ? taps.A / (float)total : .5f;
        float target = -(ratioA - .5f) * 2 * arena.rect.width * maxShiftShare;
        shift = reducedMotion ? target : Mathf.Lerp(shift, target, 1 - Mathf.Exp(-dt * 4));

        Place();
    }

    void Rebuild()
    {
        shownRosterVersion = client.RosterVersion;
        var a = new List<RosterEntry>(); var b = new List<RosterEntry>();
        foreach (var p in client.Roster) (p.team == "A" ? a : b).Add(p);
        a = Ordered(a, out int hiddenA);
        b = Ordered(b, out int hiddenB);
        overflowA.text = hiddenA > 0 ? "+" + hiddenA : "";
        overflowB.text = hiddenB > 0 ? "+" + hiddenB : "";

        while (pool.Count < a.Count + b.Count) pool.Add(Create());
        int k = 0;
        var placed = new List<Character>();
        foreach (var (list, team) in new[] { (a, "A"), (b, "B") })
            for (int i = 0; i < list.Count; i++) placed.Add(Assign(pool[k++], list[i], team, i, list.Count));
        for (; k < pool.Count; k++) pool[k].root.gameObject.SetActive(false);

        // Draw order: back row first, then front row; within a row, outer characters first.
        placed.Sort((x, y) => x.row != y.row ? y.row.CompareTo(x.row) : y.column.CompareTo(x.column));
        foreach (var c in placed) c.root.SetAsLastSibling();
    }

    // My character first (nearest the centre), then join order; keep at most maxPerTeam.
    List<RosterEntry> Ordered(List<RosterEntry> team, out int hidden)
    {
        int mine = team.FindIndex(p => p.id == client.PlayerId);
        if (mine > 0) { var me = team[mine]; team.RemoveAt(mine); team.Insert(0, me); }
        hidden = Mathf.Max(0, team.Count - maxPerTeam);
        return hidden > 0 ? team.GetRange(0, maxPerTeam) : team;
    }

    Character Create()
    {
        var root = (RectTransform)Instantiate(template, crowd);
        return new Character
        {
            root = root,
            image = root.Find("Mochi").GetComponent<Image>(),
            marker = (RectTransform)root.Find("Marker"),
        };
    }

    Character Assign(Character c, RosterEntry p, string team, int index, int count)
    {
        int perRow = count <= maxPerRow ? count : Mathf.CeilToInt(count / 2f);
        c.team = team;
        c.row = index < perRow ? 0 : 1;
        c.column = c.row == 0 ? index : index - perRow;
        c.rowCount = c.row == 0 ? perRow : count - perRow;
        c.phase = index * 1.7f;
        c.root.gameObject.SetActive(true);
        c.root.name = "Mochi " + team + " " + p.id;
        float shade = c.row == 0 ? 1 : backRowShade;    // multiplies the image: back row a bit darker
        c.image.sprite = team == "A" ? teamASprite : teamBSprite;
        c.image.color = new Color(shade, shade, shade, 1);
        c.marker.gameObject.SetActive(p.id == client.PlayerId);
        return c;
    }

    void Place()
    {
        var r = arena.rect;
        float size = r.height * characterHeightShare;
        float ground = -r.height + size * .04f;                // arena pivot is top-left
        float maxShift = r.width * maxShiftShare;
        float centre = r.width * .5f + shift;
        // Room for one team, leaving space for the knot shift so nobody leaves the arena.
        float room = r.width * .5f - centreGap - maxShift - size;
        float t = Time.unscaledTime;
        int count = 0;
        float frontMinX = centre, frontMaxX = centre;

        foreach (var c in pool)
        {
            if (!c.root.gameObject.activeSelf) continue;
            count++;
            bool isA = c.team == "A";
            float scale = c.row == 0 ? 1 : backRowScale;
            float s = size * scale;
            float spacing = c.rowCount <= 1 ? 0 : Mathf.Min(s * .78f, room / (c.rowCount - 1));
            float offset = centreGap + size * .5f + c.column * spacing + (c.row == 1 ? spacing * .5f : 0);
            float x = isA ? centre - offset : centre + offset;
            float y = ground + (c.row == 1 ? size * backRowLift : 0);
            float lean = (isA ? leanA : leanB) * leanDegrees;
            float bob = reducedMotion ? 0 : Mathf.Sin(t * 3f + c.phase) * bobPixels;
            float back = reducedMotion ? 0 : lean * .6f;          // step back a little while pulling

            c.root.sizeDelta = new Vector2(s, s);
            c.root.anchoredPosition = new Vector2(x + (isA ? -back : back), y + bob);
            c.root.localScale = new Vector3(isA ? 1 : -1, 1, 1);
            float tilt = reducedMotion ? 0 : lean;              // mirrored for B by the scale
            c.root.localRotation = Quaternion.Euler(0, 0, tilt);
            // Keep "YOU" upright and readable whatever the character does. Under B's mirrored
            // parent a child rotation appears reversed, so the counter-rotation flips sign too.
            c.marker.localRotation = Quaternion.Euler(0, 0, isA ? -tilt : tilt);
            float markerScale = Mathf.Clamp(s / 80f, 1, 1.8f);   // bigger on tall (phone) layouts
            c.marker.localScale = new Vector3((isA ? 1 : -1) * markerScale, markerScale, 1);

            if (c.row == 0)
            {
                frontMinX = Mathf.Min(frontMinX, x - s * .45f);
                frontMaxX = Mathf.Max(frontMaxX, x + s * .45f);
            }
        }

        float ropeY = ground + size * HandHeight;
        rope.anchoredPosition = new Vector2(frontMinX, ropeY);
        rope.sizeDelta = new Vector2(Mathf.Max(0, frontMaxX - frontMinX), rope.sizeDelta.y);
        rope.gameObject.SetActive(count > 0);
        knot.anchoredPosition = new Vector2(centre, ropeY);
        knot.gameObject.SetActive(count > 0);
        overflowA.alignment = TextAnchor.UpperLeft; overflowB.alignment = TextAnchor.UpperRight;
        overflowA.rectTransform.anchoredPosition = new Vector2(0, -26);
        overflowB.rectTransform.anchoredPosition = new Vector2(r.width - overflowB.rectTransform.sizeDelta.x, -26);
    }

    // Test hook: how many characters are shown per team and whether mine is marked.
    public int Shown(string team) { int n = 0; foreach (var c in pool) if (c.root.gameObject.activeSelf && c.team == team) n++; return n; }
    public bool MineMarked() { foreach (var c in pool) if (c.root.gameObject.activeSelf && c.marker.gameObject.activeSelf) return true; return false; }
}
