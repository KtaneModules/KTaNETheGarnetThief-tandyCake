using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using KModkit;

public class TheGarnetThiefScript : MonoBehaviour {

    public KMBombInfo Bomb;
    public KMAudio Audio;
    public KMBombModule Module;

    public KMSelectable[] Buttons;
    public Sprite[] allFactionIcons;
    public SpriteRenderer[] frontSprites;
    public SpriteRenderer[] frontFrames;
    public TextMesh[] texts;
    private Coroutine[] buttonMovements = new Coroutine[4];
    private Color[] frameColors = new Color[] { new Color(188f / 255, 73f / 255, 73f / 255), new Color(64f / 255, 131f / 255, 64f / 255), new Color(96f / 255, 96f / 255, 96f / 255), Color.white};

    static int moduleIdCounter = 1;
    int moduleId;
    private bool moduleSolved;
    const int gemCnt = 4;

    private List<Name> availableNames = Enumerable.Range(0, 13).Cast<Name>().ToList();
    private Contestant[] contestants = new Contestant[7];
    private int tableRow;
    static Faction[,] claimTable = new Faction[15,4]
    {
        { Faction.Cartel, Faction.Mafia, Faction.Beggar, Faction.Police },
        { Faction.Beggar, Faction.Police, Faction.Cartel, Faction.Mafia },
        { Faction.Mafia, Faction.Mafia, Faction.Police, Faction.Police },
        { Faction.Beggar, Faction.Beggar, Faction.Beggar, Faction.Beggar},
        { Faction.Beggar, Faction.Police, Faction.Mafia, Faction.Cartel },
        { Faction.Police, Faction.Beggar, Faction.Mafia, Faction.Cartel },
        { Faction.Cartel, Faction.Cartel, Faction.Beggar, Faction.Beggar },
        { Faction.Police, Faction.Mafia, Faction.Mafia, Faction.Mafia },
        { Faction.Cartel, Faction.Beggar, Faction.Cartel, Faction.Cartel },
        { Faction.Beggar, Faction.Police, Faction.Beggar, Faction.Police },
        { Faction.Police, Faction.Cartel, Faction.Cartel, Faction.Police },
        { Faction.Cartel, Faction.Cartel, Faction.Cartel, Faction.Cartel },
        { Faction.Mafia, Faction.Mafia, Faction.Mafia, Faction.Mafia },
        { Faction.Police, Faction.Police, Faction.Police, Faction.Police },
        { Faction.Mafia, Faction.Cartel, Faction.Police, Faction.Beggar }
    };
    static IEnumerable<Faction> factions = Enumerable.Range(0, 4).Cast<Faction>();
    int[] results = new int[4];
    List<Faction> solution;

    void Awake () {
        moduleId = moduleIdCounter++;
        foreach (KMSelectable button in Buttons)
        {
            button.OnInteract += delegate ()
            {
                if (buttonMovements[Array.IndexOf(Buttons, button)] != null)
                    StopCoroutine(buttonMovements[Array.IndexOf(Buttons, button)]);
                buttonMovements[Array.IndexOf(Buttons, button)] = StartCoroutine(ButtonMove(button));
                Submit((Faction)Array.IndexOf(Buttons, button));
                return false;
            };
        }


        //Button.OnInteract += delegate () { ButtonPress(); return false; };

    }

    void Start ()
    {
        GetTableRow();
        GetContestants();
        GetTruths();
        GetSolution();
        SetIcons();
        DoLogging();
    }

