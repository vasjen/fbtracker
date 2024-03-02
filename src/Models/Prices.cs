namespace fbtracker.Models;

public class Prices
{
    public Prices(Ps ps, Pc pc)
    {
        Ps = ps;
        Pc = pc;
    }
  
    public Ps Ps { get; set; } = new();
    public Pc Pc { get; set; } = new();
}

public sealed class Pc : BasePrice
{
    
}
public sealed class Ps : BasePrice
{
    
}

public abstract class BasePrice
{
    public int LCPrice { get; set; }
    public int LCPrice2 { get; set; }
    public int LCPrice3 { get; set; }
    public int LCPrice4 { get; set; }
    public int LCPrice5 { get; set; }
    public int MinPrice { get; set; }
    public int MaxPrice { get; set; }
    public int PRP { get; set; }
    public int LCPClosing { get; set; }
    public string Updated { get; set; } = string.Empty;
    public int Average => GetAverage();

    private int GetAverage()
    {
        int[] prices = { LCPrice, LCPrice2, LCPrice3, LCPrice4, LCPrice5 };
        int sum = 0;
        int count = 0;

        foreach (int price in prices)
        {
            if (price != 0)
            {
                sum += price;
                count++;
            }
        }

        return count > 0 ? sum / count : 0;
    }
    
}