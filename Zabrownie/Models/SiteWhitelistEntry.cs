namespace Zabrownie.Models
{
    public class SiteWhitelistEntry
    {
        public string Domain { get; set; } = string.Empty;
        public bool Enabled { get; set; } = true;
    }
}