    void GetTableRow()
    {
        string inds = Bomb.GetIndicators().Join("");
        bool[] conditions = new bool[15]
        {
            Bomb.GetSerialNumberLetters().Count(x => "AEIOU".Contains(x)) > 1,
            Bomb.GetSerialNumberNumbers().Count() == 4,
            Bomb.GetPortPlates().Any(plate => new string[] {"DVI", "StereoRCA", "RJ45", "PS2"}.All(port => plate.Contains(port))),
            Bomb.GetPortPlates().Count(plt => plt.Count() == 0) >= 3,
            Bomb.GetBatteryCount() == 1,
            Bomb.GetPorts().Distinct().Count() >= 4,
            Bomb.IsIndicatorOff(Indicator.BOB) && Bomb.GetBatteryCount() == 2 && Bomb.GetBatteryHolderCount() == 2,
            Bomb.GetSolvableModuleNames().Contains("Lying Indicators") && Bomb.GetSolvableModuleNames().Contains("Mafia"),
            Bomb.GetSolvableModuleNames().Contains("Dr. Doctor") && Bomb.GetSolvableModuleNames().Contains("Stoichiometry"),
            Bomb.GetPortPlates().All(x => x.Count() == 1),
            Bomb.GetSerialNumber().Any(x => "G3N1US".Contains(x)),
            Bomb.GetBatteryHolderCount() + Bomb.GetIndicators().Count() + Bomb.GetPortPlates().Count() == 16,
            inds.Count(x => "AEIOU".Contains(x)) != 0 && inds.Count(x => !"AEIOU".Contains(x)) % inds.Count(x => "AEIOU".Contains(x)) == 0,
            Bomb.GetSolvableModuleNames().Contains("The Jukebox") && Bomb.GetSolvableModuleNames().Contains("Synchronization"),
            true
        };
        tableRow = Enumerable.Range(0, conditions.Length).First(x => conditions[x]);
    }

    void GetContestants()
    {
        for (int i = 0; i < 7; i++)
        {
            contestants[i] = new Contestant(availableNames.PickRandom(), (Faction)UnityEngine.Random.Range(0, 4));
            availableNames.Remove(contestants[i].name);
        }
    }
    void GetTruths()
    {
        int fuckYouYoohyun = -1; //Yoohyun's condition needs to be checked last. If we encounter him, postpone his checking until the end.
        for (int i = 0; i < 7; i++)
        {
            if (contestants[i].name == Name.Yoohyun)
                fuckYouYoohyun = i;
            else contestants[i].lying = DetermineTruth(contestants[i]);
        }
        if (fuckYouYoohyun != -1)
            contestants[fuckYouYoohyun].lying = DetermineTruth(contestants[fuckYouYoohyun]);
        for (int i = 0; i < 7; i++)
        {
            Contestant cont = contestants[i];
            cont.realFaction = cont.lying ? claimTable[tableRow, (int)cont.claimedFaction] : cont.claimedFaction; // if the contestant is lying, use the section of the table. Otherwise use the one they claimed.
        }
    }

    void GetSolution()
    {
        var realFactions = contestants.Select(x => x.realFaction);
        for (int i = 0; i < 4; i++)
            results[i] = DetermineOutcome(realFactions, (Faction)i);
        List<Faction> winners;
        winners = Enumerable.Range(0, 4).Where(x => results[x] == results.Max()).Cast<Faction>().ToList();
        if (winners.Count == 1)
            solution = winners; 
        else
        {
            int[] realFactionCounts = factions.Select(x => realFactions.Count(y => x == y)).ToArray();
            int minRealFactionCount = winners.Select(x => realFactionCounts[(int)x]).Min();
            winners = winners.Where(x => realFactionCounts[(int)x] == minRealFactionCount).ToList();
            if (winners.Count == 1)
                solution = winners;
            else
            {
                int[] claimedFactionCounts = factions.Select(fac => contestants.Select(cont => cont.claimedFaction).Count(y => y == fac)).ToArray();
                int minClaimedFactionCount = winners.Select(x => claimedFactionCounts[(int)x]).Min();
                winners = winners.Where(x => claimedFactionCounts[(int)x] == minClaimedFactionCount).ToList();
                solution = winners; //If there's not only one answer at this point, give up and just accept any tied answer.
            }
        }  
    }

    void SetIcons()
    {
        for (int i = 0; i < 7; i++)
        {
            Contestant cont = contestants[i];
            frontFrames[i].color = frameColors[(int)cont.claimedFaction];
            frontSprites[i].sprite = allFactionIcons[(int)cont.claimedFaction];
            texts[i].text = cont.name.ToString();
        }
    }

