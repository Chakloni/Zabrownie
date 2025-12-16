using System;

namespace Zabrownie.Models
{
    public class RecentSite
    {
        public string Title { get; set; } = "";
        public string Url { get; set; } = "";
        public DateTime VisitedAt { get; set; } = DateTime.Now;
    }
}