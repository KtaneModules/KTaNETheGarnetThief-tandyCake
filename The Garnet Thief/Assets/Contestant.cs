public class Contestant
{
    public Name name;
    public Faction claimedFaction;
    public Faction realFaction;
    public bool lying;

    public Contestant(Name n, Faction disp)
    {
        name = n;
        claimedFaction = disp;    
    }
}