    void DoLogging()
    {
        for (int i = 0; i < 7; i++)
            Debug.LogFormat("[The Garnet Thief #{0}] The contestant {1} has claimed the faction {2}.", 
                            moduleId, contestants[i].name.ToString(), contestants[i].claimedFaction.ToString());
        Debug.LogFormat("[The Garnet Thief #{0}] We are using the {1} row of Table B", moduleId, ordinal(tableRow + 1));
        for (int i = 0; i < 7; i++)
        {
            if (contestants[i].lying)
                Debug.LogFormat("[The Garnet Thief #{0}] {1} is lying. His actual faction is {2}.",
                                moduleId, contestants[i].name.ToString(), contestants[i].realFaction.ToString());
        }
        Debug.LogFormat("[The Garnet Thief #{0}] Choosing Mafia would give you {1} garnet{2}, Cartel => {3}, Police => {4}, Beggar => {5}.",
                        moduleId, results[0], results[0] == 1 ? "" : "s", results[1], results[2], results[3]);
        if (solution.Count == 1)
            Debug.LogFormat("[The Garnet Thief #{0}] The correct faction to choose is {1}.", moduleId, solution[0].ToString());
        else Debug.LogFormat("[The Garnet Thief #{0}] The correct faction can be any out of: {1}.", moduleId, solution.Select(x => x.ToString()).Join(", "));
    }
    
    string ordinal(int input)
    {
        switch (input)
        {
            case 1: return "1st";
            case 2: return "2nd";
            case 3: return "3rd";
            default: return input + "th";
        }
    }

    bool DetermineTruth(Contestant thisCont)
    {
        Faction[] claimedFactions = contestants.Select(cont => cont.claimedFaction).ToArray();
        int[] factionCounts = factions.Select(x => claimedFactions.Count(y => x == y)).ToArray();
        switch (thisCont.name)
        {
            case Name.Jungmoon: return factionCounts.Distinct().Count() == 4; //If this is not 4, there's at least 1 duplicate
            case Name.Yeonseung: return claimTable[tableRow, (int)thisCont.claimedFaction] == Faction.Beggar;
            case Name.Jinho: return Bomb.GetSolvableModuleNames().Contains("The 1, 2, 3 Game") || Bomb.GetSolvableModuleNames().Contains("English Entries");
            case Name.Dongmin: return thisCont.claimedFaction == Faction.Police || thisCont.claimedFaction == Faction.Beggar;
            case Name.Kyunghoon: return factionCounts[0] > factionCounts[1]; //mafia > cartel claims
            case Name.Kyungran: return thisCont.claimedFaction == Faction.Mafia || thisCont.claimedFaction == Faction.Cartel;
            case Name.Yoohyun: return contestants.Count(cont => cont.lying) % 2 == 0;
            case Name.Junseok: return claimTable[tableRow, (int)thisCont.claimedFaction] != Faction.Beggar;
            case Name.Sangmin: return factionCounts[0] == factionCounts[1]; // mafia = cartel claims
            case Name.Yohwan: return factionCounts[1] > factionCounts[0]; // cartel > mafia claims
            case Name.Yoonsun: return factionCounts.Any(x => x >= 4);
            case Name.Hyunmin: return factionCounts.Any(count => factionCounts.Count(fjalsf => fjalsf == count) == 3); //returns if there's any factions which occur 3 times
            case Name.Junghyun: return factionCounts[2] == factionCounts[3]; // police = beggar claims
            default: throw new ArgumentOutOfRangeException("thisCont.name");
        }
    }

    int DetermineOutcome(IEnumerable<Faction> people, Faction you)
    {
        int[] counts = Enumerable.Range(0, 4).Select(x => people.Count(y => (int)y == x)).ToArray();
        counts[(int)you]++; //Makes sure that you're included in the selection
        if (counts[0] == counts[1]) //If mafia == cartels
        {
            switch (you)
            {
                case Faction.Mafia:
                case Faction.Cartel:
                    return 0;
                case Faction.Police:
                    return gemCnt / counts[2]; //Return gems / police
                case Faction.Beggar:
                    if (counts[2] != 0) //If there are no police, this'll throw a dividebyzero exception. 
                        return gemCnt % counts[2] / counts[3];
                    else return gemCnt / counts[3];
                default: throw new ArgumentOutOfRangeException("you"); //This will never happen, but visual studio will not shut the fuck up if I don't include this.
            }
        }
        else
        {
            Faction popularOrg = counts[0] > counts[1] ? Faction.Mafia : Faction.Cartel;   
            if (you == popularOrg) //Popular org gets even split
                return gemCnt / counts[(int)popularOrg];
            else if (you == Faction.Beggar) //Beggars get even split of remainder from pop org split
                return gemCnt % counts[(int)popularOrg] / counts[3];
            else return 0; //Police or the unpopular org get nothing
        }
    }

    void Submit(Faction submission)
    {
        if (moduleSolved)
            return;
        if (solution.Contains(submission))
        {
            moduleSolved = true;
            Debug.LogFormat("[The Garnet Thief #{0}] You pressed {1}. That is correct; module solved!", moduleId, submission.ToString());
            Module.HandlePass();
            Audio.PlaySoundAtTransform("solveSound", transform);
            FlipShit();
        }
        else
        {
            Debug.LogFormat("[The Garnet Thief #{0}] You pressed {1}. That is incorrect; strike incurred.", moduleId, submission.ToString());
            Module.HandleStrike();
        }
    }

    void FlipShit()
    {
        for (int i = 0; i < 7; i++)
            if (contestants[i].lying)
                StartCoroutine(FlipCard(i));
    }

    IEnumerator FlipCard(int ix, float speed = 2)
    {
        Transform cardTF = frontFrames[ix].transform;
        while (cardTF.localScale.x - Time.deltaTime > 0)
        {
            cardTF.localScale -= new Vector3(speed * Time.deltaTime, 0, 0);
            yield return null;
        }
        cardTF.localScale = new Vector3(0, 1, 1);
        frontFrames[ix].color = frameColors[(int)contestants[ix].realFaction];
        frontSprites[ix].sprite = allFactionIcons[(int)contestants[ix].realFaction];
        frontSprites[ix].flipX = true;
        while (cardTF.localScale.x - Time.deltaTime > -1)
        {
            cardTF.localScale -= new Vector3(speed * Time.deltaTime, 0, 0);
            yield return null;
        }
        cardTF.localScale = new Vector3(-1, 1, 1);
    }

    IEnumerator ButtonMove(KMSelectable button)
    {
        button.AddInteractionPunch(1);
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, button.transform);
        while (button.transform.localPosition.y > -0.004)
        {
            button.transform.localPosition += 0.075f * Time.deltaTime * Vector3.down;
            yield return null;
        }
        while (button.transform.localPosition.y < 0)
        {
            button.transform.localPosition += 0.075f * Time.deltaTime * Vector3.up;
            yield return null;
        }
    }

#pragma warning disable 414
    private readonly string TwitchHelpMessage = @"Use <!{0} submit mafia> to choose that role.";
    #pragma warning restore 414

    IEnumerator ProcessTwitchCommand (string command)
    {
        string[] validCommands = new string[] { "MAFIA", "CARTEL", "POLICE", "BEGGAR", "M", "C", "P", "B", "RED", "GREEN", "BLACK", "WHITE", "R", "G", "K", "W", "", "", "", "BEGGARS" };
        command = command.Trim().ToUpperInvariant();
        List<string> parameters = command.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
        if (parameters[0] == "SUBMIT" || parameters[0] == "PRESS")
            parameters.RemoveAt(0);
        else yield break;
        if (parameters.Count == 1 && validCommands.Contains(parameters[0]))
        {
            yield return null;
            Buttons[Array.IndexOf(validCommands, parameters[0]) % 4].OnInteract();
        }
        else yield return "sendtochaterror Unknown button.";
    }

    IEnumerator TwitchHandleForcedSolve ()
    {
        Buttons[(int)solution.First()].OnInteract();
        yield return new WaitForSeconds(0.1f);
    }
}
